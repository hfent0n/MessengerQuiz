using System.Text.Json;
using System.Threading.RateLimiting;
using MessengerQuiz.Api.Demo;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Parsing;
using MessengerQuiz.Api.Selection;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Api.Endpoints.EndpointSupport;

namespace MessengerQuiz.Api.Endpoints;

public static class ChatEndpoints
{
    public const string UploadRateLimitPolicy = "uploads";

    // A JSON array of media paths inside the chat folder, such as "photos/123.jpg" or "videos/456.mp4".
    // Only names are sent, never the files.
    private const string AvailableMediaField = "media";

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats", UploadChat).RequireRateLimiting(UploadRateLimitPolicy);
        app.MapPost("/api/chats/demo", StartDemo).RequireRateLimiting(UploadRateLimitPolicy);
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

        if (quiz.DemoOnly)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "This site only plays the demo chat. Run Messenger Quiz on your own computer to play your own chats.");
        }

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

        if (!TryReadAvailableMedia(form, quiz.MaxAvailableMedia, out var availableMedia))
        {
            return Problem(StatusCodes.Status400BadRequest, "The list of media files couldn't be read.");
        }

        var decompressor = new PageDecompressor(quiz.MaxPagesBytes);
        var streams = new List<Stream>();
        try
        {
            var pages = new List<ChatPage>();
            foreach (var file in form.Files)
            {
                var upload = file.OpenReadStream();
                streams.Add(upload);
                var page = await decompressor.OpenAsync(upload, context.RequestAborted);
                if (page != upload)
                {
                    streams.Add(page);
                }

                pages.Add(new ChatPage(file.FileName, page));
            }

            var chat = (await parser.ParseAsync(pages, context.RequestAborted)).KeepMedia(availableMedia);
            var eligible = selector.SelectEligible(chat);

            if (eligible.Select(message => message.Sender).Distinct().Count() < 2 || eligible.Count < quiz.MinQuizMessages)
            {
                return Problem(
                    StatusCodes.Status422UnprocessableEntity,
                    "This chat doesn't have enough messages from different people for a quiz.");
            }

            return Store(chat, eligible, store);
        }
        catch (ChatTooLargeException)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This chat is too large to upload.");
        }
        catch (InvalidDataException)
        {
            return Problem(StatusCodes.Status400BadRequest, "A chat file couldn't be decompressed.");
        }
        catch (ChatParseException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, exception.Message);
        }
        finally
        {
            streams.ForEach(stream => stream.Dispose());
        }
    }

    // Every demo gets its own session, so players don't share which questions they've had.
    private static IResult StartDemo(DemoChat demo, MessageSelector selector, ChatStore store) =>
        Store(demo.Chat, selector.SelectEligible(demo.Chat), store);

    private static IResult Store(Chat chat, IReadOnlyList<Message> eligible, ChatStore store)
    {
        var session = new ChatSession(chat, eligible);
        if (!store.TryAdd(session))
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The server is full. Try again later.");
        }

        return Results.Created(
            "/api/chats/current",
            new ChatCreatedResponse(session.Id, chat.Title, chat.Participants, chat.Messages.Count, eligible.Count));
    }

    private static bool TryReadAvailableMedia(IFormCollection form, int maxPaths, out HashSet<string> media)
    {
        media = [];
        var json = form[AvailableMediaField].ToString();
        if (json.Length == 0)
        {
            return true;
        }

        try
        {
            var paths = JsonSerializer.Deserialize<string[]>(json);
            if (paths is null || paths.Length > maxPaths)
            {
                return false;
            }

            media = [.. paths];
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
