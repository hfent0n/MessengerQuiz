using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MessengerQuiz.Api;
using MessengerQuiz.Api.Endpoints;
using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Questions;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Hosts such as Cloud Run choose the port and pass it in PORT.
if (builder.Configuration["PORT"] is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.Configure<QuizOptions>(builder.Configuration.GetSection(QuizOptions.SectionName));

builder.Services.AddOptions<KestrelServerOptions>().Configure<IOptions<QuizOptions>>((kestrel, quiz) =>
    kestrel.Limits.MaxRequestBodySize = quiz.Value.MaxUploadBytes);

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

builder.Services.AddSingleton<MessageSelector>();
builder.Services.AddSingleton<ChatStore>();
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

if (app.Services.GetRequiredService<IOptions<QuizOptions>>().Value.TrustForwardedHeaders)
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        // Only the last address, which the host's proxy adds. Earlier ones come from the visitor and can be faked.
        ForwardLimit = 1,
    };
    // The host's proxy addresses aren't fixed, so any proxy is trusted. That's only safe because the app can only
    // be reached through it.
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

app.UseSecurityHeaders();
app.UseSameOriginFor(GameHub.Path);
app.UseStaticFiles(SiteEndpoints.ClientFiles);

app.UseRateLimiter();

app.MapChatEndpoints();
app.MapQuestionEndpoints();
app.MapRoomEndpoints();
app.MapSiteEndpoints();

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
