using MessengerQuiz.Api.Domain;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Api.Selection;

public sealed class MessageSelector(IOptions<QuizOptions> options)
{
    private readonly QuizOptions _options = options.Value;

    public IReadOnlyList<Message> SelectEligible(Chat chat)
    {
        var longEnough = chat.Messages
            .Where(message => message.Text.Length >= _options.MinMessageLength)
            .Where(message => !SystemMessages.IsSystemLine(message.Text) && !SystemMessages.IsLinkOnly(message.Text))
            .ToList();

        var reacted = longEnough.Where(message => message.Reactions.Count >= _options.MinReactions).ToList();

        return reacted.Count >= _options.MinPoolSize ? reacted : longEnough;
    }

    // Picks the sender first, so the most talkative person isn't nearly always the answer.
    public static Message? PickBalanced(IReadOnlyList<Message> candidates, Random random)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var bySender = candidates.GroupBy(message => message.Sender).ToList();
        var messages = bySender[random.Next(bySender.Count)].ToList();
        return messages[random.Next(messages.Count)];
    }
}
