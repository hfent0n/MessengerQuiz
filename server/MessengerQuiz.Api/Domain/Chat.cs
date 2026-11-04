namespace MessengerQuiz.Api.Domain;

public sealed record Chat(string Title, IReadOnlyList<string> Participants, IReadOnlyList<Message> Messages)
{
    // Drops media missing from the player's files. Exports often leave out older media.
    public Chat KeepMedia(IReadOnlySet<string> availableMedia) => this with
    {
        Messages = Messages
            .Select(message => message.Media.Count == 0
                ? message
                : message with { Media = message.Media.Where(availableMedia.Contains).ToList() })
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
    // Photos, GIFs and videos, as paths relative to the chat folder, such as "photos/123.jpg" or "videos/456.mp4".
    // The folder says which kind each is. Stickers and voice messages aren't included.
    public IReadOnlyList<string> Media { get; init; } = [];
}

public sealed record Reaction(string Emoji, string Reactor);
