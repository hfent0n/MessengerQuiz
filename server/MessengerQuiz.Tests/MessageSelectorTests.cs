using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Tests;

public class MessageSelectorTests
{
    private const string LongText = "This message is comfortably long enough to use.";
    private static readonly DateTime Start = new(2023, 6, 1, 12, 0, 0);

    private readonly MessageSelector _selector = Selector();

    [Fact]
    public void TakesTheMostReactedShareOfMessages()
    {
        var selector = Selector(topShare: 0.1, minPoolSize: 1);
        var messages = Enumerable.Range(0, 100).Select(index => Create(LongText, reactions: index)).ToList();

        var eligible = selector.SelectEligible(Chat(messages));

        Assert.Equal(Enumerable.Range(90, 10).Reverse(), eligible.Select(message => message.Reactions.Count));
    }

    [Fact]
    public void SmallChatsGetAtLeastTheMinimumPool()
    {
        var selector = Selector(topShare: 0.05, minPoolSize: 20);
        var messages = Enumerable.Range(0, 100).Select(index => Create(LongText, reactions: index)).ToList();

        var eligible = selector.SelectEligible(Chat(messages));

        Assert.Equal(20, eligible.Count);
        Assert.All(eligible, message => Assert.True(message.Reactions.Count >= 80));
    }

    [Fact]
    public void PoolNeverExceedsTheCandidates()
    {
        var selector = Selector(topShare: 0.05, minPoolSize: 50);

        Assert.Equal(3, selector.SelectEligible(Chat([Create(LongText), Create(LongText), Create(LongText)])).Count);
    }

    [Fact]
    public void BreaksTiesAtTheCutOffRandomly()
    {
        var selector = Selector(topShare: 0.5, minPoolSize: 1);
        var tied = Enumerable.Range(0, 10).Select(_ => Create(LongText, reactions: 1)).ToList();
        var chat = Chat([Create(LongText, reactions: 5), .. tied]);

        var chosen = Enumerable.Range(0, 200)
            .SelectMany(_ => selector.SelectEligible(chat))
            .Where(message => message.Reactions.Count == 1)
            .Select(message => message.Id)
            .ToHashSet();

        Assert.Equal(10, chosen.Count);
    }

    [Fact]
    public void ExcludesShortMessagesBeforeRanking()
    {
        var chat = Chat([Create("Too short", reactions: 50), Create(LongText, reactions: 0)]);

        Assert.Equal(LongText, Assert.Single(_selector.SelectEligible(chat)).Text);
    }

    [Theory]
    [InlineData("Ana Smith reacted ❤ to your message, and that's fine")]
    [InlineData("Ana Smith set the nickname for Ben Jones to Benny")]
    [InlineData("Ana Smith joined the video call with everybody else")]
    [InlineData("Ana Smith named the group Holiday planning crew")]
    [InlineData("Ana Smith set the quick reaction to a heart emoji")]
    [InlineData("You can now see when other people open this chat")]
    [InlineData("https://example.com/a/very/long/link/that/is/over/thirty/characters")]
    public void ExcludesSystemLinesAndLinkOnlyMessages(string text)
    {
        Assert.Empty(_selector.SelectEligible(Chat([Create(text, reactions: 3)])));
    }

    [Fact]
    public void KeepsLongMessagesThatOnlyMentionASystemPhrase()
    {
        var text = "I honestly think he missed a really big chance there. " + new string('x', 100);

        Assert.Single(_selector.SelectEligible(Chat([Create(text, reactions: 3)])));
    }

    [Fact]
    public void PhotoMessagesAreQuizzableWithoutText()
    {
        Assert.Single(_selector.SelectEligible(Chat([Create("", photos: ["photos/a.jpg"])])));
    }

    [Fact]
    public void PhotosMissingFromTheFolderAreDropped()
    {
        var chat = Chat([Create("", photos: ["photos/a.jpg", "photos/b.jpg"])]).KeepMedia(new HashSet<string> { "photos/b.jpg" });

        Assert.Equal(["photos/b.jpg"], chat.Messages.Single().Media);
    }

    [Fact]
    public void PhotoMessagesWithNoAvailablePhotosAreNotQuizzable()
    {
        var chat = Chat([Create("", photos: ["photos/a.jpg"], hasMedia: true)]).KeepMedia(new HashSet<string>());

        Assert.Empty(_selector.SelectEligible(chat));
    }

    [Fact]
    public void RanksPhotosAndTextTogether()
    {
        var selector = Selector(topShare: 0.5, minPoolSize: 1);
        var chat = Chat([
            Create("", reactions: 9, photos: ["photos/a.jpg"]),
            Create(LongText, reactions: 5),
            Create(LongText, reactions: 1),
            Create("", reactions: 0, photos: ["photos/b.jpg"]),
        ]);

        Assert.Equal([9, 5], selector.SelectEligible(chat).Select(message => message.Reactions.Count));
    }

    [Fact]
    public void ExcludesMediaMessagesWithoutText()
    {
        Assert.Empty(_selector.SelectEligible(Chat([Create("", reactions: 3)])));
    }

    [Fact]
    public void ContextIsTheSendersEarlierRunOldestFirst()
    {
        var chat = Chat([
            Create("Ben", sender: "Ben", minute: 0),
            Create("one", minute: 1),
            Create("two", minute: 2),
            Create("question", minute: 3),
        ]);

        Assert.Equal(["one", "two"], _selector.PrecedingContext(chat, 3).Select(message => message.Text));
    }

    [Fact]
    public void ContextStopsAtTheMaximum()
    {
        var selector = Selector(maxContext: 2);
        var chat = Chat([Create("one", minute: 0), Create("two", minute: 1), Create("three", minute: 2), Create("question", minute: 3)]);

        Assert.Equal(["two", "three"], selector.PrecedingContext(chat, 3).Select(message => message.Text));
    }

    [Fact]
    public void ContextGapAppliesBetweenEachPairNotTheWholeRun()
    {
        // Each message is 4 minutes after the last, so the run spans 12 minutes.
        var chat = Chat([Create("one", minute: 0), Create("two", minute: 4), Create("three", minute: 8), Create("question", minute: 12)]);

        Assert.Equal(["one", "two", "three"], _selector.PrecedingContext(chat, 3).Select(message => message.Text));
    }

    [Fact]
    public void DefaultsToThreeContextMessages()
    {
        var selector = new MessageSelector(Options.Create(new QuizOptions()));
        var chat = Chat([.. Enumerable.Range(0, 5).Select(minute => Create($"earlier {minute}", minute: minute)), Create("question", minute: 5)]);

        Assert.Equal(["earlier 2", "earlier 3", "earlier 4"], selector.PrecedingContext(chat, 5).Select(message => message.Text));
    }

    [Fact]
    public void ContextStopsAtALongGap()
    {
        var chat = Chat([Create("old", minute: 0), Create("recent", minute: 30), Create("question", minute: 32)]);

        Assert.Equal(["recent"], _selector.PrecedingContext(chat, 2).Select(message => message.Text));
    }

    [Fact]
    public void ContextStopsAtASystemLine()
    {
        var chat = Chat([
            Create("before", minute: 0),
            Create("Ana Smith named the group Weekend", minute: 1),
            Create("question", minute: 2),
        ]);

        Assert.Empty(_selector.PrecedingContext(chat, 2));
    }

    [Fact]
    public void ContextIncludesMediaMessages()
    {
        var chat = Chat([Create("", minute: 0, hasMedia: true), Create("question", minute: 1)]);

        Assert.True(Assert.Single(_selector.PrecedingContext(chat, 1)).HasMedia);
    }

    [Fact]
    public void NoContextWithoutTimestamps()
    {
        var chat = Chat([Create("one"), Create("question")]);

        Assert.Empty(_selector.PrecedingContext(chat, 1));
    }

    [Fact]
    public void PickBalancedGivesQuietSendersAFairShare()
    {
        var candidates = Enumerable.Range(0, 99).Select(_ => Create(LongText, sender: "Loud"))
            .Append(Create(LongText, sender: "Quiet"))
            .ToList();
        var random = new Random(1);

        var quietPicks = Enumerable.Range(0, 1000)
            .Count(_ => MessageSelector.PickBalanced(candidates, random)!.Sender == "Quiet");

        Assert.InRange(quietPicks, 400, 600);
    }

    [Fact]
    public void PickBalancedReturnsNullWhenThereAreNoCandidates()
    {
        Assert.Null(MessageSelector.PickBalanced([], new Random(1)));
    }

    private static MessageSelector Selector(double topShare = 0.05, int minPoolSize = 50, int maxContext = 3)
    {
        return new MessageSelector(Options.Create(new QuizOptions
        {
            MinMessageLength = 30,
            TopReactionShare = topShare,
            MinPoolSize = minPoolSize,
            MaxContextMessages = maxContext,
            ContextGap = TimeSpan.FromMinutes(5),
        }));
    }

    private static Chat Chat(IReadOnlyList<Message> messages) => new("Test", ["Ana", "Ben"], messages);

    private static Message Create(
        string text,
        int reactions = 0,
        string sender = "Ana",
        int? minute = null,
        bool hasMedia = false,
        IReadOnlyList<string>? photos = null)
    {
        var reactionList = Enumerable.Range(0, reactions).Select(_ => new Reaction("😆", "Ben")).ToList();
        return new Message(Guid.NewGuid().ToString(), sender, text, minute is { } m ? Start.AddMinutes(m) : null, reactionList, hasMedia)
        {
            Media = photos ?? [],
        };
    }
}
