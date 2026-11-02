using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MessengerQuiz.Api.Questions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MessengerQuiz.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string QuestionUrl = "/api/questions/who-said-this";

    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
        builder.UseSetting("Quiz:UploadsPerMinutePerIp", "1000"));

    [Fact]
    public async Task UploadThenQuestionReturnsAMessageFromTheChat()
    {
        var client = _factory.CreateClient();

        var chat = await Upload(client, FakeExport.QuizzablePage("Friends"));
        var question = await GetQuestion(client, chat.ChatId);

        Assert.Equal("Friends", chat.Title);
        Assert.Equal(18, chat.MessageCount);
        Assert.Contains(question.Answer, question.Options);
        Assert.StartsWith(question.Answer, question.Text);
    }

    [Fact]
    public async Task OneChatIdNeverReachesAnotherChat()
    {
        var client = _factory.CreateClient();
        var first = await Upload(client, FakeExport.QuizzablePage("First"));
        var second = await Upload(client, FakeExport.Page("Second", OtherPeoplesMessages()));

        var firstQuestion = await GetQuestion(client, first.ChatId);
        var secondQuestion = await GetQuestion(client, second.ChatId);

        Assert.DoesNotContain("Zed", firstQuestion.Options);
        Assert.DoesNotContain("Ana", secondQuestion.Options);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-chat")]
    public async Task MissingOrUnknownChatIdIsRejected(string? chatId)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, QuestionUrl);
        if (chatId is not null)
        {
            request.Headers.Add("X-Chat-Id", chatId);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsNoContentWhenQuestionsRunOut()
    {
        var client = _factory.CreateClient();
        var chat = await Upload(client, FakeExport.QuizzablePage());

        for (var index = 0; index < chat.EligibleCount; index++)
        {
            await GetQuestion(client, chat.ChatId);
        }

        var response = await SendQuestionRequest(client, chat.ChatId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWipesTheChat()
    {
        var client = _factory.CreateClient();
        var chat = await Upload(client, FakeExport.QuizzablePage());
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chats/current");
        request.Headers.Add("X-Chat-Id", chat.ChatId);

        var deleted = await client.SendAsync(request);
        var question = await SendQuestionRequest(client, chat.ChatId);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, question.StatusCode);
    }

    [Fact]
    public async Task RejectsANonMessengerFile()
    {
        var response = await Post(_factory.CreateClient(), "<html><body>Hello</body></html>");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsARequestWithNoFiles()
    {
        var response = await _factory.CreateClient().PostAsync("/api/chats", new MultipartFormDataContent
        {
            { new StringContent("value"), "field" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsANonFormRequest()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/chats", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAChatWithOnlyOneSender()
    {
        var messages = Enumerable.Range(0, 20)
            .Select(index => FakeExport.Message("Ana", $"Ana wrote a long enough message, number {index}."))
            .ToArray();

        var response = await Post(_factory.CreateClient(), FakeExport.Page("Solo", messages));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAChatWithTooFewMessages()
    {
        var response = await Post(_factory.CreateClient(), FakeExport.QuizzablePage(messagesPerSender: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAnOversizedUpload()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Quiz:MaxUploadBytes", "1024"))
            .CreateClient();

        var response = await Post(client, FakeExport.QuizzablePage());

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RateLimitsUploadsPerIp()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Quiz:UploadsPerMinutePerIp", "2"))
            .CreateClient();
        var page = FakeExport.QuizzablePage();

        Assert.Equal(HttpStatusCode.Created, (await Post(client, page)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(client, page)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, page)).StatusCode);
    }

    [Fact]
    public async Task ReturnsServiceUnavailableWhenTheMemoryCapIsReached()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Quiz:ChatMemoryCapBytes", "1"))
            .CreateClient();

        var response = await Post(client, FakeExport.QuizzablePage());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ErrorsNeverEchoChatContent()
    {
        const string secret = "my-private-message-text";
        var page = FakeExport.Page("Solo", FakeExport.Message("Ana", secret));

        var response = await Post(_factory.CreateClient(), page);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.DoesNotContain(secret, problem!.Detail);
    }

    private static string[] OtherPeoplesMessages()
    {
        return new[] { "Zed", "Yan", "Xia" }.SelectMany(sender => Enumerable
            .Range(0, 6)
            .Select(index => FakeExport.Message(sender, $"{sender} wrote a long enough message, number {index}.", reactions: "😆Zed")))
            .ToArray();
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string html)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        return await client.PostAsync("/api/chats", new MultipartFormDataContent
        {
            { content, "files", "chat/message_1.html" },
        });
    }

    private static async Task<ChatCreated> Upload(HttpClient client, string html)
    {
        var response = await Post(client, html);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChatCreated>())!;
    }

    private static async Task<HttpResponseMessage> SendQuestionRequest(HttpClient client, string chatId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, QuestionUrl);
        request.Headers.Add("X-Chat-Id", chatId);
        return await client.SendAsync(request);
    }

    private static async Task<WhoSaidThisQuestion> GetQuestion(HttpClient client, string chatId)
    {
        var response = await SendQuestionRequest(client, chatId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WhoSaidThisQuestion>())!;
    }

    private sealed record ChatCreated(
        string ChatId,
        string Title,
        IReadOnlyList<string> Participants,
        int MessageCount,
        int EligibleCount);
}
