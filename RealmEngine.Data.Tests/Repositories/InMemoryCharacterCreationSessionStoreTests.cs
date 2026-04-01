using FluentAssertions;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class InMemoryCharacterCreationSessionStoreTests
{
    private readonly InMemoryCharacterCreationSessionStore _store = new();

    [Fact]
    public async Task CreateSessionAsync_ReturnsNewSession_WithUniqueId()
    {
        var s1 = await _store.CreateSessionAsync();
        var s2 = await _store.CreateSessionAsync();

        s1.SessionId.Should().NotBeEmpty();
        s2.SessionId.Should().NotBeEmpty();
        s1.SessionId.Should().NotBe(s2.SessionId);
    }

    [Fact]
    public async Task CreateSessionAsync_InitialStatus_IsDraft()
    {
        var session = await _store.CreateSessionAsync();
        session.Status.Should().Be(CreationSessionStatus.Draft);
    }

    [Fact]
    public async Task GetSessionAsync_AfterCreate_ReturnsSession()
    {
        var created = await _store.CreateSessionAsync();

        var retrieved = await _store.GetSessionAsync(created.SessionId);

        retrieved.Should().NotBeNull();
        retrieved!.SessionId.Should().Be(created.SessionId);
    }

    [Fact]
    public async Task GetSessionAsync_UnknownId_ReturnsNull()
    {
        var result = await _store.GetSessionAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSessionAsync_PersistsChanges()
    {
        var session = await _store.CreateSessionAsync();
        session.CharacterName = "Hero";

        await _store.UpdateSessionAsync(session);

        var reloaded = await _store.GetSessionAsync(session.SessionId);
        reloaded!.CharacterName.Should().Be("Hero");
    }

    [Fact]
    public async Task UpdateSessionAsync_SetsLastUpdatedAt()
    {
        var session = await _store.CreateSessionAsync();
        var before  = DateTime.UtcNow;

        await _store.UpdateSessionAsync(session);

        var reloaded = await _store.GetSessionAsync(session.SessionId);
        reloaded!.LastUpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task RemoveSessionAsync_RemovesSession()
    {
        var session = await _store.CreateSessionAsync();

        await _store.RemoveSessionAsync(session.SessionId);

        var result = await _store.GetSessionAsync(session.SessionId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveSessionAsync_UnknownId_DoesNotThrow()
    {
        var act = async () => await _store.RemoveSessionAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
