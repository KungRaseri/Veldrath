using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Lifecycle coverage for <see cref="EfCoreEnemyInstanceStore"/>: the proactive respawn sweep, the "zero health but
/// still flagged alive" repair, and the missing-row paths of save / remove / lookup. These are the recovery paths —
/// the ones that decide whether the world heals itself after a crash or a rolling deploy.
/// </summary>
public class EfCoreEnemyInstanceStoreLifecycleTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static EfCoreEnemyInstanceStore CreateStore(ApplicationDbContext db) =>
        new(
            db,
            new ActorPoolResolver(new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance)),
            Options.Create(new EnemyWorldOptions { RespawnDelaySeconds = 60 }),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

    private static SpawnedEnemy Enemy(string name = "Goblin", int hp = 40) => new()
    {
        ArchetypeSlug = "goblin",
        Name = name,
        Level = 3,
        CurrentHealth = hp,
        MaxHealth = hp,
        BaseXp = 10,
        GoldReward = 5,
    };

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db)
    {
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
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static async Task<SpawnedEnemy> SpawnAsync(
        EfCoreEnemyInstanceStore store, string zoneGroup = "zone:test", string location = "square")
    {
        var roster = await store.GetAliveOrSpawnAsync(
            zoneGroup,
            location,
            _ => Task.FromResult(new List<SpawnedEnemy> { Enemy() }));
        return roster[0];
    }

    /// <summary>Kills the enemy and moves its respawn timer into the past, as elapsed time would.</summary>
    private static async Task ExpireAsync(ApplicationDbContext db, Guid enemyId)
    {
        var row = await db.EnemyInstances.SingleAsync(e => e.Id == enemyId);
        row.RespawnAt = DateTimeOffset.UtcNow.AddSeconds(-5);
        await db.SaveChangesAsync();
    }

    // ── SweepExpiredRespawnsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task Sweep_NothingDue_ReturnsEmptyAndTouchesNothing()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store);

        var revived = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        revived.Should().BeEmpty("a living enemy is not a respawn candidate");
        (await store.TryGetAsync(enemy.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Sweep_RevivesADueEnemyAtFullHealthAndClearsItsParticipants()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store);
        var hero = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);

        enemy.Participants.Add(hero.Id);
        enemy.DamageContributions[hero.Id] = 15;
        enemy.CurrentHealth = 4;
        await store.SaveAsync(enemy, "zone:test", "square", CancellationToken.None);
        await store.TryMarkDeadAsync(enemy.Id, CancellationToken.None);
        await ExpireAsync(db, enemy.Id);

        var revived = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        revived.Should().ContainSingle();
        var evt = revived[0];
        evt.Id.Should().Be(enemy.Id);
        evt.Name.Should().Be("Goblin");
        evt.Level.Should().Be(3);
        evt.ZoneGroup.Should().Be("zone:test");
        evt.LocationSlug.Should().Be("square");
        evt.MaxHealth.Should().Be(enemy.MaxHealth);

        var reloaded = await store.TryGetAsync(enemy.Id);
        reloaded.Should().NotBeNull();
        reloaded!.CurrentHealth.Should().Be(enemy.MaxHealth, "a respawn comes back at full health");
        reloaded.Participants.Should().BeEmpty("a respawned enemy starts with a clean slate");

        db.EnemyParticipants.Count(p => p.EnemyInstanceId == enemy.Id).Should().Be(0);
        // The ally never engaged, so it must not have been touched either.
        db.Characters.Count(c => c.Id == ally.Id).Should().Be(1);
    }

    [Fact]
    public async Task Sweep_RespectsTheBatchSize()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var roster = await store.GetAliveOrSpawnAsync(
            "zone:batch",
            "square",
            _ => Task.FromResult(new List<SpawnedEnemy> { Enemy("A"), Enemy("B"), Enemy("C") }));
        foreach (var enemy in roster)
        {
            await store.TryMarkDeadAsync(enemy.Id, CancellationToken.None);
            await ExpireAsync(db, enemy.Id);
        }

        var firstBatch = await store.SweepExpiredRespawnsAsync(2, CancellationToken.None);
        var secondBatch = await store.SweepExpiredRespawnsAsync(2, CancellationToken.None);

        firstBatch.Should().HaveCount(2);
        secondBatch.Should().ContainSingle();
        (await store.SweepExpiredRespawnsAsync(2, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_LeavesEnemiesWhoseTimerHasNotElapsed()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store, "zone:soon", "square");
        await store.TryMarkDeadAsync(enemy.Id, CancellationToken.None);

        var revived = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        revived.Should().BeEmpty();
        db.ChangeTracker.Clear();
        var stillDead = await db.EnemyInstances.AsNoTracking().SingleAsync(e => e.Id == enemy.Id);
        stillDead.IsAlive.Should().BeFalse("the timer has not elapsed, so the enemy stays down");
        stillDead.RespawnAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Sweep_DoesNotReviveEnemiesTwice()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store, "zone:once", "square");
        await store.TryMarkDeadAsync(enemy.Id, CancellationToken.None);
        await ExpireAsync(db, enemy.Id);

        var first = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);
        var second = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        first.Should().ContainSingle();
        second.Should().BeEmpty();
    }

    // ── SettleLocationAsync repair path ───────────────────────────────────────

    [Fact]
    public async Task GetAliveOrSpawn_MarksAZeroHealthEnemyDeadInsteadOfServingIt()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store, "zone:broken", "square");

        // A crash between persisting health and flipping the death flag leaves exactly this state.
        var row = await db.EnemyInstances.SingleAsync(e => e.Id == enemy.Id);
        row.CurrentHealth = 0;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var roster = await store.GetAliveOrSpawnAsync(
            "zone:broken",
            "square",
            _ => Task.FromResult(new List<SpawnedEnemy>()));

        roster.Should().BeEmpty("a zero-health enemy must not be served as alive");
        var repaired = await db.EnemyInstances.AsNoTracking().SingleAsync(e => e.Id == enemy.Id);
        repaired.IsAlive.Should().BeFalse();
        repaired.RespawnAt.Should().NotBeNull("the repair schedules the respawn it would have had");
    }

    [Fact]
    public async Task GetAliveOrSpawn_WithAnEmptyRosterPersistsNothing()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);

        var roster = await store.GetAliveOrSpawnAsync(
            "zone:empty", "square", _ => Task.FromResult(new List<SpawnedEnemy>()));

        roster.Should().BeEmpty();
        db.EnemyInstances.Count(e => e.ZoneGroup == "zone:empty").Should().Be(0);
    }

    // ── Missing-row paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task TryGetAsync_UnknownEnemy_ReturnsNull()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);

        (await store.TryGetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task TryMarkDeadAsync_UnknownEnemy_ReportsNoWin()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);

        var won = await store.TryMarkDeadAsync(Guid.NewGuid(), CancellationToken.None);

        won.Should().BeFalse("nobody can claim a kill on a row that does not exist");
    }

    [Fact]
    public async Task SaveAsync_DeletedRow_IsANoOpInsteadOfAnException()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store, "zone:gone", "square");
        await store.RemoveAsync(enemy.Id, CancellationToken.None);

        enemy.CurrentHealth = 1;
        Func<Task> act = () => store.SaveAsync(enemy, "zone:gone", "square", CancellationToken.None);

        await act.Should().NotThrowAsync();
        db.EnemyInstances.Count(e => e.Id == enemy.Id).Should().Be(0);
    }

    [Fact]
    public async Task RemoveAsync_UnknownEnemy_IsANoOp()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);

        var act = () => store.RemoveAsync(Guid.NewGuid(), CancellationToken.None);

        await FluentActions.Awaiting(act).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_RecordsAParticipantWhoIsOnlyKnownFromDamageContributions()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SpawnAsync(store, "zone:damage", "square");
        var latecomer = await SeedCharacterAsync(db);

        enemy.DamageContributions[latecomer.Id] = 9;
        await store.SaveAsync(enemy, "zone:damage", "square", CancellationToken.None);

        var reloaded = await store.TryGetAsync(enemy.Id);
        reloaded!.Participants.Should().Contain(latecomer.Id);
        reloaded.DamageContributions[latecomer.Id].Should().Be(9);
    }
}
