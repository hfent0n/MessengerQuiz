using Microsoft.AspNetCore.SignalR;

namespace MessengerQuiz.Api.Multiplayer;

// Players call these after joining. Each returns an error to show, or null. Updates arrive as "room" events.
public sealed class GameHub(GameService games) : Hub
{
    public const string Path = "/api/rooms/hub";

    // The token is sent here rather than in the URL, which can reach logs.
    public Task<string?> Connect(string code, string token) => games.ConnectAsync(code, token, Context.ConnectionId);

    public Task<string?> Start() => games.StartAsync(Context.ConnectionId);

    public Task<string?> Answer(int option) => games.AnswerAsync(Context.ConnectionId, option);

    public Task<string?> Ready() => games.ReadyAsync(Context.ConnectionId);

    public Task<string?> Chat(string sealedMessage) => games.ChatAsync(Context.ConnectionId, sealedMessage);

    public Task<string?> React(string emoji) => games.ReactAsync(Context.ConnectionId, emoji);

    public override Task OnDisconnectedAsync(Exception? exception) => games.DisconnectAsync(Context.ConnectionId);
}
