using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Parsing;

namespace MessengerQuiz.Tests;

public class HtmlChatParserTests
{
    private readonly HtmlChatParser _parser = new();

    [Fact]
    public async Task ParsesSenderTextAndTimestamp()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Message("Ana", "Hello there")));

        var message = Assert.Single(chat.Messages);
        Assert.Equal("Friends", chat.Title);
        Assert.Equal("Ana", message.Sender);
        Assert.Equal("Hello there", message.Text);
        Assert.Equal(new DateTime(2023, 12, 12, 21, 5, 3), message.SentAt);
        Assert.False(message.HasMedia);
    }

    [Fact]
    public async Task KeepsLineBreaksAndDecodesEntities()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Message("Ana", "It's <fine>\n& good")));

        Assert.Equal("It's <fine>\n& good", chat.Messages.Single().Text);
    }

    [Fact]
    public async Task SplitsReactionsIncludingMultiCodePointEmoji()
    {
        var chat = await Parse(FakeExport.Page(
            "Friends",
            FakeExport.Message("Ana", "Hello", reactions: ["😆Ben Smith", "👍🏽Cal Jones"])));

        Assert.Equal(
            [new Reaction("😆", "Ben Smith"), new Reaction("👍🏽", "Cal Jones")],
            chat.Messages.Single().Reactions);
    }

    [Fact]
    public async Task ReactionTextIsNotPartOfMessageText()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Message("Ana", "Hello", reactions: "😆Ben")));

        Assert.Equal("Hello", chat.Messages.Single().Text);
    }

    [Fact]
    public async Task MediaMessagesHaveNoTextAndKeepTheirReactions()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Photo("Ana", "😆Ben")));

        var message = Assert.Single(chat.Messages);
        Assert.Equal("", message.Text);
        Assert.True(message.HasMedia);
        Assert.Single(message.Reactions);
    }

    [Fact]
    public async Task RecordsPhotoPathsRelativeToTheChatFolder()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.PhotoAt("Ana", FakeExport.DefaultTimestamp, "123.jpg")));

        Assert.Equal(["photos/123.jpg"], chat.Messages.Single().Photos);
    }

    [Theory]
    [InlineData(FakeExport.ChatFolder + "/gifs/a.gif")]
    [InlineData(FakeExport.ChatFolder + "/videos/thumbnail.jpg")]
    [InlineData("your_facebook_activity/messages/stickers_used/s.png")]
    public async Task OtherMediaAreNotPhotos(string source)
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Media("Ana", source, FakeExport.DefaultTimestamp)));

        var message = chat.Messages.Single();
        Assert.Empty(message.Photos);
        Assert.True(message.HasMedia);
    }

    [Fact]
    public async Task SkipsHeaderAndGroupPhotoBlocks()
    {
        var chat = await Parse(FakeExport.Page(
            "Friends",
            FakeExport.GroupPhoto("Group photo"),
            FakeExport.Message("Ana", "Hello")));

        Assert.Equal(["Ana"], chat.Participants);
        Assert.Single(chat.Messages);
    }

    [Theory]
    [InlineData("Dec 12, 2023 9:05:03pm")]
    [InlineData("Dec 12, 2023 9:05:03 pm")]
    [InlineData("Dec 12, 2023 9:05:03 PM")]
    public async Task ParsesOldAndNewTimestampFormats(string timestamp)
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Message("Ana", "Hello", timestamp)));

        Assert.Equal(new DateTime(2023, 12, 12, 21, 5, 3), chat.Messages.Single().SentAt);
    }

    [Fact]
    public async Task UnreadableTimestampBecomesNull()
    {
        var chat = await Parse(FakeExport.Page("Friends", FakeExport.Message("Ana", "Hello", "12 décembre 2023")));

        Assert.Null(chat.Messages.Single().SentAt);
    }

    [Fact]
    public async Task MergesPagesOldestFirstWithUniqueIds()
    {
        var newest = FakeExport.Page("Friends", FakeExport.Message("Ana", "newest", "Dec 12, 2023 9:05:03pm"));
        var oldest = FakeExport.Page("Friends", FakeExport.Message("Ben", "oldest", "Jan 2, 2023 1:00:00am"));

        var chat = await Parse(("chat/message_1.html", newest), ("chat/message_2.html", oldest));

        Assert.Equal(["oldest", "newest"], chat.Messages.Select(message => message.Text));
        Assert.Equal(2, chat.Messages.Select(message => message.Id).Distinct().Count());
    }

    [Fact]
    public async Task KeepsSameSecondMessagesInSendingOrder()
    {
        // The export lists messages newest first.
        var chat = await Parse(FakeExport.Page(
            "Friends",
            FakeExport.Message("Ana", "second"),
            FakeExport.Message("Ana", "first")));

        Assert.Equal(["first", "second"], chat.Messages.Select(message => message.Text));
    }

    [Fact]
    public async Task OrdersParticipantsByMessageCount()
    {
        var chat = await Parse(FakeExport.Page(
            "Friends",
            FakeExport.Message("Ana", "one"),
            FakeExport.Message("Ben", "two"),
            FakeExport.Message("Ben", "three")));

        Assert.Equal(["Ben", "Ana"], chat.Participants);
    }

    [Fact]
    public async Task RejectsNoFiles()
    {
        await Assert.ThrowsAsync<ChatParseException>(() => _parser.ParseAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task RejectsPagesThatArentMessengerChats()
    {
        await Assert.ThrowsAsync<ChatParseException>(() => Parse("<html><body>Hello</body></html>"));
    }

    [Fact]
    public async Task RejectsDifferentTitles()
    {
        await Assert.ThrowsAsync<ChatParseException>(() => Parse(
            ("message_1.html", FakeExport.Page("One", FakeExport.Message("Ana", "Hello"))),
            ("message_2.html", FakeExport.Page("Two", FakeExport.Message("Ana", "Hello")))));
    }

    [Fact]
    public async Task RejectsDifferentFoldersEvenWithTheSameTitle()
    {
        var page = FakeExport.Page("Ana", FakeExport.Message("Ana", "Hello"));

        await Assert.ThrowsAsync<ChatParseException>(() => Parse(
            ("ana_1/message_1.html", page),
            ("ana_2/message_1.html", page)));
    }

    [Fact]
    public async Task RejectsTheSameFileTwice()
    {
        var page = FakeExport.Page("Ana", FakeExport.Message("Ana", "Hello"));

        await Assert.ThrowsAsync<ChatParseException>(() => Parse(
            ("chat/message_1.html", page),
            ("chat/message_1.html", page)));
    }

    private Task<Chat> Parse(string html) => Parse(("message_1.html", html));

    private Task<Chat> Parse(params (string FileName, string Html)[] pages)
    {
        var chatPages = pages.Select(page => new ChatPage(page.FileName, FakeExport.Stream(page.Html))).ToList();
        return _parser.ParseAsync(chatPages, CancellationToken.None);
    }
}
