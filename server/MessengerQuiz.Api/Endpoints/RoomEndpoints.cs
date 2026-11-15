using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;
using static MessengerQuiz.Api.Endpoints.EndpointSupport;

namespace MessengerQuiz.Api.Endpoints;

public static class RoomEndpoints
{
    public const string RoomRateLimitPolicy = "rooms";

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rooms", CreateRoom).RequireRateLimiting(RoomRateLimitPolicy);
        app.MapPost("/api/rooms/{code}/players", JoinRoom).RequireRateLimiting(RoomRateLimitPolicy);
        app.MapHub<GameHub>(GameHub.Path);

        // Media relayed from the host to the other players, sealed with the game link's key.
        app.MapPut("/api/rooms/{code}/media/{position:int}/{index:int}", UploadMedia);
        app.MapGet("/api/rooms/{code}/media/{position:int}/{index:int}", GetMedia);
    }

    // A player's secret token, in a header so it stays out of URLs and logs.
    private const string PlayerTokenHeader = "X-Player-Token";

    // The body is the sealed file. An empty body means the host couldn't send it, such as a video over the limit.
    private static async Task<IResult> UploadMedia(
        string code, int position, int index, HttpContext context, GameService games, IOptions<QuizOptions> options)
    {
        var limit = options.Value.MaxRelayedFileBytes;
        if (context.Request.ContentLength > limit)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "This file is too large to send to the other players.");
        }

        using var body = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            if (body.Length + read > limit)
            {
                return Problem(StatusCodes.Status413PayloadTooLarge, "This file is too large to send to the other players.");
            }

            body.Write(buffer, 0, read);
        }

        var file = body.Length == 0 ? null : body.ToArray();
        return await games.UploadMediaAsync(code, context.Request.Headers[PlayerTokenHeader].ToString(), position, index, file) switch
        {
            null => Problem(StatusCodes.Status403Forbidden, "Only the host can send the game's media."),
            MediaUpload.Stored => Results.NoContent(),
            MediaUpload.NotWanted => Problem(StatusCodes.Status404NotFound, "This game doesn't need that file."),
            MediaUpload.TooLarge => Problem(StatusCodes.Status413PayloadTooLarge, "This file is too large to send to the other players."),
            _ => Problem(StatusCodes.Status507InsufficientStorage, "There's no room for more media right now."),
        };
    }

    // 404 means the host hasn't sent the file yet, so try again shortly. 410 means it won't come.
    private static IResult GetMedia(string code, int position, int index, HttpContext context, GameService games)
    {
        var lookup = games.FindMedia(code, context.Request.Headers[PlayerTokenHeader].ToString(), position, index, out var file);
        context.Response.Headers.CacheControl = "no-store";
        return lookup switch
        {
            null => Problem(StatusCodes.Status401Unauthorized, "You're not in this game."),
            MediaLookup.Found => Results.Bytes(file!, "application/octet-stream"),
            MediaLookup.NotYet => Problem(StatusCodes.Status404NotFound, "This file hasn't arrived yet."),
            _ => Problem(StatusCodes.Status410Gone, "This file isn't available."),
        };
    }

    // The host's chat is identified by its ID, as for questions.
    private static IResult CreateRoom(
        HttpContext context, CreateRoomRequest request, ChatStore chats, GameService games, IOptions<QuizOptions> options)
    {
        var chatId = context.GetChatId();
        if (!chats.TryGet(chatId, out var session) || session is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "This chat has expired. Upload it again to keep playing.");
        }

        if (!QuestionEndpoints.TryParseSelection(request.Selection, out var selection))
        {
            return Problem(StatusCodes.Status400BadRequest, "Choose reacted or random messages.");
        }

        return games.CreateRoom(chatId, session, request.Name, request.TimeLimitSeconds, request.Endless ? null : request.QuestionCount ?? QuestionsPerGame(options), selection, out var room, out var host) switch
        {
            CreateOutcome.InvalidName => Problem(StatusCodes.Status400BadRequest, InvalidNameMessage),
            CreateOutcome.InvalidTimeLimit => Problem(StatusCodes.Status400BadRequest, "Choose one of the time limits."),
            CreateOutcome.InvalidQuestionCount => Problem(StatusCodes.Status400BadRequest, "Choose one of the numbers of questions."),
            CreateOutcome.TooManyRooms => Problem(
                StatusCodes.Status409Conflict, "This chat already has several games. Wipe it and start again."),
            _ => Results.Created($"/play/{room!.Code}", new JoinedRoomResponse(room.Code, host!.Token)),
        };
    }

    private static async Task<IResult> JoinRoom(string code, JoinRoomRequest request, GameService games)
    {
        var result = await games.JoinAsync(code, request.Name);
        if (result.NotFound)
        {
            return Problem(StatusCodes.Status404NotFound, "This game has ended or the link is wrong.");
        }

        if (result.InvalidName)
        {
            return Problem(StatusCodes.Status400BadRequest, InvalidNameMessage);
        }

        return result.Outcome switch
        {
            JoinOutcome.AlreadyStarted => Problem(StatusCodes.Status409Conflict, "This game has already started."),
            JoinOutcome.Full => Problem(StatusCodes.Status409Conflict, "This game is full."),
            JoinOutcome.NameTaken => Problem(StatusCodes.Status409Conflict, "Someone in this game already has that name."),
            _ => Results.Created($"/play/{code}", new JoinedRoomResponse(code.Trim().ToLowerInvariant(), result.Player!.Token)),
        };
    }

    private const string InvalidNameMessage =
        "Names can be up to 20 letters and numbers, with single spaces between words.";

    private static int QuestionsPerGame(IOptions<QuizOptions> options) => options.Value.QuestionsPerGame;

    // QuestionCount is optional and defaults to QuestionsPerGame. Endless ignores it.
    // Selection is "reacted", the default, or "random".
    private sealed record CreateRoomRequest(
        string? Name, int TimeLimitSeconds, int? QuestionCount = null, bool Endless = false, string? Selection = null);

    private sealed record JoinRoomRequest(string? Name);

    private sealed record JoinedRoomResponse(string Code, string PlayerToken);
}
