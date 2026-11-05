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
}

public sealed record PlayerAnswer(string Option, int Points, TimeSpan Elapsed);
