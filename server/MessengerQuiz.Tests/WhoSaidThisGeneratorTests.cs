using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Tests;

public class WhoSaidThisGeneratorTests
{
    private readonly WhoSaidThisGenerator _generator = new(new MessageSelector(Options.Create(new QuizOptions())));

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

    [Fact]
    public void IncludesEarlierMessagesFromTheSameSenderAsContext()
    {
        var start = new DateTime(2023, 6, 1, 12, 0, 0);
        var messages = new[]
        {
            new Message("m_0", "Ben", "Unrelated", start, [], false),
            new Message("m_1", "Ana", "Guess what", start.AddMinutes(1), [], false),
            new Message("m_2", "Ana", "", start.AddMinutes(2), [], true) { Photos = ["photos/a.jpg"] },
            new Message("m_3", "Ana", "That was me", start.AddMinutes(3), [], false),
        };
        var session = new ChatSession(new Chat("Test", ["Ana", "Ben"], messages), [messages[3]]);

        var question = _generator.Next(session)!;

        Assert.Equal(
            [("Guess what", false, ""), ("", true, "photos/a.jpg")],
            question.Context.Select(context => (context.Text, context.HasMedia, string.Join(",", context.Photos))));
    }

    [Fact]
    public void ContextMessagesAreNeverAskedLater()
    {
        var start = new DateTime(2023, 6, 1, 12, 0, 0);
        var messages = Enumerable.Range(0, 4)
            .Select(index => new Message($"m_{index}", "Ana", $"Message {index}", start.AddMinutes(index), [], false))
            .Append(new Message("m_4", "Ben", "Reply", start.AddMinutes(5), [], false))
            .ToList();
        var session = new ChatSession(new Chat("Test", ["Ana", "Ben"], messages), messages);
        var seen = new List<string>();

        while (_generator.Next(session) is { } question)
        {
            Assert.DoesNotContain(question.MessageId, seen);
            seen.Add(question.MessageId);
            seen.AddRange(question.Context.Select(context => messages.Single(message => message.Text == context.Text).Id));
        }

        // Context can repeat across questions. Every message is still either asked or shown.
        Assert.Equal(messages.Select(message => message.Id).ToHashSet(), seen.ToHashSet());
    }

    [Fact]
    public void PhotoQuestionsIncludeTheirPhotos()
    {
        var messages = new[]
        {
            new Message("m_0", "Ana", "", null, [], true) { Photos = ["photos/a.jpg", "photos/b.jpg"] },
            new Message("m_1", "Ben", "Unrelated", null, [], false),
        };
        var session = new ChatSession(new Chat("Test", ["Ana", "Ben"], messages), [messages[0]]);

        var question = _generator.Next(session)!;

        Assert.Equal(["photos/a.jpg", "photos/b.jpg"], question.Photos);
        Assert.Equal("", question.Text);
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
