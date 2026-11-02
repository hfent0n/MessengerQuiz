using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

public sealed record WhoSaidThisQuestion(
    string MessageId,
    string Text,
    IReadOnlyList<string> Options,
    string Answer,
    int Reactions);

public sealed class WhoSaidThisGenerator : IQuestionGenerator<WhoSaidThisQuestion>
{
    private const int MaxOptions = 4;

    public WhoSaidThisQuestion? Next(ChatSession session)
    {
        var message = session.TakeNext(candidates => MessageSelector.PickBalanced(candidates, Random.Shared));
        if (message is null)
        {
            return null;
        }

        var others = session.Chat.Participants
            .Where(participant => participant != message.Sender)
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaxOptions - 1);

        var options = others.Append(message.Sender).OrderBy(_ => Random.Shared.Next()).ToList();

        return new WhoSaidThisQuestion(message.Id, message.Text, options, message.Sender, message.Reactions.Count);
    }
}
