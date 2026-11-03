using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Storage;

public sealed record QuizItem(Message Message, IReadOnlyList<Message> Context);

public sealed class ChatSession(Chat chat, IReadOnlyList<Message> eligible)
{
    private const int MessageOverheadBytes = 200;
    private const int ReactionOverheadBytes = 100;

    private readonly HashSet<string> _askedMessageIds = [];
    private readonly Dictionary<string, int> _positions = chat.Messages
        .Select((message, position) => (message.Id, position))
        .ToDictionary(entry => entry.Id, entry => entry.position);
    private readonly Lock _gate = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public Chat Chat { get; } = chat;
    public IReadOnlyList<Message> Eligible { get; } = eligible;

    public long EstimatedBytes { get; } = chat.Messages.Sum(message =>
        MessageOverheadBytes + message.Text.Length * 2L + message.Reactions.Count * ReactionOverheadBytes);

    public int PositionOf(Message message) => _positions[message.Id];

    // Choosing and recording happen under one lock, so parallel requests can't get the same message.
    // Context messages are recorded too, since a player who has seen them knows who sent them.
    public QuizItem? TakeNext(
        Func<IReadOnlyList<Message>, Message?> pick,
        Func<Message, IReadOnlyList<Message>> findContext)
    {
        lock (_gate)
        {
            var next = pick(Eligible.Where(message => !_askedMessageIds.Contains(message.Id)).ToList());
            if (next is null)
            {
                return null;
            }

            var context = findContext(next);
            _askedMessageIds.Add(next.Id);
            _askedMessageIds.UnionWith(context.Select(message => message.Id));

            return new QuizItem(next, context);
        }
    }
}
