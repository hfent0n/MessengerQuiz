using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MessengerQuiz.Tests;

public class MultiplayerApiTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<TestPlayer> _players = [];

    public MultiplayerApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Quiz:UploadsPerMinutePerIp", "1000");
            builder.UseSetting("Quiz:RoomRequestsPerMinutePerIp", "1000");
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(_time));
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var player in _players)
        {
            await player.DisposeAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TwoPlayersPlayAWholeGame()
    {
        var (host, guest) = await StartTwoPlayerGame();

        for (var number = 1; number <= 10; number++)
        {
            var question = await host.WaitFor(view => Stage(view) == "question" && view.GetProperty("questionNumber").GetInt32() == number);
            await guest.WaitFor(view => Stage(view) == "question" && view.GetProperty("questionNumber").GetInt32() == number);

            // Nobody can find the answer in the question.
            Assert.False(question.GetProperty("question").TryGetProperty("answer", out _));
            Assert.Equal(JsonValueKind.Null, question.GetProperty("reveal").ValueKind);

            var options = Options(question);
            // Players get the sealed box and people's numbers. The key in the game link opens them.
            Assert.Equal(TestChats.Sealed("people"), question.GetProperty("people").GetString());
            Assert.StartsWith("p", TestChats.Label(question.GetProperty("question").GetProperty("sealed").GetString()!));

            Assert.Null(await host.Hub.InvokeAsync<string?>("Answer", options[0]));
            Assert.Null(await guest.Hub.InvokeAsync<string?>("Answer", options[1]));

            var reveal = await host.WaitFor(view => Stage(view) == "reveal");
            Assert.Contains(reveal.GetProperty("reveal").GetProperty("answer").GetInt32(), options);

            await host.Hub.InvokeAsync<string?>("Ready");
            await guest.Hub.InvokeAsync<string?>("Ready");
        }

        var finished = await guest.WaitFor(view => Stage(view) == "finished");
        var scores = finished.GetProperty("players").EnumerateArray().Select(player => player.GetProperty("score").GetInt32()).ToList();
        Assert.Equal(scores.OrderDescending(), scores);
        Assert.Equal(10_000, finished.GetProperty("maxScore").GetInt32());
    }

    [Fact]
    public async Task QuestionsEndWhenTimeRunsOut()
    {
        var (host, guest) = await StartTwoPlayerGame();
        await guest.WaitFor(view => Stage(view) == "question");
        var options = Options(await host.WaitFor(view => Stage(view) == "question"));
        await host.Hub.InvokeAsync<string?>("Answer", options[0]);

        _time.Advance(TimeSpan.FromSeconds(21));

        var reveal = await guest.WaitFor(view => Stage(view) == "reveal");
        var guestPick = reveal.GetProperty("reveal").GetProperty("picks").EnumerateArray()
            .Single(pick => pick.GetProperty("playerId").GetString() == reveal.GetProperty("youId").GetString());
        Assert.Equal(JsonValueKind.Null, guestPick.GetProperty("option").ValueKind);
        Assert.Equal(0, guestPick.GetProperty("points").GetInt32());
    }

    [Fact]
    public async Task LateAnswersAreRejected()
    {
        var (host, _) = await StartTwoPlayerGame();
        var options = Options(await host.WaitFor(view => Stage(view) == "question"));

        _time.Advance(TimeSpan.FromSeconds(20.5));
        Assert.Null(await host.Hub.InvokeAsync<string?>("Answer", options[0]));

        var (secondHost, _) = await StartTwoPlayerGame();
        var secondOptions = Options(await secondHost.WaitFor(view => Stage(view) == "question"));
        _time.Advance(TimeSpan.FromSeconds(21));
        Assert.NotNull(await secondHost.Hub.InvokeAsync<string?>("Answer", secondOptions[0]));
    }

    [Fact]
    public async Task PlayersCantJoinAfterTheStart()
    {
        var (host, _) = await StartTwoPlayerGame();

        var response = await Join(host.Code, "Late");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EightPlayersCanJoinButNotNine()
    {
        var (_, code, _) = await CreateRoom("Host");
        for (var index = 2; index <= 8; index++)
        {
            Assert.Equal(HttpStatusCode.Created, (await Join(code, $"Player {index}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await Join(code, "Player 9")).StatusCode);
    }

    [Theory]
    [InlineData("<b>Ana</b>")]
    [InlineData("")]
    [InlineData("Twenty one characters")]
    public async Task InvalidNamesAreRejected(string name)
    {
        var (_, code, _) = await CreateRoom("Host");

        Assert.Equal(HttpStatusCode.BadRequest, (await Join(code, name)).StatusCode);
    }

    [Fact]
    public async Task DuplicateNamesAreRejected()
    {
        var (_, code, _) = await CreateRoom("Host");

        Assert.Equal(HttpStatusCode.Conflict, (await Join(code, "host")).StatusCode);
    }

    [Fact]
    public async Task UnknownRoomsAreNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Join("aaaaaaaaaa", "Ana")).StatusCode);
    }

    [Fact]
    public async Task OnlyTheChatsOwnerCanCreateARoom()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms")
        {
            Content = JsonContent.Create(new { name = "Host", timeLimitSeconds = 20 }),
        };
        request.Headers.Add("X-Chat-Id", "not-a-chat");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OnlyOfferedTimeLimitsAreAllowed()
    {
        var chatId = await UploadChat();

        var response = await PostRoom(chatId, "Host", timeLimitSeconds: 7);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AWrongTokenCantConnect()
    {
        var (_, code, _) = await CreateRoom("Host");
        var intruder = await Connect(code, "not-a-token");

        Assert.NotNull(intruder.ConnectError);
    }

    [Fact]
    public async Task ReconnectingKeepsTheSeatAndScore()
    {
        var (host, guest) = await StartTwoPlayerGame();
        var options = Options(await host.WaitFor(view => Stage(view) == "question"));
        await guest.WaitFor(view => Stage(view) == "question");
        await host.Hub.InvokeAsync<string?>("Answer", options[0]);
        await guest.Hub.InvokeAsync<string?>("Answer", options[0]);
        var reveal = await host.WaitFor(view => Stage(view) == "reveal");
        var hostScore = Score(reveal, reveal.GetProperty("youId").GetString()!);

        await host.Hub.StopAsync();
        var again = await Connect(host.Code, host.Token);

        var view = await again.WaitFor(view => Stage(view) == "reveal");
        Assert.Equal(hostScore, Score(view, view.GetProperty("youId").GetString()!));
        Assert.True(view.GetProperty("youAreHost").GetBoolean());
    }

    [Fact]
    public async Task WipingTheChatEndsTheGameForEveryone()
    {
        var (host, guest) = await StartTwoPlayerGame();
        await guest.WaitFor(view => Stage(view) == "question");

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chats/current");
        request.Headers.Add("X-Chat-Id", host.ChatId);
        await _factory.CreateClient().SendAsync(request);

        await guest.WaitFor(view => Stage(view) == "ended");
        Assert.NotNull(await guest.Hub.InvokeAsync<string?>("Ready"));
    }

    [Fact]
    public async Task OnlyTheHostCanStart()
    {
        var (host, code, _) = await CreateRoom("Host");
        var guest = await JoinAndConnect(code, "Guest");

        Assert.NotNull(await guest.Hub.InvokeAsync<string?>("Start"));
        Assert.Null(await host.Hub.InvokeAsync<string?>("Start"));
    }

    [Fact]
    public async Task ARematchUsesNewQuestions()
    {
        var (host, guest) = await StartTwoPlayerGame();
        var firstGame = await PlayThrough(host, guest);

        Assert.Null(await host.Hub.InvokeAsync<string?>("Start"));
        var secondGame = await PlayThrough(host, guest);

        Assert.Empty(firstGame.Intersect(secondGame));
    }

    private async Task<List<string>> PlayThrough(TestPlayer host, TestPlayer guest)
    {
        var texts = new List<string>();
        for (var number = 1; number <= 10; number++)
        {
            var view = await host.WaitFor(view => Stage(view) == "question" && view.GetProperty("questionNumber").GetInt32() == number);
            texts.Add(TestChats.Label(view.GetProperty("question").GetProperty("sealed").GetString()!));
            _time.Advance(TimeSpan.FromSeconds(21));
            await host.WaitFor(view => Stage(view) == "reveal" && view.GetProperty("questionNumber").GetInt32() == number);
            await host.Hub.InvokeAsync<string?>("Ready");
            await guest.Hub.InvokeAsync<string?>("Ready");
        }

        await host.WaitFor(view => Stage(view) == "finished");
        return texts;
    }

    private async Task<(TestPlayer Host, TestPlayer Guest)> StartTwoPlayerGame()
    {
        var (host, code, _) = await CreateRoom("Host");
        var guest = await JoinAndConnect(code, "Guest");
        await host.WaitFor(view => view.GetProperty("players").GetArrayLength() == 2);

        Assert.Null(await host.Hub.InvokeAsync<string?>("Start"));
        return (host, guest);
    }

    private async Task<(TestPlayer Host, string Code, string ChatId)> CreateRoom(string name)
    {
        var chatId = await UploadChat();
        var response = await PostRoom(chatId, name, timeLimitSeconds: 20);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var joined = (await response.Content.ReadFromJsonAsync<Joined>())!;
        var host = await Connect(joined.Code, joined.PlayerToken, chatId);
        Assert.Null(host.ConnectError);
        return (host, joined.Code, chatId);
    }

    private async Task<TestPlayer> JoinAndConnect(string code, string name)
    {
        var response = await Join(code, name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var joined = (await response.Content.ReadFromJsonAsync<Joined>())!;
        var player = await Connect(code, joined.PlayerToken);
        Assert.Null(player.ConnectError);
        return player;
    }

    // Enough text messages for two games.
    private async Task<string> UploadChat()
    {
        var response = await _factory.CreateClient().PostAsync("/api/chats", TestChats.Content(TestChats.QuizzableUpload(people: 4, messagesPerPerson: 10)));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("chatId").GetString()!;
    }

    private Task<HttpResponseMessage> PostRoom(string chatId, string name, int timeLimitSeconds)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms")
        {
            Content = JsonContent.Create(new { name, timeLimitSeconds }),
        };
        request.Headers.Add("X-Chat-Id", chatId);
        return _factory.CreateClient().SendAsync(request);
    }

    private Task<HttpResponseMessage> Join(string code, string name) =>
        _factory.CreateClient().PostAsJsonAsync($"/api/rooms/{code}/players", new { name });

    private async Task<TestPlayer> Connect(string code, string token, string chatId = "")
    {
        var server = _factory.Server;
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "api/rooms/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .Build();

        var player = new TestPlayer(hub, code, token, chatId);
        _players.Add(player);
        await hub.StartAsync();
        player.ConnectError = await hub.InvokeAsync<string?>("Connect", code, token);
        return player;
    }

    private static string Stage(JsonElement view) => view.GetProperty("stage").GetString()!;

    // People's numbers.
    private static List<int> Options(JsonElement view) =>
        view.GetProperty("question").GetProperty("options").EnumerateArray().Select(option => option.GetInt32()).ToList();

    private static int Score(JsonElement view, string playerId) => view.GetProperty("players").EnumerateArray()
        .Single(player => player.GetProperty("id").GetString() == playerId).GetProperty("score").GetInt32();

    private sealed record Joined(string Code, string PlayerToken);

    private sealed class TestPlayer : IAsyncDisposable
    {
        private readonly Channel<JsonElement> _views = Channel.CreateUnbounded<JsonElement>();

        public TestPlayer(HubConnection hub, string code, string token, string chatId)
        {
            Hub = hub;
            Code = code;
            Token = token;
            ChatId = chatId;
            hub.On<JsonElement>("room", view => _views.Writer.TryWrite(view));
        }

        public HubConnection Hub { get; }
        public string Code { get; }
        public string Token { get; }
        public string ChatId { get; }
        public string? ConnectError { get; set; }

        // Skips older updates until one matches.
        public async Task<JsonElement> WaitFor(Func<JsonElement, bool> match)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                var view = await _views.Reader.ReadAsync(timeout.Token);
                if (match(view))
                {
                    return view;
                }
            }
        }

        public ValueTask DisposeAsync() => Hub.DisposeAsync();
    }
}
