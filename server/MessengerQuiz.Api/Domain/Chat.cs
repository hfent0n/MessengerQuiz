namespace MessengerQuiz.Api.Domain;

public sealed record Chat(string Title, IReadOnlyList<string> Participants, IReadOnlyList<Message> Messages)
{
    // Drops photos missing from the player's folder. Exports often leave out older media.
    public Chat KeepPhotos(IReadOnlySet<string> availablePhotos) => this with
    {
        Messages = Messages
            .Select(message => message.Photos.Count == 0
                ? message
                : message with { Photos = message.Photos.Where(availablePhotos.Contains).ToList() })
            .ToList(),
    };
}

public sealed record Message(
    string Id,
    string Sender,
    string Text,
    DateTime? SentAt,
    IReadOnlyList<Reaction> Reactions,
    bool HasMedia)
{
    // Paths relative to the chat folder, such as "photos/123.jpg". GIFs, videos and stickers aren't included.
    public IReadOnlyList<string> Photos { get; init; } = [];
}

public sealed record Reaction(string Emoji, string Reactor);
