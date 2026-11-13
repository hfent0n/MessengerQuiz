using System.Net;
using System.Net.Http.Json;
using System.Text;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Uploads;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using static MessengerQuiz.Tests.TestChats;

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

        var chat = await Send(client, QuizzableUpload(people: 3));
        var question = await GetQuestion(client, chat.ChatId);

        Assert.Equal(18, chat.EligibleCount);
        Assert.Equal(3, question.Options.Count);
        Assert.Contains(question.Answer, question.Options);
        Assert.StartsWith($"p{question.Answer}-", Label(question.Sealed));
    }

    [Fact]
    public async Task SealedBoxesComeBackExactlyAsUploaded()
    {
        var client = _factory.CreateClient();
        var upload = QuizzableUpload();

        var chat = await Send(client, upload);
        var question = await GetQuestion(client, chat.ChatId);

        Assert.Equal(upload.Messages![question.Position].Sealed, question.Sealed);
    }

    [Fact]
    public async Task QuestionsIncludeEarlierMessagesFromTheSameSender()
    {
        var client = _factory.CreateClient();
        // Person 0 sends a run of three, and only the last can be a question. Person 1 replies.
        var messages = Enumerable.Range(0, 12).SelectMany(index => new[]
        {
            Uploaded(0, $"first {index}", minute: index * 10, quizzable: false),
            Uploaded(0, $"photo {index}", minute: index * 10 + 1, quizzable: false, media: 0),
            Uploaded(0, $"question {index}", minute: index * 10 + 2),
            Uploaded(1, $"reply {index}", minute: index * 10 + 3),
        });
        var chat = await Send(client, Upload(2, messages));

        var question = await GetQuestion(client, chat.ChatId);
        while (question.Answer != 0)
        {
            question = await GetQuestion(client, chat.ChatId);
        }

        var index = Label(question.Sealed).Split(' ').Last();
        Assert.Equal([$"first {index}", $"photo {index}"], question.Context.Select(context => Label(context.Sealed)));
        Assert.Equal([question.Position - 2, question.Position - 1], question.Context.Select(context => context.Position));
    }

    [Fact]
    public async Task MediaQuestionsSayHowManyItemsTheyHave()
    {
        var client = _factory.CreateClient();
        var messages = QuizzableUpload().Messages!.Append(Uploaded(1, "a photo", reactions: 50, media: 1));
        var chat = await Send(client, Upload(3, messages));

        var media = new List<(string, int)>();
        HttpResponseMessage response;
        while ((response = await SendQuestionRequest(client, chat.ChatId)).StatusCode == HttpStatusCode.OK)
        {
            var question = (await response.Content.ReadFromJsonAsync<WhoSaidThisQuestion>())!;
            if (question.MediaCount > 0)
            {
                media.Add((Label(question.Sealed), question.Answer));
            }
        }

        Assert.Equal([("a photo", 1)], media);
    }

    [Fact]
    public async Task OneChatIdNeverReachesAnotherChat()
    {
        var client = _factory.CreateClient();
        var first = await Send(client, QuizzableUpload());
        var second = await Send(client, Upload(3, QuizzableUpload().Messages!.Select(message =>
            message with { Sealed = Sealed("second chat") })));

        Assert.StartsWith("p", Label((await GetQuestion(client, first.ChatId)).Sealed));
        Assert.Equal("second chat", Label((await GetQuestion(client, second.ChatId)).Sealed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-chat")]
    public async Task MissingOrUnknownChatIdIsRejected(string? chatId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, QuestionUrl);
        if (chatId is not null)
        {
            request.Headers.Add("X-Chat-Id", chatId);
        }

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatIdsAreLongAndRandom()
    {
        var client = _factory.CreateClient();

        var first = await Send(client, QuizzableUpload());
        var second = await Send(client, QuizzableUpload());

        Assert.Matches("^[0-9a-f]{64}$", first.ChatId);
        Assert.NotEqual(first.ChatId, second.ChatId);
    }

    [Fact]
    public async Task ReturnsNoContentWhenQuestionsRunOut()
    {
        var client = _factory.CreateClient();
        var chat = await Send(client, QuizzableUpload());

        for (var index = 0; index < chat.EligibleCount; index++)
        {
            await GetQuestion(client, chat.ChatId);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendQuestionRequest(client, chat.ChatId)).StatusCode);
    }

    [Fact]
    public async Task DeleteWipesTheChat()
    {
        var client = _factory.CreateClient();
        var chat = await Send(client, QuizzableUpload());
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chats/current");
        request.Headers.Add("X-Chat-Id", chat.ChatId);

        var deleted = await client.SendAsync(request);
        var question = await SendQuestionRequest(client, chat.ChatId);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, question.StatusCode);
    }

    [Fact]
    public async Task AcceptsPlainJsonAsWellAsGzip()
    {
        var response = await _factory.CreateClient().PostAsync("/api/chats", Content(QuizzableUpload(), gzip: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public static TheoryData<string, ChatUpload> BrokenUploads()
    {
        var good = QuizzableUpload();
        var first = good.Messages![0];
        return new()
        {
            { "no messages", good with { Messages = [] } },
            { "no message list", good with { Messages = null } },
            { "no people", good with { PersonCount = 0 } },
            { "missing title", good with { Title = null } },
            { "a sender who isn't in the chat", Replace(good, first with { Sender = 3 }) },
            { "a negative sender", Replace(good, first with { Sender = -1 }) },
            { "negative reactions", Replace(good, first with { Reactions = -1 }) },
            { "a time before Messenger", Replace(good, first with { SentAt = 0 }) },
            { "a box that isn't base64url", Replace(good, first with { Sealed = "not base64url!".PadRight(40, '!') }) },
            { "a box too short to be sealed", Replace(good, first with { Sealed = "abc" }) },
            { "a missing box", Replace(good, first with { Sealed = null }) },
            { "a box that's too long", Replace(good, first with { Sealed = new string('a', 100_001) }) },
        };
    }

    [Theory]
    [MemberData(nameof(BrokenUploads))]
    public async Task RejectsBrokenUploads(string problem, ChatUpload upload)
    {
        var response = await _factory.CreateClient().PostAsync("/api/chats", Content(upload));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, problem);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("")]
    public async Task RejectsBodiesThatArentAChat(string body)
    {
        var response = await _factory.CreateClient().PostAsync("/api/chats", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsTooManyMessages()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("Quiz:MaxMessages", "10")).CreateClient();

        var response = await client.PostAsync("/api/chats", Content(QuizzableUpload()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAChatWithOnlyOneSender()
    {
        var response = await Post(_factory.CreateClient(), Upload(1, Enumerable.Range(0, 20).Select(index => Uploaded(0, $"{index}"))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAChatWithTooFewQuestions()
    {
        var response = await Post(_factory.CreateClient(), QuizzableUpload(messagesPerPerson: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAnOversizedUpload()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("Quiz:MaxUploadBytes", "256")).CreateClient();

        var response = await Post(client, QuizzableUpload());

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RejectsGzipThatIsTooLargeOnceDecompressed()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("Quiz:MaxChatBytes", "4096")).CreateClient();

        // Repetitive JSON compresses well, so the upload itself stays small.
        var response = await Post(client, QuizzableUpload(messagesPerPerson: 200));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task RejectsBrokenGzipData()
    {
        var content = new ByteArrayContent([0x1f, 0x8b, 1, 2, 3, 4]);

        var response = await _factory.CreateClient().PostAsync("/api/chats", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateLimitsUploadsPerIp()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("Quiz:UploadsPerMinutePerIp", "2")).CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await Post(client, QuizzableUpload())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(client, QuizzableUpload())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, QuizzableUpload())).StatusCode);
    }

    [Fact]
    public async Task ReturnsServiceUnavailableWhenTheMemoryCapIsReached()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("Quiz:ChatMemoryCapBytes", "1")).CreateClient();

        var response = await Post(client, QuizzableUpload());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ErrorsNeverEchoWhatWasUploaded()
    {
        const string secret = "zzz-uploaded-value-zzz";
        var upload = QuizzableUpload() with { Title = secret };

        var response = await Post(_factory.CreateClient(), upload);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.DoesNotContain(secret, problem!.Detail);
    }

    private static ChatUpload Replace(ChatUpload upload, UploadedMessage first) =>
        upload with { Messages = [first, .. upload.Messages!.Skip(1)] };

    private static Task<HttpResponseMessage> Post(HttpClient client, ChatUpload upload) =>
        client.PostAsync("/api/chats", Content(upload));

    private static async Task<ChatCreated> Send(HttpClient client, ChatUpload upload)
    {
        var response = await Post(client, upload);
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

    private sealed record ChatCreated(string ChatId, int EligibleCount);
}
