using System.Text.Json;
using System.Threading.RateLimiting;
using MessengerQuiz.Api.Parsing;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Api.Endpoints.EndpointSupport;

namespace MessengerQuiz.Api.Endpoints;

public static class ChatEndpoints
{
    public const string UploadRateLimitPolicy = "uploads";

    // A JSON array of photo paths inside the chat folder, such as "photos/123.jpg". Only names are sent, never images.
    private const string AvailablePhotosField = "photos";

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats", UploadChat).RequireRateLimiting(UploadRateLimitPolicy);
        app.MapDelete("/api/chats/current", DeleteChat);
    }

    private static async Task<IResult> UploadChat(
        HttpContext context,
        ConcurrencyLimiter parseLimiter,
        IChatParser parser,
        MessageSelector selector,
        ChatStore store,
        IOptions<QuizOptions> options)
    {
        var quiz = options.Value;

        if (context.Request.ContentLength > quiz.MaxUploadBytes)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This chat is too large to upload.");
        }

        if (!context.Request.HasFormContentType)
        {
            return Problem(StatusCodes.Status400BadRequest, "Upload the chat as multipart form data.");
        }

        using var lease = await parseLimiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The server is busy. Try again shortly.");
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This chat is too large to upload.");
        }

        if (!TryReadAvailablePhotos(form, quiz.MaxAvailablePhotos, out var availablePhotos))
        {
            return Problem(StatusCodes.Status400BadRequest, "The list of photos couldn't be read.");
        }

        var streams = form.Files.Select(file => (file.FileName, Stream: file.OpenReadStream())).ToList();
        try
        {
            var pages = streams.Select(file => new ChatPage(file.FileName, file.Stream)).ToList();
            var chat = (await parser.ParseAsync(pages, context.RequestAborted)).KeepPhotos(availablePhotos);
            var eligible = selector.SelectEligible(chat);

            if (eligible.Select(message => message.Sender).Distinct().Count() < 2 || eligible.Count < quiz.MinQuizMessages)
            {
                return Problem(
                    StatusCodes.Status422UnprocessableEntity,
                    "This chat doesn't have enough messages from different people for a quiz.");
            }

            var session = new ChatSession(chat, eligible);
            if (!store.TryAdd(session))
            {
                return Problem(StatusCodes.Status503ServiceUnavailable, "The server is full. Try again later.");
            }

            return Results.Created(
                "/api/chats/current",
                new ChatCreatedResponse(session.Id, chat.Title, chat.Participants, chat.Messages.Count, eligible.Count));
        }
        catch (ChatParseException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, exception.Message);
        }
        finally
        {
            streams.ForEach(file => file.Stream.Dispose());
        }
    }

    private static bool TryReadAvailablePhotos(IFormCollection form, int maxPhotos, out HashSet<string> photos)
    {
        photos = [];
        var json = form[AvailablePhotosField].ToString();
        if (json.Length == 0)
        {
            return true;
        }

        try
        {
            var paths = JsonSerializer.Deserialize<string[]>(json);
            if (paths is null || paths.Length > maxPhotos)
            {
                return false;
            }

            photos = [.. paths];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IResult DeleteChat(HttpContext context, ChatStore store)
    {
        store.Remove(context.GetChatId());
        return Results.NoContent();
    }

    private sealed record ChatCreatedResponse(
        string ChatId,
        string Title,
        IReadOnlyList<string> Participants,
        int MessageCount,
        int EligibleCount);
}
