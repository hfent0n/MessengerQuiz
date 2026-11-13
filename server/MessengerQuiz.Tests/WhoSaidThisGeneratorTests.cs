using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Tests.TestChats;

namespace MessengerQuiz.Tests;

public class WhoSaidThisGeneratorTests
{
    private readonly WhoSaidThisGenerator _generator = new(new MessageSelector(Options.Create(new QuizOptions())));

    [Fact]
    public void AnswerIsAlwaysAmongDistinctOptions()
    {
        var session = Session(people: 6, messageCount: 60);

        for (var index = 0; index < 60; index++)
        {
            var question = _generator.Next(session)!;

            Assert.Contains(question.Answer, question.Options);
            Assert.Equal(4, question.Options.Count);
            Assert.Equal(4, question.Options.Distinct().Count());
            Assert.All(question.Options, option => Assert.InRange(option, 0, 5));
        }
    }

    [Fact]
    public void TwoPersonChatsGetTwoOptions()
    {
        Assert.Equal(2, _generator.Next(Session(people: 2, messageCount: 2))!.Options.Count);
    }

    [Fact]
    public void AnswerIsNotAlwaysInTheSamePosition()
    {
        var session = Session(people: 4, messageCount: 200);

        var positions = Enumerable.Range(0, 200)
            .Select(_ => _generator.Next(session)!)
            .Select(question => question.Options.ToList().IndexOf(question.Answer))
            .Distinct();

        Assert.Equal(4, positions.Count());
    }

    [Fact]
    public void ReturnsNullOnceEveryMessageHasBeenAsked()
    {
        var session = Session(people: 2, messageCount: 2);

        Assert.NotNull(_generator.Next(session));
        Assert.NotNull(_generator.Next(session));
        Assert.Null(_generator.Next(session));
    }

    [Fact]
    public async Task ParallelRequestsNeverRepeatAMessage()
    {
        const int messageCount = 2000;
        var session = Session(people: 3, messageCount);

        var positions = await Task.WhenAll(Enumerable.Range(0, messageCount)
            .Select(_ => Task.Run(() => _generator.Next(session)!.Position)));

        Assert.Equal(messageCount, positions.Distinct().Count());
    }

    [Fact]
    public void ReturnsTheSealedBoxesForTheBrowserToOpen()
    {
        var chat = Chat(2,
            Message(sender: 1, minute: 0, label: "unrelated"),
            Message(sender: 0, minute: 1, label: "guess what"),
            Message(sender: 0, minute: 2, media: 1, quizzable: false, label: "a photo"),
            Message(sender: 0, minute: 3, reactions: 4, label: "that was me"));
        var session = new ChatSession(chat, [chat.Messages[3]]);

        var question = _generator.Next(session)!;

        Assert.Equal((3, "that was me", 0, 4), (question.Position, Label(question.Sealed), question.Answer, question.Reactions));
        Assert.Equal([(1, "guess what", 0), (2, "a photo", 1)],
            question.Context.Select(context => (context.Position, Label(context.Sealed), context.MediaCount)));
    }

    [Fact]
    public void ContextMessagesAreNeverAskedLater()
    {
        var chat = Chat(2, [.. Enumerable.Range(0, 4).Select(minute => Message(sender: 0, minute: minute)), Message(sender: 1, minute: 5)]);
        var session = new ChatSession(chat, chat.Messages);
        var seen = new List<int>();

        while (_generator.Next(session) is { } question)
        {
            Assert.DoesNotContain(question.Position, seen);
            seen.Add(question.Position);
            seen.AddRange(question.Context.Select(context => context.Position));
        }

        // Context can repeat across questions. Every message is still either asked or shown.
        Assert.Equal(Enumerable.Range(0, 5).ToHashSet(), seen.ToHashSet());
    }

    [Fact]
    public void MediaQuestionsSayHowManyItemsTheyHave()
    {
        var chat = Chat(2, Message(sender: 0, media: 2), Message(sender: 1));

        Assert.Equal(2, _generator.Next(new ChatSession(chat, [chat.Messages[0]]))!.MediaCount);
    }

    private static ChatSession Session(int people, int messageCount)
    {
        var chat = Chat(people, Enumerable.Range(0, messageCount).Select(index => Message(sender: index % people)));
        return new ChatSession(chat, chat.Messages);
    }
}
