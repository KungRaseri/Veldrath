using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Tests for <see cref="InMemoryCharacterCreationSessionStore.EvictExpiredSessions"/>: idle sessions must be dropped
/// so a long-running server does not accumulate abandoned character-creation drafts.
/// </summary>
/// <remarks>
/// The store keeps the session instances it hands out, so a test can age a session by writing
/// <see cref="RealmEngine.Shared.Models.CharacterCreationSession.LastUpdatedAt"/> directly. That makes the
/// stale/fresh separation explicit instead of relying on two clock reads landing in the same tick.
/// </remarks>
[Trait("Category", "Repository")]
public class InMemoryCharacterCreationSessionEvictionTests
{
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Fact]
    public async Task EvictExpiredSessions_RemovesSessionsIdleBeyondTheLimit()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var stale = await store.CreateSessionAsync();
        var fresh = await store.CreateSessionAsync();
        stale.LastUpdatedAt = DateTime.UtcNow - TimeSpan.FromHours(2);

        // A one-hour window: the two-hour-old session is stale, the just-created one is not.
        var evicted = store.EvictExpiredSessions(OneHour);

        evicted.Should().Be(1);
        (await store.GetSessionAsync(stale.SessionId)).Should().BeNull();
        (await store.GetSessionAsync(fresh.SessionId)).Should().NotBeNull(
            "a session updated inside the window survives the sweep");
    }

    [Fact]
    public async Task EvictExpiredSessions_KeepsSessionsWithinTheWindow()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var session = await store.CreateSessionAsync();
        session.LastUpdatedAt = DateTime.UtcNow - TimeSpan.FromMinutes(10);

        var evicted = store.EvictExpiredSessions(OneHour);

        evicted.Should().Be(0);
        (await store.GetSessionAsync(session.SessionId)).Should().NotBeNull();
    }

    [Fact]
    public async Task EvictExpiredSessions_LeavesSessionsCreatedAfterAnEvictionSweep()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var stale = await store.CreateSessionAsync();
        stale.LastUpdatedAt = DateTime.UtcNow - TimeSpan.FromHours(3);

        store.EvictExpiredSessions(OneHour).Should().Be(1);

        var survivor = await store.CreateSessionAsync();
        var evicted = store.EvictExpiredSessions(OneHour);

        evicted.Should().Be(0, "a session created after the sweep has a current timestamp");
        (await store.GetSessionAsync(survivor.SessionId)).Should().NotBeNull();
        (await store.GetSessionAsync(stale.SessionId)).Should().BeNull();
    }

    [Fact]
    public async Task EvictExpiredSessions_UsesAStrictComparison_SoASessionUpdatedAfterTheCutoffSurvives()
    {
        var store = new InMemoryCharacterCreationSessionStore();
        var session = await store.CreateSessionAsync();

        // A future stamp is the only stable way to pin the strict "<" comparison: a session at or after
        // the cutoff must survive even a zero window.
        session.LastUpdatedAt = DateTime.UtcNow + TimeSpan.FromMinutes(1);

        store.EvictExpiredSessions(TimeSpan.Zero).Should().Be(0);
        (await store.GetSessionAsync(session.SessionId)).Should().NotBeNull();
    }
}
