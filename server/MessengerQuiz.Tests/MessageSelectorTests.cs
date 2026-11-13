using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Tests.TestChats;

namespace MessengerQuiz.Tests;

// Which messages are long enough or are system lines is decided in the browser, and tested there.
public class MessageSelectorTests
{
    private readonly MessageSelector _selector = Selector();

    [Fact]
    public void TakesTheMostReactedShareOfMessages()
    {
        var selector = Selector(topShare: 0.1, minPoolSize: 1);
        var chat = Chat(2, Enumerable.Range(0, 100).Select(index => Message(reactions: index)));

        var eligible = selector.SelectEligible(chat);

        Assert.Equal(Enumerable.Range(90, 10).Reverse(), eligible.Select(message => message.Reactions));
    }

    [Fact]
    public void SmallChatsGetAtLeastTheMinimumPool()
    {
        var selector = Selector(topShare: 0.05, minPoolSize: 20);
        var chat = Chat(2, Enumerable.Range(0, 100).Select(index => Message(reactions: index)));

        var eligible = selector.SelectEligible(chat);

        Assert.Equal(20, eligible.Count);
        Assert.All(eligible, message => Assert.True(message.Reactions >= 80));
    }

    [Fact]
    public void PoolNeverExceedsTheCandidates()
    {
        var selector = Selector(topShare: 0.05, minPoolSize: 50);

        Assert.Equal(3, selector.SelectEligible(Chat(2, Message(), Message(), Message())).Count);
    }

    [Fact]
    public void BreaksTiesAtTheCutOffRandomly()
    {
        var selector = Selector(topShare: 0.5, minPoolSize: 1);
        var chat = Chat(2, [Message(reactions: 5), .. Enumerable.Range(0, 10).Select(_ => Message(reactions: 1))]);

        var chosen = Enumerable.Range(0, 200)
            .SelectMany(_ => selector.SelectEligible(chat))
            .Where(message => message.Reactions == 1)
            .Select(message => message.Position)
            .ToHashSet();

        Assert.Equal(10, chosen.Count);
    }

    [Fact]
    public void OnlyMessagesTheBrowserMarkedQuizzableAreUsed()
    {
        var chat = Chat(2, Message(reactions: 50, quizzable: false, label: "short"), Message(reactions: 0, label: "long"));

        Assert.Equal("long", Label(Assert.Single(_selector.SelectEligible(chat)).Sealed));
    }

    [Fact]
    public void RanksMediaAndTextTogether()
    {
        var selector = Selector(topShare: 0.5, minPoolSize: 1);
        var chat = Chat(2,
            Message(reactions: 9, media: 1),
            Message(reactions: 5),
            Message(reactions: 1),
            Message(reactions: 0, media: 1));

        Assert.Equal([9, 5], selector.SelectEligible(chat).Select(message => message.Reactions));
    }

    [Fact]
    public void ContextIsTheSendersEarlierRunOldestFirst()
    {
        var chat = Chat(2,
            Message(sender: 1, minute: 0, label: "Ben"),
            Message(minute: 1, label: "one"),
            Message(minute: 2, label: "two"),
            Message(minute: 3, label: "question"));

        Assert.Equal(["one", "two"], Labels(_selector.PrecedingContext(chat, 3)));
    }

    [Fact]
    public void ContextStopsAtTheMaximum()
    {
        var selector = Selector(maxContext: 2);
        var chat = Chat(2, Message(minute: 0, label: "one"), Message(minute: 1, label: "two"), Message(minute: 2, label: "three"), Message(minute: 3));

        Assert.Equal(["two", "three"], Labels(selector.PrecedingContext(chat, 3)));
    }

    [Fact]
    public void ContextGapAppliesBetweenEachPairNotTheWholeRun()
    {
        // Each message is 4 minutes after the last, so the run spans 12 minutes.
        var chat = Chat(2, Message(minute: 0, label: "one"), Message(minute: 4, label: "two"), Message(minute: 8, label: "three"), Message(minute: 12));

        Assert.Equal(["one", "two", "three"], Labels(_selector.PrecedingContext(chat, 3)));
    }

    [Fact]
    public void DefaultsToThreeContextMessages()
    {
        var selector = new MessageSelector(Options.Create(new QuizOptions()));
        var chat = Chat(2, [.. Enumerable.Range(0, 5).Select(minute => Message(minute: minute, label: $"earlier {minute}")), Message(minute: 5)]);

        Assert.Equal(["earlier 2", "earlier 3", "earlier 4"], Labels(selector.PrecedingContext(chat, 5)));
    }

    [Fact]
    public void ContextStopsAtALongGap()
    {
        var chat = Chat(2, Message(minute: 0, label: "old"), Message(minute: 30, label: "recent"), Message(minute: 32));

        Assert.Equal(["recent"], Labels(_selector.PrecedingContext(chat, 2)));
    }

    [Fact]
    public void ContextStopsAtASystemLine()
    {
        var chat = Chat(2, Message(minute: 0), Message(minute: 1, quizzable: false, system: true), Message(minute: 2));

        Assert.Empty(_selector.PrecedingContext(chat, 2));
    }

    [Fact]
    public void ContextIncludesMessagesThatCantBeQuestions()
    {
        var chat = Chat(2, Message(minute: 0, quizzable: false, label: "sticker"), Message(minute: 1));

        Assert.Equal(["sticker"], Labels(_selector.PrecedingContext(chat, 1)));
    }

    [Fact]
    public void NoContextWithoutTimestamps()
    {
        Assert.Empty(_selector.PrecedingContext(Chat(2, Message(), Message()), 1));
    }

    [Fact]
    public void PickBalancedGivesQuietSendersAFairShare()
    {
        var candidates = Enumerable.Range(0, 99).Select(_ => Message(sender: 0)).Append(Message(sender: 1)).ToList();
        var random = new Random(1);

        var quietPicks = Enumerable.Range(0, 1000).Count(_ => MessageSelector.PickBalanced(candidates, random)!.Sender == 1);

        Assert.InRange(quietPicks, 400, 600);
    }

    [Fact]
    public void PickBalancedReturnsNullWhenThereAreNoCandidates()
    {
        Assert.Null(MessageSelector.PickBalanced([], new Random(1)));
    }

    private static IEnumerable<string> Labels(IEnumerable<Message> messages) => messages.Select(message => Label(message.Sealed));

    private static MessageSelector Selector(double topShare = 0.05, int minPoolSize = 50, int maxContext = 3) =>
        new(Options.Create(new QuizOptions
        {
            TopReactionShare = topShare,
            MinPoolSize = minPoolSize,
            MaxContextMessages = maxContext,
            ContextGap = TimeSpan.FromMinutes(5),
        }));
}
