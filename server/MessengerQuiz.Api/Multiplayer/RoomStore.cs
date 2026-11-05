using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MessengerQuiz.Api.Multiplayer;

public sealed class RoomStore
{
    // No 0/o, 1/l/i or u, so codes are easy to read out. 10 characters give about 49 bits.
    private const string CodeAlphabet = "23456789abcdefghjkmnpqrstvwxyz";
    private const int CodeLength = 10;

    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public static string NewCode() => RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string NewPlayerId() => RandomNumberGenerator.GetString(CodeAlphabet, 8);

    public bool TryAdd(Room room) => _rooms.TryAdd(room.Code, room);

    public Room? Find(string code) => _rooms.GetValueOrDefault(code.Trim().ToLowerInvariant());

    public IReadOnlyList<Room> ForChat(string chatId) => _rooms.Values.Where(room => room.ChatId == chatId).ToList();

    public void Remove(Room room) => _rooms.TryRemove(room.Code, out _);
}
