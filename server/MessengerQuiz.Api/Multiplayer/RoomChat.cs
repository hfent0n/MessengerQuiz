namespace MessengerQuiz.Api.Multiplayer;

// A chat message between players. The sender's browser seals it with the game link's key, so the server passes
// it on without being able to read it. The box is labelled with the sender's player ID, so the server can't pass
// it off as someone else's.
public sealed record ChatLine(int Id, string PlayerId, string Sealed);

public static class QuickReactions
{
    // The only reactions accepted, so nothing else can be sent through them.
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string> { "😂", "🔥", "😮", "😭", "👏", "🤔" };
}

// Allows up to a number of actions in a sliding window of time, so one player can't flood the others.
public sealed class ActionLimiter(int maxActions, TimeSpan window)
{
    private readonly Queue<DateTimeOffset> _recent = new();

    // Callers hold the room's lock.
    public bool TryTake(DateTimeOffset now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= window)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= maxActions)
        {
            return false;
        }

        _recent.Enqueue(now);
        return true;
    }
}
