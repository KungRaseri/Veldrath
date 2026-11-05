using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Tests for <see cref="InMemoryCharacterCreationSessionStore.EvictExpiredSessions"/>: idle sessions must be dropped
/// so a long-running server does not accumulate abandoned character-creation drafts.
/// </summary>
[Trait("Category", "Repository")]
public class InMemoryCharacterCreationSessionEvictionTests
{
    [Fact]
    public async Task EvictExpiredSessions_RemovesSessionsIdleBeyondTheLimit()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var stale = await store.CreateSessionAsync();
        var fresh = await store.CreateSessionAsync();

        // Nothing has been touched yet, so a zero idle window makes every session stale.
        var evicted = store.EvictExpiredSessions(TimeSpan.Zero);

        evicted.Should().Be(2);
        (await store.GetSessionAsync(stale.SessionId)).Should().BeNull();
        (await store.GetSessionAsync(fresh.SessionId)).Should().BeNull();
    }

    [Fact]
    public async Task EvictExpiredSessions_KeepsSessionsWithinTheWindow()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var session = await store.CreateSessionAsync();

        var evicted = store.EvictExpiredSessions(TimeSpan.FromHours(1));

        evicted.Should().Be(0);
        (await store.GetSessionAsync(session.SessionId)).Should().NotBeNull();
    }

    [Fact]
    public async Task EvictExpiredSessions_LeavesSessionsCreatedAfterAnEvictionSweep()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        await store.CreateSessionAsync();
        store.EvictExpiredSessions(TimeSpan.Zero).Should().Be(1);

        var survivor = await store.CreateSessionAsync();
        var evicted = store.EvictExpiredSessions(TimeSpan.FromHours(1));

        evicted.Should().Be(0);
        (await store.GetSessionAsync(survivor.SessionId)).Should().NotBeNull();
    }
}
