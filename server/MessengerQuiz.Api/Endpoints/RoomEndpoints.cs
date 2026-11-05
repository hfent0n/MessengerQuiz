using MessengerQuiz.Api.Multiplayer;
using MessengerQuiz.Api.Storage;
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
    }

    // The host's chat is identified by its ID, as for questions.
    private static IResult CreateRoom(HttpContext context, CreateRoomRequest request, ChatStore chats, GameService games)
    {
        var chatId = context.GetChatId();
        if (!chats.TryGet(chatId, out var session) || session is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "This chat has expired. Upload it again to keep playing.");
        }

        return games.CreateRoom(chatId, session, request.Name, request.TimeLimitSeconds, out var room, out var host) switch
        {
            CreateOutcome.InvalidName => Problem(StatusCodes.Status400BadRequest, InvalidNameMessage),
            CreateOutcome.InvalidTimeLimit => Problem(StatusCodes.Status400BadRequest, "Choose one of the time limits."),
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

    private sealed record CreateRoomRequest(string? Name, int TimeLimitSeconds);

    private sealed record JoinRoomRequest(string? Name);

    private sealed record JoinedRoomResponse(string Code, string PlayerToken);
}
