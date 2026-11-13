using System.Text.Json;
using System.Threading.RateLimiting;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using MessengerQuiz.Api.Uploads;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Api.Endpoints.EndpointSupport;

namespace MessengerQuiz.Api.Endpoints;

public static class ChatEndpoints
{
    public const string UploadRateLimitPolicy = "uploads";

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats", UploadChat).RequireRateLimiting(UploadRateLimitPolicy);
        app.MapDelete("/api/chats/current", DeleteChat);
    }

    // The body is a ChatUpload as JSON, usually gzipped. Everything readable in it is sealed by the browser.
    private static async Task<IResult> UploadChat(
        HttpContext context,
        ConcurrencyLimiter parseLimiter,
        MessageSelector selector,
        ChatStore store,
        IOptions<QuizOptions> options,
        IOptions<JsonOptions> json)
    {
        var quiz = options.Value;

        if (context.Request.ContentLength > quiz.MaxUploadBytes)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This chat is too large to upload.");
        }

        using var lease = await parseLimiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The server is busy. Try again shortly.");
        }

        ChatUpload? upload;
        try
        {
            await using var body = await GzipReader.ReadAsync(context.Request.Body, quiz.MaxChatBytes, context.RequestAborted);
            upload = await JsonSerializer.DeserializeAsync<ChatUpload>(body, json.Value.SerializerOptions, context.RequestAborted);
        }
        catch (Exception exception) when (exception is ChatTooLargeException
            or BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This chat is too large to upload.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return Problem(StatusCodes.Status400BadRequest, "The chat couldn't be read.");
        }

        if (!ChatUploads.TryRead(upload, quiz, out var chat, out var error))
        {
            return Problem(StatusCodes.Status400BadRequest, error);
        }

        var eligible = selector.SelectEligible(chat);
        if (eligible.Select(message => message.Sender).Distinct().Count() < 2 || eligible.Count < quiz.MinQuizMessages)
        {
            return Problem(
                StatusCodes.Status422UnprocessableEntity,
                "This chat doesn't have enough messages from different people for a quiz.");
        }

        return Store(chat, eligible, store);
    }

    private static IResult Store(Chat chat, IReadOnlyList<Message> eligible, ChatStore store)
    {
        var session = new ChatSession(chat, eligible);
        if (!store.TryAdd(session))
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The server is full. Try again later.");
        }

        return Results.Created("/api/chats/current", new ChatCreatedResponse(session.Id, eligible.Count));
    }

    private static IResult DeleteChat(HttpContext context, ChatStore store)
    {
        store.Remove(context.GetChatId());
        return Results.NoContent();
    }

    private sealed record ChatCreatedResponse(string ChatId, int EligibleCount);
}
