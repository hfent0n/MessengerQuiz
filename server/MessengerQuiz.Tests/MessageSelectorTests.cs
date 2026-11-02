using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Tests;

public class MessageSelectorTests
{
    private const string LongText = "This message is comfortably long enough to use.";

    private readonly MessageSelector _selector = new(Options.Create(new QuizOptions
    {
        MinMessageLength = 30,
        MinReactions = 1,
        MinPoolSize = 3,
    }));

    [Fact]
    public void RequiresLengthAndReactionsWhenThePoolIsBigEnough()
    {
        var keep = Enumerable.Range(0, 3).Select(index => Create(LongText + index, reactions: 1));
        var chat = Chat([.. keep, Create(LongText, reactions: 0), Create("Too short", reactions: 5)]);

        var eligible = _selector.SelectEligible(chat);

        Assert.Equal(3, eligible.Count);
        Assert.All(eligible, message => Assert.NotEmpty(message.Reactions));
    }

    [Fact]
    public void DropsTheReactionRequirementWhenThePoolIsTooSmall()
    {
        var chat = Chat([Create(LongText, reactions: 1), Create(LongText, reactions: 0), Create("Too short", reactions: 5)]);

        Assert.Equal(2, _selector.SelectEligible(chat).Count);
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
    public void ExcludesMediaMessagesWithoutText()
    {
        Assert.Empty(_selector.SelectEligible(Chat([Create("", reactions: 3)])));
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

    private static Chat Chat(IReadOnlyList<Message> messages) => new("Test", ["Ana"], messages);

    private static Message Create(string text, int reactions = 0, string sender = "Ana")
    {
        var reactionList = Enumerable.Range(0, reactions).Select(_ => new Reaction("😆", "Ben")).ToList();
        return new Message(Guid.NewGuid().ToString(), sender, text, null, reactionList, false);
    }
}
