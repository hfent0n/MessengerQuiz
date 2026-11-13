namespace MessengerQuiz.Api.Domain;

// A chat as the server holds it. The browser encrypts everything readable before upload, so the server works
// only with numbers and flags, and hands sealed boxes back for the browser to open.
public sealed record Chat(
    // The sealed title and list of people's names.
    string Title,
    string People,
    // People are numbered from 0, most messages first.
    int PersonCount,
    // Oldest first.
    IReadOnlyList<Message> Messages)
{
    // Automated accounts, such as Meta AI, that the browser recognised by name. They're rarely offered as options.
    public IReadOnlySet<int> Automated { get; init; } = new HashSet<int>();
}

public sealed record Message(
    // The message's place in the chat. Its sealed box only opens with this position, so it can't be moved.
    int Position,
    int Sender,
    DateTime? SentAt,
    int Reactions,
    int MediaCount,
    // Worked out by the browser: long enough and not a system line or bare link, or has media.
    bool Quizzable,
    // An event line, such as someone naming the group. It ends a run of context.
    bool System,
    // The text, reactions and media paths.
    string Sealed);
