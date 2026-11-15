using System.Security.Cryptography;
using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Storage;

public sealed record QuizItem(Message Message, IReadOnlyList<Message> Context);

// Which messages questions come from.
public enum QuestionSelection
{
    // The most-reacted share of messages.
    Reacted,

    // Any message that ends its run, whatever its reactions.
    Random,
}

public sealed class ChatSession(Chat chat, IReadOnlyList<Message> eligible, IReadOnlyList<Message>? randomPool = null)
{
    private const int MessageOverheadBytes = 120;

    private readonly HashSet<int> _askedPositions = [];
    private readonly Lock _gate = new();

    // 256 random bits from the operating system's cryptographic generator. The ID works like a password.
    public string Id { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    public Chat Chat { get; } = chat;
    public IReadOnlyList<Message> Eligible { get; } = eligible;
    public IReadOnlyList<Message> RandomPool { get; } = randomPool ?? eligible;

    public IReadOnlyList<Message> Pool(QuestionSelection selection) =>
        selection == QuestionSelection.Random ? RandomPool : Eligible;

    // Only people who sent a message in the pool are offered as options. Someone who never features, such as an
    // automated account, would be an obviously wrong answer.
    public IReadOnlyList<int> OptionPeople(QuestionSelection selection) => selection == QuestionSelection.Random
        ? _randomOptionPeople.Value
        : _reactedOptionPeople;

    private readonly IReadOnlyList<int> _reactedOptionPeople = Senders(eligible);
    private readonly Lazy<IReadOnlyList<int>> _randomOptionPeople = new(() => Senders(randomPool ?? eligible));

    private static IReadOnlyList<int> Senders(IReadOnlyList<Message> messages) =>
        messages.Select(message => message.Sender).Distinct().Order().ToList();

    // Strings take 2 bytes per character in .NET.
    public long EstimatedBytes { get; } =
        (chat.Title.Length + chat.People.Length) * 2L
        + chat.Messages.Sum(message => MessageOverheadBytes + message.Sealed.Length * 2L);

    // Choosing and recording happen under one lock, so parallel requests can't get the same message.
    // Context messages are recorded too, since a player who has seen them knows who sent them.
    public QuizItem? TakeNext(
        Func<IReadOnlyList<Message>, Message?> pick,
        Func<Message, IReadOnlyList<Message>> findContext,
        QuestionSelection selection = QuestionSelection.Reacted)
    {
        lock (_gate)
        {
            var next = pick(Pool(selection).Where(message => !_askedPositions.Contains(message.Position)).ToList());
            if (next is null)
            {
                return null;
            }

            var context = findContext(next);
            _askedPositions.Add(next.Position);
            _askedPositions.UnionWith(context.Select(message => message.Position));

            return new QuizItem(next, context);
        }
    }
}
