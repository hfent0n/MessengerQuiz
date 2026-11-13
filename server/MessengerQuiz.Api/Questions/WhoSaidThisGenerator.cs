using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

// Options and the answer are people's numbers. The browser opens the sealed boxes and shows names.
public sealed record WhoSaidThisQuestion(
    int Position,
    string Sealed,
    int MediaCount,
    IReadOnlyList<ContextMessage> Context,
    IReadOnlyList<int> Options,
    int Answer,
    int Reactions);

// An earlier message from the same sender, which helps the question make sense.
public sealed record ContextMessage(int Position, string Sealed, int MediaCount);

public sealed class WhoSaidThisGenerator(MessageSelector selector) : IQuestionGenerator<WhoSaidThisQuestion>
{
    private const int MaxOptions = 4;

    public WhoSaidThisQuestion? Next(ChatSession session)
    {
        var item = session.TakeNext(
            candidates => MessageSelector.PickBalanced(candidates, Random.Shared),
            message => selector.PrecedingContext(session.Chat, message.Position));
        return item is null ? null : Create(session.Chat, item.Message, item.Context);
    }

    // Builds the question for a message that has already been chosen, with up to 4 shuffled options.
    public static WhoSaidThisQuestion Create(Chat chat, Message message, IReadOnlyList<Message> context)
    {
        var others = Enumerable.Range(0, chat.PersonCount)
            .Where(person => person != message.Sender)
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaxOptions - 1);

        var options = others.Append(message.Sender).OrderBy(_ => Random.Shared.Next()).ToList();
        var earlier = context.Select(item => new ContextMessage(item.Position, item.Sealed, item.MediaCount)).ToList();

        return new WhoSaidThisQuestion(
            message.Position, message.Sealed, message.MediaCount, earlier, options, message.Sender, message.Reactions);
    }
}
