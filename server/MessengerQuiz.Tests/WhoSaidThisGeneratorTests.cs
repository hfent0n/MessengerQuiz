using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Storage;

namespace MessengerQuiz.Tests;

public class WhoSaidThisGeneratorTests
{
    private readonly WhoSaidThisGenerator _generator = new();

    [Fact]
    public void AnswerIsAlwaysAmongDistinctOptions()
    {
        var session = Session(["Ana", "Ben", "Cal", "Dee", "Eve", "Fay"], messageCount: 60);

        for (var index = 0; index < 60; index++)
        {
            var question = _generator.Next(session)!;

            Assert.Contains(question.Answer, question.Options);
            Assert.Equal(4, question.Options.Count);
            Assert.Equal(4, question.Options.Distinct().Count());
        }
    }

    [Fact]
    public void TwoPersonChatsGetTwoOptions()
    {
        var question = _generator.Next(Session(["Ana", "Ben"], messageCount: 2))!;

        Assert.Equal(2, question.Options.Count);
    }

    [Fact]
    public void AnswerIsNotAlwaysInTheSamePosition()
    {
        var session = Session(["Ana", "Ben", "Cal", "Dee"], messageCount: 200);

        var positions = Enumerable.Range(0, 200)
            .Select(_ => _generator.Next(session)!)
            .Select(question => question.Options.ToList().IndexOf(question.Answer))
            .Distinct();

        Assert.Equal(4, positions.Count());
    }

    [Fact]
    public void ReturnsNullOnceEveryMessageHasBeenAsked()
    {
        var session = Session(["Ana", "Ben"], messageCount: 2);

        Assert.NotNull(_generator.Next(session));
        Assert.NotNull(_generator.Next(session));
        Assert.Null(_generator.Next(session));
    }

    [Fact]
    public async Task ParallelRequestsNeverRepeatAMessage()
    {
        const int messageCount = 2000;
        var session = Session(["Ana", "Ben", "Cal"], messageCount);

        var tasks = Enumerable.Range(0, messageCount)
            .Select(_ => Task.Run(() => _generator.Next(session)!.MessageId));
        var ids = await Task.WhenAll(tasks);

        Assert.Equal(messageCount, ids.Distinct().Count());
    }

    private static ChatSession Session(IReadOnlyList<string> participants, int messageCount)
    {
        var messages = Enumerable.Range(0, messageCount)
            .Select(index => new Message(
                $"m_{index}", participants[index % participants.Count], "A message", null, [], false))
            .ToList();

        return new ChatSession(new Chat("Test", participants, messages), messages);
    }
}
