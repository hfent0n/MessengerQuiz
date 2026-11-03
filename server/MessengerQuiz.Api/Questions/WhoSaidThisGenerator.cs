using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

public sealed record WhoSaidThisQuestion(
    string MessageId,
    string Text,
    IReadOnlyList<ContextMessage> Context,
    IReadOnlyList<string> Options,
    string Answer,
    int Reactions);

public sealed record ContextMessage(string Text, bool HasMedia);

public sealed class WhoSaidThisGenerator(MessageSelector selector) : IQuestionGenerator<WhoSaidThisQuestion>
{
    private const int MaxOptions = 4;

    public WhoSaidThisQuestion? Next(ChatSession session)
    {
        var item = session.TakeNext(
            candidates => MessageSelector.PickBalanced(candidates, Random.Shared),
            message => selector.PrecedingContext(session.Chat, session.PositionOf(message)));
        if (item is null)
        {
            return null;
        }

        var message = item.Message;
        var others = session.Chat.Participants
            .Where(participant => participant != message.Sender)
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaxOptions - 1);

        var options = others.Append(message.Sender).OrderBy(_ => Random.Shared.Next()).ToList();
        var context = item.Context.Select(earlier => new ContextMessage(earlier.Text, earlier.HasMedia)).ToList();

        return new WhoSaidThisQuestion(message.Id, message.Text, context, options, message.Sender, message.Reactions.Count);
    }
}
