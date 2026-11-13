using System.Collections.Concurrent;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Api.Multiplayer;

public enum CreateOutcome { Created, InvalidName, InvalidTimeLimit, TooManyRooms }

public sealed record JoinResult(JoinOutcome? Outcome, Player? Player, bool InvalidName = false, bool NotFound = false);

// Runs multiplayer games: creates rooms, applies players' actions, ends questions on time and sends every
// player their own view after each change.
public sealed class GameService : IDisposable
{
    public const string RoomEvent = "room";

    private readonly RoomStore _rooms;
    private readonly ChatStore _chats;
    private readonly MessageSelector _selector;
    private readonly IHubContext<GameHub> _hub;
    private readonly TimeProvider _time;
    private readonly ILogger<GameService> _logger;
    private readonly QuizOptions _options;

    // Which room and seat each SignalR connection belongs to.
    private readonly ConcurrentDictionary<string, (Room Room, Player Player)> _connections = new();
    private readonly ConcurrentDictionary<string, RoundTimer> _timers = new();

    public GameService(
        RoomStore rooms,
        ChatStore chats,
        MessageSelector selector,
        IHubContext<GameHub> hub,
        TimeProvider time,
        ILogger<GameService> logger,
        IOptions<QuizOptions> options)
    {
        _rooms = rooms;
        _chats = chats;
        _selector = selector;
        _hub = hub;
        _time = time;
        _logger = logger;
        _options = options.Value;
        _chats.Removed += EndRoomsForChat;
    }

    public CreateOutcome CreateRoom(
        string chatId, ChatSession session, string? hostName, int timeLimitSeconds, out Room? room, out Player? host)
    {
        room = null;
        host = null;

        var name = PlayerNames.Clean(hostName);
        if (name is null)
        {
            return CreateOutcome.InvalidName;
        }

        if (!_options.TimeLimitSeconds.Contains(timeLimitSeconds))
        {
            return CreateOutcome.InvalidTimeLimit;
        }

        if (_rooms.ForChat(chatId).Count(existing => existing.Stage != RoomStage.Ended) >= _options.MaxRoomsPerChat)
        {
            return CreateOutcome.TooManyRooms;
        }

        do
        {
            room = new Room(
                RoomStore.NewCode(), chatId, session, TimeSpan.FromSeconds(timeLimitSeconds), _options.MaxPlayers);
        }
        while (!_rooms.TryAdd(room));

        host = new Player(RoomStore.NewPlayerId(), RoomStore.NewToken(), name, isHost: true);
        lock (room.Gate)
        {
            room.AddPlayer(host);
        }

        return CreateOutcome.Created;
    }

    public async Task<JoinResult> JoinAsync(string code, string? playerName)
    {
        var room = _rooms.Find(code);
        if (room is null)
        {
            return new JoinResult(null, null, NotFound: true);
        }

        var name = PlayerNames.Clean(playerName);
        if (name is null)
        {
            return new JoinResult(null, null, InvalidName: true);
        }

        var player = new Player(RoomStore.NewPlayerId(), RoomStore.NewToken(), name, isHost: false);
        JoinOutcome outcome;
        lock (room.Gate)
        {
            outcome = room.Stage == RoomStage.Ended ? JoinOutcome.AlreadyStarted : room.AddPlayer(player);
        }

        if (outcome == JoinOutcome.Joined)
        {
            await BroadcastAsync(room);
        }

        return new JoinResult(outcome, outcome == JoinOutcome.Joined ? player : null);
    }

    // Returns an error to show the player, or null on success.
    public async Task<string?> ConnectAsync(string code, string token, string connectionId)
    {
        var room = _rooms.Find(code);
        // Compared in constant time, so response times don't reveal how much of a guessed token was right.
        var player = room?.Players.FirstOrDefault(candidate => RoomStore.TokensMatch(candidate.Token, token));
        if (room is null || player is null)
        {
            return "This game has ended or the link is wrong.";
        }

        lock (room.Gate)
        {
            if (player.ConnectionId is { } previous)
            {
                _connections.TryRemove(previous, out _);
            }

            _connections[connectionId] = (room, player);
            room.SetConnection(player, connectionId, Now);
        }

        KeepChatAlive(room);
        await BroadcastAsync(room);
        return null;
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var seat))
        {
            return;
        }

        var (room, player) = seat;
        lock (room.Gate)
        {
            // A refresh may already have connected this player again.
            if (player.ConnectionId != connectionId)
            {
                return;
            }

            room.SetConnection(player, null, Now);
            ScheduleTimeout(room);
        }

        await BroadcastAsync(room);
    }

    public Task<string?> StartAsync(string connectionId) => ActAsync(connectionId, (room, player) =>
    {
        if (!room.CanStart(player))
        {
            return player.IsHost ? "At least one other player needs to join first." : "Only the host can start the game.";
        }

        var (questions, used) = PickQuestions(room);
        if (questions.Count == 0)
        {
            return "This chat has no more questions without photos or videos.";
        }

        room.Start(questions, used, Now);
        return null;
    });

    public Task<string?> AnswerAsync(string connectionId, int option) => ActAsync(connectionId, (room, player) =>
        room.Answer(player, option, Now, _options.AnswerGrace) ? null : "That answer wasn't accepted.");

    public Task<string?> ReadyAsync(string connectionId) => ActAsync(connectionId, (room, player) =>
    {
        room.MarkReady(player, Now);
        return null;
    });

    public void Dispose()
    {
        _chats.Removed -= EndRoomsForChat;
        foreach (var timer in _timers.Values)
        {
            timer.Timer.Dispose();
        }
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    private async Task<string?> ActAsync(string connectionId, Func<Room, Player, string?> action)
    {
        if (!_connections.TryGetValue(connectionId, out var seat))
        {
            return "You're not in this game. Open the link again.";
        }

        var (room, player) = seat;
        string? error;
        lock (room.Gate)
        {
            if (room.Stage == RoomStage.Ended)
            {
                return "This game has ended.";
            }

            error = action(room, player);
            ScheduleTimeout(room);
        }

        KeepChatAlive(room);
        await BroadcastAsync(room);
        return error;
    }

    // The same selection as single player, without media, which only the host has. Players' browsers show media in
    // context as a placeholder. Messages asked in an earlier game in this room aren't used again.
    private (List<WhoSaidThisQuestion> Questions, List<int> Used) PickQuestions(Room room)
    {
        var chat = room.Session.Chat;
        var candidates = room.Session.Eligible
            .Where(message => message.MediaCount == 0 && !room.AskedPositions.Contains(message.Position))
            .ToList();
        var questions = new List<WhoSaidThisQuestion>();
        var used = new List<int>();

        while (questions.Count < _options.QuestionsPerGame
            && MessageSelector.PickBalanced(candidates, Random.Shared) is { } pick)
        {
            var context = _selector.PrecedingContext(chat, pick.Position);
            questions.Add(WhoSaidThisGenerator.Create(room.Session, pick, context));

            var shown = context.Select(message => message.Position).Append(pick.Position).ToHashSet();
            candidates.RemoveAll(message => shown.Contains(message.Position));
            used.AddRange(shown);
        }

        return (questions, used);
    }

    // Called with the room's lock held, after any change that may have started a question.
    // Each room has one timer, for its current question.
    private void ScheduleTimeout(Room room)
    {
        if (room.Stage != RoomStage.Question
            || (_timers.TryGetValue(room.Code, out var existing) && existing.Round == room.Round))
        {
            return;
        }

        var round = room.Round;
        var due = room.Deadline + _options.AnswerGrace - Now;
        var timer = _time.CreateTimer(
            _ => OnTimeout(room, round), null, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        if (_timers.TryRemove(room.Code, out var previous))
        {
            previous.Timer.Dispose();
        }

        _timers[room.Code] = new RoundTimer(timer, round);
    }

    private void OnTimeout(Room room, int round)
    {
        bool changed;
        lock (room.Gate)
        {
            changed = room.TimeOut(round);
        }

        if (changed)
        {
            _ = BroadcastSafelyAsync(room);
        }
    }

    private void EndRoomsForChat(string chatId)
    {
        foreach (var room in _rooms.ForChat(chatId))
        {
            lock (room.Gate)
            {
                room.End();
            }

            _rooms.Remove(room);
            if (_timers.TryRemove(room.Code, out var timer))
            {
                timer.Timer.Dispose();
            }

            _ = BroadcastSafelyAsync(room);
        }
    }

    // Playing counts as using the chat, so it doesn't expire mid-game.
    private void KeepChatAlive(Room room) => _chats.TryGet(room.ChatId, out _);

    private async Task BroadcastAsync(Room room)
    {
        List<(string ConnectionId, RoomView View)> views;
        lock (room.Gate)
        {
            var now = Now;
            views = room.Players
                .Where(player => player.ConnectionId is not null)
                .Select(player => (player.ConnectionId!, RoomView.For(room, player, now)))
                .ToList();
        }

        await Task.WhenAll(views.Select(item => _hub.Clients.Client(item.ConnectionId).SendAsync(RoomEvent, item.View)));
    }

    private async Task BroadcastSafelyAsync(Room room)
    {
        try
        {
            await BroadcastAsync(room);
        }
        catch (Exception exception)
        {
            // Never log the room's content, only that sending failed.
            _logger.LogWarning(exception, "Sending a room update failed.");
        }
    }

    private sealed record RoundTimer(ITimer Timer, int Round);
}
