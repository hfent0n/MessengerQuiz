using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Api.Questions;

public sealed record WhoSaidThisQuestion(
    string MessageId,
    string Text,
    IReadOnlyList<string> Media,
    IReadOnlyList<ContextMessage> Context,
    IReadOnlyList<string> Options,
    string Answer,
    int Reactions);

// Media are paths inside the chat folder. The browser shows them from the player's own files.
public sealed record ContextMessage(string Text, bool HasMedia, IReadOnlyList<string> Media);

public sealed class WhoSaidThisGenerator(MessageSelector selector) : IQuestionGenerator<WhoSaidThisQuestion>
{
    private const int MaxOptions = 4;

    public WhoSaidThisQuestion? Next(ChatSession session)
    {
        var item = session.TakeNext(
            candidates => MessageSelector.PickBalanced(candidates, Random.Shared),
            message => selector.PrecedingContext(session.Chat, session.PositionOf(message)));
        return item is null ? null : Create(session.Chat, item.Message, item.Context);
    }

    // Builds the question for a message that has already been chosen, with up to 4 shuffled options.
    public static WhoSaidThisQuestion Create(Chat chat, Message message, IReadOnlyList<Message> context)
    {
        var others = chat.Participants
            .Where(participant => participant != message.Sender)
            .OrderBy(_ => Random.Shared.Next())
            .Take(MaxOptions - 1);

        var options = others.Append(message.Sender).OrderBy(_ => Random.Shared.Next()).ToList();
        var earlier = context
            .Select(item => new ContextMessage(item.Text, item.HasMedia, item.Media))
            .ToList();

        return new WhoSaidThisQuestion(
            message.Id, message.Text, message.Media, earlier, options, message.Sender, message.Reactions.Count);
    }
}
