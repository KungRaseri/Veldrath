using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Services;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the D4 reconnect mechanics: the ephemeral AFK tracker and the combat-session suspend/resume transitions.
/// </summary>
public class CombatReconnectTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    // ── AFK tracker ────────────────────────────────────────────────────────────

    [Fact]
    public void AfkTracker_Records_The_Message_And_Reports_It_Back()
    {
        var tracker = new AfkTracker();
        var characterId = Guid.NewGuid();

        tracker.Set(characterId, "brb");

        tracker.TryGet(characterId, out var message).Should().BeTrue();
        message.Should().Be("brb");
    }

    [Fact]
    public void AfkTracker_Clear_Reports_Whether_The_Character_Was_Away()
    {
        var tracker = new AfkTracker();
        var characterId = Guid.NewGuid();

        tracker.TryClear(characterId, out _).Should().BeFalse("a character who was never away cannot be cleared");

        tracker.Set(characterId, null);
        tracker.TryClear(characterId, out var message).Should().BeTrue();
        message.Should().BeNull("an AFK with no message is still AFK");
        tracker.TryGet(characterId, out _).Should().BeFalse("clearing removes the entry");
    }

    // ── Combat session suspend / resume ────────────────────────────────────────

    private static async Task<(EfCoreCombatSessionStore Store, ApplicationDbContext Db, Guid CharacterId, Guid EnemyId)>
        SeedSessionAsync(TestDbContextFactory factory)
    {
        var db = factory.CreateContext();

        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
        };
        var enemy = new EnemyInstance
        {
            ZoneGroup = "zone:test",
            LocationSlug = "square",
            ArchetypeSlug = "goblin",
            Name = "Goblin",
            CurrentHealth = 10,
            MaxHealth = 10,
        };
        db.Characters.Add(character);
        db.EnemyInstances.Add(enemy);
        await db.SaveChangesAsync();

        var store = new EfCoreCombatSessionStore(db);
        await store.SetAsync(
            character.Id,
            new ActiveCombatSession("zone:test", "square", enemy.Id, false, 2, DateTimeOffset.UtcNow),
            CancellationToken.None);

        return (store, db, character.Id, enemy.Id);
    }

    [Fact]
    public async Task MarkDisconnected_Then_MarkConnected_Keeps_The_Same_Fight()
    {
        var (store, db, characterId, enemyId) = await SeedSessionAsync(_dbFactory);
        await using var _ = db;

        (await store.MarkDisconnectedAsync(characterId, CancellationToken.None)).Should().BeTrue();

        var suspended = await db.CombatSessions.SingleAsync(s => s.CharacterId == characterId);
        suspended.DisconnectedAt.Should().NotBeNull("an unexpected drop starts the grace window");
        suspended.TurnCount.Should().Be(2, "the turn state survives the suspension");

        (await store.MarkConnectedAsync(characterId, CancellationToken.None)).Should().BeTrue();

        var resumed = await store.TryGetAsync(characterId, CancellationToken.None);
        resumed.Should().NotBeNull("the fight is still there to resume");
        resumed!.EnemyId.Should().Be(enemyId);
        (await db.CombatSessions.SingleAsync(s => s.CharacterId == characterId))
            .DisconnectedAt.Should().BeNull("reconnecting clears the suspension");
    }

    [Fact]
    public async Task MarkConnected_Reports_False_When_No_Session_Was_Suspended()
    {
        var (store, db, characterId, _) = await SeedSessionAsync(_dbFactory);
        await using var _ = db;

        (await store.MarkConnectedAsync(characterId, CancellationToken.None))
            .Should().BeFalse("a session that never disconnected needs no resume");
    }

    [Fact]
    public async Task PruneExpiredAsync_Only_Removes_Sessions_Disconnected_Before_The_Grace()
    {
        var (store, db, characterId, _) = await SeedSessionAsync(_dbFactory);
        await using var _ = db;

        await store.MarkDisconnectedAsync(characterId, CancellationToken.None);

        (await store.PruneExpiredAsync(TimeSpan.FromSeconds(60), CancellationToken.None))
            .Should().BeEmpty("a fresh suspension is still inside the grace window");
        (await store.IsInCombatAsync(characterId, CancellationToken.None)).Should().BeTrue();

        var row = await db.CombatSessions.SingleAsync(s => s.CharacterId == characterId);
        row.DisconnectedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        (await store.PruneExpiredAsync(TimeSpan.FromSeconds(60), CancellationToken.None))
            .Should().BeEquivalentTo([characterId]);
        (await store.IsInCombatAsync(characterId, CancellationToken.None))
            .Should().BeFalse("a suspension past the grace window ends the fight");
    }
}
