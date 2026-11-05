using MessengerQuiz.Api;
using MessengerQuiz.Api.Demo;
using MessengerQuiz.Api.Selection;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Tests;

public class DemoChatTests
{
    private readonly DemoChat _demo = new();
    private readonly MessageSelector _selector = new(Options.Create(new QuizOptions()));

    [Fact]
    public void HasFourFlatmates()
    {
        Assert.Equal(["Jess", "Priya", "Sam", "Tom"], _demo.Chat.Participants.Order());
    }

    [Fact]
    public void MessagesAreInTimeOrder()
    {
        var times = _demo.Chat.Messages.Select(message => message.SentAt!.Value).ToList();

        Assert.Equal(times.Order(), times);
    }

    [Fact]
    public void HasEnoughQuestionsFromEveryone()
    {
        var eligible = _selector.SelectEligible(_demo.Chat);

        Assert.True(eligible.Count >= 50);
        Assert.Equal(_demo.Chat.Participants.Order(), eligible.Select(message => message.Sender).Distinct().Order());
    }

    [Fact]
    public void NoMessageLooksLikeASystemLine()
    {
        Assert.DoesNotContain(_demo.Chat.Messages, message => SystemMessages.IsSystemLine(message.Text));
    }

    [Fact]
    public void ReactionsComeFromOtherFlatmates()
    {
        Assert.All(_demo.Chat.Messages.SelectMany(message => message.Reactions, (message, reaction) => (message, reaction)), pair =>
        {
            Assert.Contains(pair.reaction.Reactor, _demo.Chat.Participants);
            Assert.NotEqual(pair.message.Sender, pair.reaction.Reactor);
        });
    }

    [Fact]
    public void SomeQuestionsHaveContext()
    {
        var withContext = _demo.Chat.Messages
            .Select((message, index) => _selector.PrecedingContext(_demo.Chat, index))
            .Count(context => context.Count > 0);

        Assert.True(withContext >= 5);
    }

    [Fact]
    public void PhotosAreInThePhotosFolder()
    {
        var media = _demo.Chat.Messages.SelectMany(message => message.Media).ToList();

        Assert.Equal(["photos/lasagne.svg", "photos/pickle.svg", "photos/quiz-trophy.svg"], media.Order());
    }
}
