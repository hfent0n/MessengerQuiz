using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Api.Storage;

public sealed class ChatStore(IOptions<QuizOptions> options) : IDisposable
{
    private readonly QuizOptions _options = options.Value;
    // Raised with the chat's ID when it's wiped or expires, so games using it can end.
    public event Action<string>? Removed;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = options.Value.ChatMemoryCapBytes });

    // Returns false when the memory cap is reached.
    public bool TryAdd(ChatSession session)
    {
        _cache.Set(session.Id, session, new MemoryCacheEntryOptions
        {
            Size = session.EstimatedBytes,
            SlidingExpiration = _options.ChatExpiry,
            // Stops the cache evicting active players' chats to make room for a new upload.
            Priority = CacheItemPriority.NeverRemove,
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, _, reason, _) =>
                    {
                        // Replaced means the same chat was stored again, so it hasn't gone.
                        if (reason != EvictionReason.Replaced)
                        {
                            Removed?.Invoke((string)key);
                        }
                    },
                },
            },
        });

        return _cache.TryGetValue(session.Id, out _);
    }

    public bool TryGet(string chatId, out ChatSession? session) => _cache.TryGetValue(chatId, out session);

    public void Remove(string chatId) => _cache.Remove(chatId);

    // The cache only removes expired entries when it's accessed, so an idle server needs a nudge.
    public void RemoveExpired() => _cache.Compact(0);

    public void Dispose() => _cache.Dispose();
}
