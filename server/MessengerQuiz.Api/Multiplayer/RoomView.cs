using MessengerQuiz.Api.Questions;

namespace MessengerQuiz.Api.Multiplayer;

// What one player sees. Built fresh for each player, so the answer and other players' picks stay hidden until
// the reveal.
public sealed record RoomView(
    string Code,
    int Version,
    // The chat's sealed title and people's names. Players open them with the key from the game link.
    string Title,
    string People,
    RoomStage Stage,
    int TimeLimitSeconds,
    string YouId,
    bool YouAreHost,
    int MaxPlayers,
    IReadOnlyList<PlayerView> Players,
    int QuestionNumber,
    // In endless mode, how many questions the chat has left to give, at most.
    int QuestionCount,
    bool Endless,
    // What a full score bar stands for. In endless mode, the questions asked so far.
    int MaxScore,
    QuestionView? Question,
    RevealView? Reveal,
    // The latest chat messages, oldest first, still sealed.
    IReadOnlyList<ChatLine> Chat)
{
    public static RoomView For(Room room, Player viewer, DateTimeOffset now)
    {
        var question = room.CurrentQuestion;
        var showsQuestion = question is not null && (room.Stage is RoomStage.Question or RoomStage.Reveal);

        // During a question, scores leave out its answers, or a rising score would show who got it right.
        // Ranked by score, with the faster total answer time breaking ties.
        var players = room.Players
            .Select(player =>
            {
                var pending = room.Stage == RoomStage.Question ? room.Answers.GetValueOrDefault(player.Id) : null;
                return (Player: player,
                    Score: player.Score - (pending?.Points ?? 0),
                    AnswerTime: player.AnswerTime - (pending?.Elapsed ?? TimeSpan.Zero));
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.AnswerTime)
            .Select(entry => new PlayerView(
                entry.Player.Id,
                entry.Player.Name,
                entry.Player.IsHost,
                entry.Player.Connected,
                entry.Score,
                room.Answers.ContainsKey(entry.Player.Id),
                room.ReadyPlayerIds.Contains(entry.Player.Id)))
            .ToList();

        return new RoomView(
            room.Code,
            room.Version,
            room.Session.Chat.Title,
            room.Session.Chat.People,
            room.Stage,
            (int)room.TimeLimit.TotalSeconds,
            viewer.Id,
            viewer.IsHost,
            room.MaxPlayers,
            players,
            room.QuestionIndex + 1,
            room.Questions.Count,
            room.Endless,
            (room.Endless ? Math.Max(room.QuestionIndex + 1, 1) : room.Questions.Count) * Scoring.MaxPointsPerQuestion,
            showsQuestion ? QuestionView.For(room, question!, viewer, now) : null,
            room.Stage == RoomStage.Reveal ? RevealView.For(room, question!) : null,
            room.Chat.ToList());
    }
}

public sealed record PlayerView(
    string Id,
    string Name,
    bool IsHost,
    bool Connected,
    int Score,
    bool Answered,
    bool Ready);

// No answer here. Players' clocks differ, so the time left is sent instead of the deadline.
public sealed record QuestionView(
    int Position,
    string Sealed,
    IReadOnlyList<ContextMessage> Context,
    IReadOnlyList<int> Options,
    int RemainingMilliseconds,
    int? YourAnswer)
{
    public static QuestionView For(Room room, WhoSaidThisQuestion question, Player viewer, DateTimeOffset now)
    {
        var remaining = room.Stage == RoomStage.Question ? Math.Max(0, (room.Deadline - now).TotalMilliseconds) : 0;
        return new QuestionView(
            question.Position,
            question.Sealed,
            question.Context,
            question.Options,
            (int)remaining,
            room.Answers.GetValueOrDefault(viewer.Id)?.Option);
    }
}

public sealed record RevealView(int Answer, int Reactions, IReadOnlyList<PickView> Picks)
{
    public static RevealView For(Room room, WhoSaidThisQuestion question) => new(
        question.Answer,
        question.Reactions,
        room.Players
            .Select(player => room.Answers.GetValueOrDefault(player.Id) is { } answer
                ? new PickView(player.Id, answer.Option, answer.Points)
                : new PickView(player.Id, null, 0))
            .ToList());
}

public sealed record PickView(string PlayerId, int? Option, int Points);
