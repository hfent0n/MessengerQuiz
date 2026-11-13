using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Multiplayer;

public enum RoomStage { Lobby, Question, Reveal, Finished, Ended }

public enum JoinOutcome { Joined, AlreadyStarted, Full, NameTaken }

// The game's rules. Callers hold Gate while using a room, so its methods don't lock themselves.
// Methods that take the time are given it, which keeps the rules easy to test.
public sealed class Room(string code, string chatId, ChatSession session, TimeSpan timeLimit, int maxPlayers)
{
    private readonly List<Player> _players = [];
    private readonly HashSet<int> _askedPositions = [];
    private readonly Dictionary<string, PlayerAnswer> _answers = [];
    private readonly HashSet<string> _ready = [];
    private IReadOnlyList<WhoSaidThisQuestion> _questions = [];

    public Lock Gate { get; } = new();

    public string Code { get; } = code;
    public string ChatId { get; } = chatId;
    public ChatSession Session { get; } = session;
    public TimeSpan TimeLimit { get; } = timeLimit;
    public int MaxPlayers { get; } = maxPlayers;

    public RoomStage Stage { get; private set; } = RoomStage.Lobby;

    // Rises with every change, so players can ignore an update that arrives after a newer one.
    public int Version { get; private set; }

    // Rises with every question, so a timer for an earlier question can't end the current one.
    public int Round { get; private set; }

    public int QuestionIndex { get; private set; } = -1;
    public DateTimeOffset QuestionStartedAt { get; private set; }
    public DateTimeOffset Deadline { get; private set; }

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<WhoSaidThisQuestion> Questions => _questions;
    public IReadOnlySet<int> AskedPositions => _askedPositions;
    public IReadOnlyDictionary<string, PlayerAnswer> Answers => _answers;
    public IReadOnlySet<string> ReadyPlayerIds => _ready;

    public WhoSaidThisQuestion? CurrentQuestion =>
        QuestionIndex >= 0 && QuestionIndex < _questions.Count ? _questions[QuestionIndex] : null;

    public Player? Host => _players.FirstOrDefault(player => player.IsHost);

    public JoinOutcome AddPlayer(Player player)
    {
        if (Stage != RoomStage.Lobby)
        {
            return JoinOutcome.AlreadyStarted;
        }

        if (_players.Count >= MaxPlayers)
        {
            return JoinOutcome.Full;
        }

        if (_players.Any(existing => string.Equals(existing.Name, player.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return JoinOutcome.NameTaken;
        }

        _players.Add(player);
        Changed();
        return JoinOutcome.Joined;
    }

    public bool CanStart(Player player) =>
        player.IsHost && (Stage is RoomStage.Lobby or RoomStage.Finished) && _players.Count >= 2;

    // Also used for a rematch, which keeps the players and resets their scores.
    public void Start(IReadOnlyList<WhoSaidThisQuestion> questions, IEnumerable<int> usedPositions, DateTimeOffset now)
    {
        _questions = questions;
        _askedPositions.UnionWith(usedPositions);
        foreach (var player in _players)
        {
            player.Score = 0;
            player.AnswerTime = TimeSpan.Zero;
        }

        QuestionIndex = -1;
        BeginNextQuestion(now);
    }

    // Returns false when the answer isn't accepted: it's late, repeated, or not one of the options.
    public bool Answer(Player player, int option, DateTimeOffset now, TimeSpan grace)
    {
        var question = CurrentQuestion;
        if (Stage != RoomStage.Question || question is null || _answers.ContainsKey(player.Id)
            || !question.Options.Contains(option) || now > Deadline + grace)
        {
            return false;
        }

        var elapsed = Clamp(now - QuestionStartedAt, TimeSpan.Zero, TimeLimit);
        var points = Scoring.Points(option == question.Answer, elapsed, TimeLimit);
        player.Score += points;
        player.AnswerTime += elapsed;
        _answers[player.Id] = new PlayerAnswer(option, points, elapsed);

        if (EveryConnectedPlayer(candidate => _answers.ContainsKey(candidate.Id)))
        {
            RevealAnswer();
        }

        Changed();
        return true;
    }

    // Called by the question's timer. Returns false if that question has already ended.
    public bool TimeOut(int round)
    {
        if (Stage != RoomStage.Question || Round != round)
        {
            return false;
        }

        RevealAnswer();
        Changed();
        return true;
    }

    public bool MarkReady(Player player, DateTimeOffset now)
    {
        if (Stage != RoomStage.Reveal || !_ready.Add(player.Id))
        {
            return false;
        }

        AdvanceIfEveryoneIsReady(now);
        Changed();
        return true;
    }

    // A player who leaves no longer holds up the others.
    public void SetConnection(Player player, string? connectionId, DateTimeOffset now)
    {
        player.ConnectionId = connectionId;

        if (Stage == RoomStage.Question && EveryConnectedPlayer(candidate => _answers.ContainsKey(candidate.Id)))
        {
            RevealAnswer();
        }
        else if (Stage == RoomStage.Reveal)
        {
            AdvanceIfEveryoneIsReady(now);
        }

        Changed();
    }

    public void End()
    {
        Stage = RoomStage.Ended;
        Changed();
    }

    private void BeginNextQuestion(DateTimeOffset now)
    {
        _answers.Clear();
        _ready.Clear();

        if (QuestionIndex + 1 >= _questions.Count)
        {
            Stage = RoomStage.Finished;
            return;
        }

        QuestionIndex++;
        Round++;
        Stage = RoomStage.Question;
        QuestionStartedAt = now;
        Deadline = now + TimeLimit;
    }

    private void RevealAnswer()
    {
        foreach (var player in _players.Where(player => !_answers.ContainsKey(player.Id)))
        {
            player.AnswerTime += TimeLimit;
        }

        Stage = RoomStage.Reveal;
    }

    private void AdvanceIfEveryoneIsReady(DateTimeOffset now)
    {
        if (EveryConnectedPlayer(player => _ready.Contains(player.Id)))
        {
            BeginNextQuestion(now);
        }
    }

    // With nobody connected, the game waits: the timer still ends a question, but nothing moves on by itself.
    private bool EveryConnectedPlayer(Func<Player, bool> condition)
    {
        var connected = _players.Where(player => player.Connected).ToList();
        return connected.Count > 0 && connected.All(condition);
    }

    private void Changed() => Version++;

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        value < min ? min : value > max ? max : value;
}
