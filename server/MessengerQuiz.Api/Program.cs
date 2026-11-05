using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MessengerQuiz.Api;
using MessengerQuiz.Api.Demo;
using MessengerQuiz.Api.Endpoints;
using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Parsing;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<QuizOptions>(builder.Configuration.GetSection(QuizOptions.SectionName));

builder.Services.AddOptions<KestrelServerOptions>().Configure<IOptions<QuizOptions>>((kestrel, quiz) =>
    kestrel.Limits.MaxRequestBodySize = quiz.Value.MaxUploadBytes);

builder.Services.AddOptions<FormOptions>().Configure<IOptions<QuizOptions>>((form, quiz) =>
{
    form.MultipartBodyLengthLimit = quiz.Value.MaxUploadBytes;
    // Uploads over the default threshold are buffered to temp files, and chats must never reach disk.
    form.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(ChatEndpoints.UploadRateLimitPolicy, context =>
        PerIpLimit(context, quiz => quiz.UploadsPerMinutePerIp));
    limiter.AddPolicy(RoomEndpoints.RoomRateLimitPolicy, context =>
        PerIpLimit(context, quiz => quiz.RoomRequestsPerMinutePerIp));
});

builder.Services.AddSignalR().AddJsonProtocol(json =>
    json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSingleton<IChatParser, HtmlChatParser>();
builder.Services.AddSingleton<MessageSelector>();
builder.Services.AddSingleton<ChatStore>();
builder.Services.AddSingleton<DemoChat>();
builder.Services.AddSingleton<IQuestionGenerator<WhoSaidThisQuestion>, WhoSaidThisGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddHostedService<ExpiredChatSweeper>();
builder.Services.AddSingleton(services =>
{
    var quiz = services.GetRequiredService<IOptions<QuizOptions>>().Value;
    return new ConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
        PermitLimit = quiz.MaxConcurrentParses,
        QueueLimit = quiz.ParseQueueLimit,
    });
});

var app = builder.Build();

app.UseRateLimiter();

app.MapChatEndpoints();
app.MapQuestionEndpoints();
app.MapRoomEndpoints();

app.Run();

static RateLimitPartition<string> PerIpLimit(HttpContext context, Func<QuizOptions, int> permitsPerMinute)
{
    var quiz = context.RequestServices.GetRequiredService<IOptions<QuizOptions>>().Value;
    return RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitsPerMinute(quiz),
            Window = TimeSpan.FromMinutes(1),
        });
}
