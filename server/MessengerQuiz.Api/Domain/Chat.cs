namespace MessengerQuiz.Api.Domain;

public sealed record Chat(string Title, IReadOnlyList<string> Participants, IReadOnlyList<Message> Messages);

public sealed record Message(
    string Id,
    string Sender,
    string Text,
    DateTime? SentAt,
    IReadOnlyList<Reaction> Reactions,
    bool HasMedia);

public sealed record Reaction(string Emoji, string Reactor);
