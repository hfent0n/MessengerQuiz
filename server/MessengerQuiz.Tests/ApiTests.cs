using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    public async Task QuestionsIncludeEarlierMessagesFromTheSameSender()
    {
        var client = _factory.CreateClient();
        // Newest first, as in the export. Ana sends two messages in a row and Ben replies.
        var messages = Enumerable.Range(0, 12).SelectMany(index => new[]
        {
            FakeExport.Message("Ben", $"Ben replied with a long enough message, number {index}.", $"Mar 1, 2023 {index + 1}:02:00pm"),
            FakeExport.Message("Ana", $"Ana followed up with a long enough message {index}.", $"Mar 1, 2023 {index + 1}:01:00pm", "😆Ben", "😮Cal"),
            FakeExport.Message("Ana", $"Ana opened with context {index}", $"Mar 1, 2023 {index + 1}:00:00pm"),
        }).Reverse().ToArray();
        var chat = await Upload(client, FakeExport.Page("Context", messages));

        var question = await GetQuestion(client, chat.ChatId);
        while (question.Answer != "Ana")
        {
            question = await GetQuestion(client, chat.ChatId);
        }

        Assert.StartsWith("Ana followed up", question.Text);
        Assert.StartsWith("Ana opened with context", Assert.Single(question.Context).Text);
    }

    [Fact]
    public async Task QuestionsIncludeSeveralContextMessagesInOrder()
    {
        var client = _factory.CreateClient();
        // Ana's context lines are too short to be questions, so her only questions have three messages of context.
        var messages = Enumerable.Range(0, 12).SelectMany(index => new[]
        {
            FakeExport.Message("Ana", $"First {index}", $"Mar 1, 2023 {index + 1}:00:00pm"),
            FakeExport.PhotoAt("Ana", $"Mar 1, 2023 {index + 1}:01:00pm"),
            FakeExport.Message("Ana", $"Third {index}", $"Mar 1, 2023 {index + 1}:02:00pm"),
            FakeExport.Message("Ana", $"Ana finished with a long enough message {index}.", $"Mar 1, 2023 {index + 1}:03:00pm", "😆Ben"),
            FakeExport.Message("Ben", $"Ben replied with a long enough message, number {index}.", $"Mar 1, 2023 {index + 1}:04:00pm"),
        }).Reverse().ToArray();
        var chat = await Upload(client, FakeExport.Page("Context", messages));

        var question = await GetQuestion(client, chat.ChatId);
        while (question.Answer != "Ana")
        {
            question = await GetQuestion(client, chat.ChatId);
        }

        var index = question.Text.Split(' ').Last().TrimEnd('.');
        // The photo wasn't listed as available, so it stays a placeholder.
        Assert.Equal(
            [($"First {index}", false, 0), ("", true, 0), ($"Third {index}", false, 0)],
            question.Context.Select(context => (context.Text, context.HasMedia, context.Media.Count)));
    }

    [Fact]
    public async Task PhotoQuestionsOnlyUseAvailablePhotos()
    {
        var client = _factory.CreateClient();
        var messages = Enumerable.Range(0, 12).SelectMany(index => new[]
        {
            FakeExport.PhotoAt("Ana", $"Mar 1, 2023 {index + 1}:00:00pm", $"{index}.jpg", ["😆Ben"]),
            FakeExport.Message("Ben", $"Ben replied with a long enough message, number {index}.", $"Mar 1, 2023 {index + 1}:30:00pm"),
        }).Reverse().ToArray();
        var available = Enumerable.Range(0, 6).Select(index => $"photos/{index}.jpg").ToList();
        var chat = await Upload(client, FakeExport.Page("Photos", messages), JsonSerializer.Serialize(available));

        var questions = new List<WhoSaidThisQuestion>();
        for (var index = 0; index < chat.EligibleCount; index++)
        {
            questions.Add(await GetQuestion(client, chat.ChatId));
        }

        var photoQuestions = questions.Where(question => question.Media.Count > 0).ToList();
        Assert.Equal(6, photoQuestions.Count);
        Assert.All(photoQuestions, question => Assert.Contains(Assert.Single(question.Media), available));
        Assert.All(photoQuestions, question => Assert.Equal("Ana", question.Answer));
    }

    [Fact]
    public async Task GifsAndVideosCanBeQuestions()
    {
        var client = _factory.CreateClient();
        var messages = OtherPeoplesMessages()
            .Append(FakeExport.Gif("Dee", "1.gif"))
            .Append(FakeExport.Video("Eve", "2.mp4"))
            .ToArray();
        var chat = await Upload(
            client, FakeExport.Page("Media", messages), JsonSerializer.Serialize(new[] { "gifs/1.gif", "videos/2.mp4" }));

        // Context uses up some messages, so ask until the chat runs out.
        var media = new Dictionary<string, string>();
        HttpResponseMessage response;
        while ((response = await SendQuestionRequest(client, chat.ChatId)).StatusCode == HttpStatusCode.OK)
        {
            var question = (await response.Content.ReadFromJsonAsync<WhoSaidThisQuestion>())!;
            if (question.Media.Count > 0)
            {
                media[Assert.Single(question.Media)] = question.Answer;
            }
        }

        Assert.Equal(new Dictionary<string, string> { ["gifs/1.gif"] = "Dee", ["videos/2.mp4"] = "Eve" }, media);
    }

    [Fact]
    public async Task WithoutAPhotoListPhotosAreNotQuestions()
    {
        var client = _factory.CreateClient();
        var messages = Enumerable.Range(0, 12).SelectMany(index => new[]
        {
            FakeExport.PhotoAt("Ana", $"Mar 1, 2023 {index + 1}:00:00pm", $"{index}.jpg", ["😆Ben"]),
            FakeExport.Message("Ana", $"Ana wrote a long enough message, number {index}.", $"Mar 1, 2023 {index + 1}:20:00pm"),
            FakeExport.Message("Ben", $"Ben replied with a long enough message, number {index}.", $"Mar 1, 2023 {index + 1}:40:00pm"),
        }).Reverse().ToArray();
        var chat = await Upload(client, FakeExport.Page("Photos", messages));

        Assert.Equal(24, chat.EligibleCount);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"photo\": 1}")]
    public async Task RejectsAnUnreadablePhotoList(string photos)
    {
        var response = await Post(_factory.CreateClient(), FakeExport.QuizzablePage(), photos);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task AcceptsGzippedPages()
    {
        var response = await Post(_factory.CreateClient(), FakeExport.QuizzablePage("Zipped"), gzip: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Zipped", (await response.Content.ReadFromJsonAsync<ChatCreated>())!.Title);
    }

    [Fact]
    public async Task RejectsGzippedPagesThatAreTooLargeOnceDecompressed()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Quiz:MaxPagesBytes", "1024"))
            .CreateClient();

        // Repetitive HTML compresses well, so the upload itself stays small.
        var page = FakeExport.QuizzablePage(messagesPerSender: 200);
        var response = await Post(client, page, gzip: true);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RejectsBrokenGzipData()
    {
        var client = _factory.CreateClient();
        var content = new ByteArrayContent([0x1f, 0x8b, 1, 2, 3, 4]);
        var form = new MultipartFormDataContent { { content, "files", "chat/message_1.html" } };

        var response = await client.PostAsync("/api/chats", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<HttpResponseMessage> Post(
        HttpClient client, string html, string? photos = null, bool gzip = false)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        var content = new ByteArrayContent(gzip ? Gzip(bytes) : bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(gzip ? "application/gzip" : "text/html");
        var form = new MultipartFormDataContent { { content, "files", "chat/message_1.html" } };
        if (photos is not null)
        {
            form.Add(new StringContent(photos), "media");
        }

        return await client.PostAsync("/api/chats", form);
    }

    private static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(bytes);
        }

        return output.ToArray();
    }

    private static async Task<ChatCreated> Upload(HttpClient client, string html, string? photos = null)
    {
        var response = await Post(client, html, photos);
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
