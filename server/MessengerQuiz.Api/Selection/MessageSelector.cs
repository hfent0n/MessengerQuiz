using MessengerQuiz.Api.Domain;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Api.Selection;

public sealed class MessageSelector(IOptions<QuizOptions> options)
{
    private readonly QuizOptions _options = options.Value;

    // Media and text messages are ranked together. The browser decides which messages can be questions.
    // The most-reacted share of quizzable messages, with at least MinPoolSize so small chats still get a quiz.
    public IReadOnlyList<Message> SelectEligible(Chat chat)
    {
        var candidates = chat.Messages.Where(message => message.Quizzable).ToList();
        var topShare = (int)Math.Ceiling(candidates.Count * _options.TopReactionShare);
        var poolSize = Math.Min(candidates.Count, Math.Max(topShare, _options.MinPoolSize));

        // Many messages share a reaction count, so ties at the cut-off are broken randomly.
        return candidates
            .OrderByDescending(message => message.Reactions)
            .ThenBy(_ => Random.Shared.Next())
            .Take(poolSize)
            .ToList();
    }

    // Any message that can be a question, whatever its reactions, as long as it ends its run: the next message is
    // from someone else, is a system line, or comes after a long gap. Its context then shows the run that led to it.
    public IReadOnlyList<Message> SelectRandomPool(Chat chat) =>
        chat.Messages.Where((message, index) => message.Quizzable && EndsItsRun(chat, index)).ToList();

    public bool EndsItsRun(Chat chat, int index)
    {
        if (index + 1 >= chat.Messages.Count)
        {
            return true;
        }

        var next = chat.Messages[index + 1];
        return next.Sender != chat.Messages[index].Sender || next.System || !WithinContextGap(chat.Messages[index], next);
    }

    // Earlier messages in the same sender's run, oldest first, in case the question needs them to make sense.
    public IReadOnlyList<Message> PrecedingContext(Chat chat, int index)
    {
        var context = new List<Message>();
        var later = chat.Messages[index];

        for (var position = index - 1; position >= 0 && context.Count < _options.MaxContextMessages; position--)
        {
            var earlier = chat.Messages[position];
            if (earlier.Sender != later.Sender || earlier.System || !WithinContextGap(earlier, later))
            {
                break;
            }

            context.Add(earlier);
            later = earlier;
        }

        context.Reverse();
        return context;
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

    private bool WithinContextGap(Message earlier, Message later) =>
        earlier.SentAt is { } earlierTime
        && later.SentAt is { } laterTime
        && laterTime - earlierTime <= _options.ContextGap;
}
