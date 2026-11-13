using System.Security.Cryptography;
using MessengerQuiz.Api.Domain;

namespace MessengerQuiz.Api.Storage;

public sealed record QuizItem(Message Message, IReadOnlyList<Message> Context);

public sealed class ChatSession(Chat chat, IReadOnlyList<Message> eligible)
{
    private const int MessageOverheadBytes = 120;

    private readonly HashSet<int> _askedPositions = [];
    private readonly Lock _gate = new();

    // 256 random bits from the operating system's cryptographic generator. The ID works like a password.
    public string Id { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    public Chat Chat { get; } = chat;
    public IReadOnlyList<Message> Eligible { get; } = eligible;

    // Only people who sent a top message are offered as options. Someone who never features, such as an automated
    // account, would be an obviously wrong answer.
    public IReadOnlyList<int> OptionPeople { get; } = eligible.Select(message => message.Sender).Distinct().Order().ToList();

    // Strings take 2 bytes per character in .NET.
    public long EstimatedBytes { get; } =
        (chat.Title.Length + chat.People.Length) * 2L
        + chat.Messages.Sum(message => MessageOverheadBytes + message.Sealed.Length * 2L);

    // Choosing and recording happen under one lock, so parallel requests can't get the same message.
    // Context messages are recorded too, since a player who has seen them knows who sent them.
    public QuizItem? TakeNext(
        Func<IReadOnlyList<Message>, Message?> pick,
        Func<Message, IReadOnlyList<Message>> findContext)
    {
        lock (_gate)
        {
            var next = pick(Eligible.Where(message => !_askedPositions.Contains(message.Position)).ToList());
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
