using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Storage;

public sealed class ChatSession(Chat chat, IReadOnlyList<Message> eligible)
{
    private const int MessageOverheadBytes = 200;
    private const int ReactionOverheadBytes = 100;

    private readonly HashSet<string> _askedMessageIds = [];
    private readonly Lock _gate = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public Chat Chat { get; } = chat;
    public IReadOnlyList<Message> Eligible { get; } = eligible;

    public long EstimatedBytes { get; } = chat.Messages.Sum(message =>
        MessageOverheadBytes + message.Text.Length * 2L + message.Reactions.Count * ReactionOverheadBytes);

    // Choosing and recording happen under one lock, so parallel requests can't get the same message.
    public Message? TakeNext(Func<IReadOnlyList<Message>, Message?> pick)
    {
        lock (_gate)
        {
            var next = pick(Eligible.Where(message => !_askedMessageIds.Contains(message.Id)).ToList());
            if (next is not null)
            {
                _askedMessageIds.Add(next.Id);
            }

            return next;
        }
    }
}
