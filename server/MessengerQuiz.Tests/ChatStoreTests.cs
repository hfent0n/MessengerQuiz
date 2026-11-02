using MessengerQuiz.Api;
using MessengerQuiz.Api.Domain;
using MessengerQuiz.Api.Storage;
using Microsoft.Extensions.Options;

namespace MessengerQuiz.Tests;

public class ChatStoreTests
{
    [Fact]
    public void StoresAndRemovesChats()
    {
        using var store = Store();
        var session = Session();

        Assert.True(store.TryAdd(session));
        Assert.True(store.TryGet(session.Id, out var found));
        Assert.Same(session, found);

        store.Remove(session.Id);

        Assert.False(store.TryGet(session.Id, out _));
    }

    [Fact]
    public void RefusesChatsOverTheMemoryCap()
    {
        var first = Session();
        using var store = Store(capBytes: first.EstimatedBytes);

        Assert.True(store.TryAdd(first));
        Assert.False(store.TryAdd(Session()));
    }

    [Fact]
    public void ReachingTheCapDoesNotEvictExistingChats()
    {
        var first = Session();
        using var store = Store(capBytes: first.EstimatedBytes);

        store.TryAdd(first);
        store.TryAdd(Session());
        store.RemoveExpired();

        Assert.True(store.TryGet(first.Id, out _));
    }

    [Fact]
    public async Task ChatsExpireWhenIdle()
    {
        using var store = Store(expiry: TimeSpan.FromMilliseconds(20));
        var session = Session();
        store.TryAdd(session);

        await Task.Delay(100);
        store.RemoveExpired();

        Assert.False(store.TryGet(session.Id, out _));
    }

    private static ChatStore Store(long capBytes = 1_000_000, TimeSpan? expiry = null)
    {
        return new ChatStore(Options.Create(new QuizOptions
        {
            ChatMemoryCapBytes = capBytes,
            ChatExpiry = expiry ?? TimeSpan.FromHours(1),
        }));
    }

    private static ChatSession Session()
    {
        var message = new Message("m_0", "Ana", "A message", null, [], false);
        return new ChatSession(new Chat("Test", ["Ana"], [message]), [message]);
    }
}
