namespace MessengerQuiz.Api.Multiplayer;

public sealed class Player(string id, string token, string name, bool isHost)
{
    // Shown to other players.
    public string Id { get; } = id;

    // Secret, kept by the player's browser to reconnect after a refresh.
    public string Token { get; } = token;

    public string Name { get; } = name;
    public bool IsHost { get; } = isHost;

    public string? ConnectionId { get; set; }
    public bool Connected => ConnectionId is not null;

    public int Score { get; set; }

    // Breaks ties. A question that wasn't answered counts as the whole time limit.
    public TimeSpan AnswerTime { get; set; }

    // About one chat message a second, and a few reactions a second, with short bursts allowed.
    public ActionLimiter ChatLimit { get; } = new(5, TimeSpan.FromSeconds(5));
    public ActionLimiter ReactionLimit { get; } = new(6, TimeSpan.FromSeconds(3));
}

// Option is the number of the person picked.
public sealed record PlayerAnswer(int Option, int Points, TimeSpan Elapsed);
