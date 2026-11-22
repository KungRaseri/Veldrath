using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the one-off reconciliation of enemy rows written before the archetype stats were projected onto
/// spawned enemies. Those rows carry the flat template defaults (50 HP, 25 XP, 10 gold) and every read, save and
/// respawn takes the stored numbers, so without this pass they would fight flat for the rest of their lives.
/// </summary>
public class EnemyInstanceStatsReconciliationTests : IDisposable
{
    private const string ZoneGroup = "zone:dark-forest_normal";
    private const string LocationSlug = "wolf-clearing";
    private const string ArchetypeSlug = "giant-rat";

    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    /// <summary>The archetype the seeded catalog projects for a band-1 giant rat, as E1 makes it reach the template.</summary>
    private static Enemy Archetype(
        int maxHealth = 120,
        int xp = 80,
        int goldReward = 40) => new()
        {
            Id = ArchetypeSlug,
            Slug = ArchetypeSlug,
            Name = "Giant Rat",
            Level = 1,
            Health = maxHealth,
            MaxHealth = maxHealth,
            XP = xp,
            GoldReward = goldReward,
        };

    private static EfCoreEnemyInstanceStore CreateStore(ApplicationDbContext db, Enemy? archetype) =>
        new(
            db,
            new ActorPoolResolver(new EnemyGenerator(Repository(archetype), NullLogger<EnemyGenerator>.Instance)),
            Options.Create(new EnemyWorldOptions()),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

    private static IEnemyRepository Repository(Enemy? archetype)
    {
        var repository = new Mock<IEnemyRepository>();
        repository
            .Setup(r => r.GetBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync(archetype);
        return repository.Object;
    }

    /// <summary>
    /// A row exactly as the pre-projection store wrote it: the flat template defaults, and
    /// <see cref="EnemyInstance.StatsVersion"/> left at the migration's default of zero.
    /// </summary>
    private static EnemyInstance LegacyRow(
        bool alive,
        DateTimeOffset? respawnAt = null,
        int currentHealth = 50,
        int spawnedSecondsAgo = 600) => new()
        {
            ZoneGroup = ZoneGroup,
            LocationSlug = LocationSlug,
            ArchetypeSlug = ArchetypeSlug,
            Name = "Giant Rat",
            Level = 1,
            CurrentHealth = alive ? currentHealth : 0,
            MaxHealth = 50,
            BaseXp = 25,
            GoldReward = 10,
            IsAlive = alive,
            SpawnedAt = DateTimeOffset.UtcNow.AddSeconds(-spawnedSecondsAgo),
            RespawnAt = respawnAt,
            Version = 1,
        };

    private static async Task<EnemyInstance> SeedAsync(ApplicationDbContext db, EnemyInstance row)
    {
        db.EnemyInstances.Add(row);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return row;
    }

    private static Task<EnemyInstance> ReloadAsync(ApplicationDbContext db, Guid id) =>
        db.EnemyInstances.AsNoTracking().SingleAsync(e => e.Id == id);

    [Fact]
    public async Task Reconciliation_RebuildsADeadLegacyRowFromTheArchetype()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: false, respawnAt: DateTimeOffset.UtcNow.AddMinutes(5)));
        var store = CreateStore(db, Archetype());

        var roster = await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        roster.Should().BeEmpty("the only row is dead and its timer has not elapsed");

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.MaxHealth.Should().Be(120);
        reloaded.BaseXp.Should().Be(80);
        reloaded.GoldReward.Should().Be(40, "a dead row is between fights, so its reward is safe to re-roll");
        reloaded.StatsVersion.Should().Be(EnemyInstance.CurrentStatsVersion);
        reloaded.CurrentHealth.Should().Be(0, "a dead row stays dead until its respawn timer elapses");
        reloaded.IsAlive.Should().BeFalse();
    }

    [Fact]
    public async Task Reconciliation_RevivesADueLegacyRowAtTheArchetypesMaximumHealth()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: false, respawnAt: DateTimeOffset.UtcNow.AddSeconds(-1)));
        var store = CreateStore(db, Archetype());

        var roster = await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        roster.Should().ContainSingle();
        roster[0].MaxHealth.Should().Be(120, "the row was reconciled before the respawn revived it");
        roster[0].CurrentHealth.Should().Be(120);
        roster[0].BaseXp.Should().Be(80);
        roster[0].GoldReward.Should().Be(40);

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.IsAlive.Should().BeTrue();
        reloaded.StatsVersion.Should().Be(EnemyInstance.CurrentStatsVersion);
    }

    [Fact]
    public async Task Reconciliation_DoesNotRerollALivingRowsRewardMidFight()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: true));
        var store = CreateStore(db, Archetype());

        await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var living = await ReloadAsync(db, row.Id);
        living.MaxHealth.Should().Be(120);
        living.BaseXp.Should().Be(80);
        living.CurrentHealth.Should().Be(50, "current health is clamped to the new maximum, not scaled up");
        living.GoldReward.Should().Be(10, "the column is what the kill path awards, so a living row keeps it");
        living.StatsVersion.Should().Be(0, "the reward still owes a refresh, which happens once the row is dead");

        // The enemy is killed: it is now between fights, so the next read refreshes the reward and stamps the row.
        await store.TryMarkDeadAsync(row.Id);
        await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var dead = await ReloadAsync(db, row.Id);
        dead.GoldReward.Should().Be(40);
        dead.StatsVersion.Should().Be(EnemyInstance.CurrentStatsVersion);
    }

    [Fact]
    public async Task Reconciliation_ClampsCurrentHealthWhenTheArchetypeShrinks()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: true, currentHealth: 50));
        var store = CreateStore(db, Archetype(maxHealth: 20, xp: 8, goldReward: 2));

        await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.MaxHealth.Should().Be(20);
        reloaded.CurrentHealth.Should().Be(20, "current health may never exceed the new maximum");
        reloaded.BaseXp.Should().Be(8);
        reloaded.GoldReward.Should().Be(10, "still alive, so the awarded value is untouched");
    }

    [Fact]
    public async Task SweepExpiredRespawnsAsync_RevivesALegacyRowAtTheArchetypesMaximumHealth()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: false, respawnAt: DateTimeOffset.UtcNow.AddSeconds(-1)));
        var store = CreateStore(db, Archetype());

        var events = await store.SweepExpiredRespawnsAsync(batchSize: 10);

        events.Should().ContainSingle();
        events[0].MaxHealth.Should().Be(120, "the sweeper reconciles before it revives, so it cannot outrun the fix");
        events[0].CurrentHealth.Should().Be(120);

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.MaxHealth.Should().Be(120);
        reloaded.CurrentHealth.Should().Be(120);
        reloaded.BaseXp.Should().Be(80);
        reloaded.GoldReward.Should().Be(40);
        reloaded.StatsVersion.Should().Be(EnemyInstance.CurrentStatsVersion);
    }

    [Fact]
    public async Task RowsSpawnedAfterTheFix_AreStampedAndNeverReprojected()
    {
        await using var db = _dbFactory.CreateContext();
        var legacyStore = CreateStore(db, Archetype(maxHealth: 40, xp: 10, goldReward: 5));

        await legacyStore.GetAliveOrSpawnAsync(
            ZoneGroup,
            LocationSlug,
            _ => Task.FromResult(new List<SpawnedEnemy>
            {
                new()
                {
                    ArchetypeSlug = ArchetypeSlug,
                    Name = "Giant Rat",
                    Level = 1,
                    CurrentHealth = 40,
                    MaxHealth = 40,
                    BaseXp = 10,
                    GoldReward = 5,
                },
            }));

        var spawned = await db.EnemyInstances.AsNoTracking().SingleAsync();
        spawned.StatsVersion.Should().Be(
            EnemyInstance.CurrentStatsVersion,
            "a spawn is built from the projection, so there is nothing to reconcile");

        // Content is retuned afterwards. The reconciliation is one-off: a row that was born from the projection
        // keeps the stats it was born with, exactly like the other snapshot columns on the row.
        var retunedStore = CreateStore(db, Archetype(maxHealth: 120, xp: 80, goldReward: 40));
        await retunedStore.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var reloaded = await ReloadAsync(db, spawned.Id);
        reloaded.MaxHealth.Should().Be(40);
        reloaded.BaseXp.Should().Be(10);
        reloaded.GoldReward.Should().Be(5);
    }

    [Fact]
    public async Task Reconciliation_UsesTheRealSeededCatalog()
    {
        // The acceptance case with no stub in the path: the shipping giant-rat authors 20 health, 8 XP and 1–3 gold,
        // while the legacy row carries the old flat 50 / 25 / 10.
        await using var content = new SeededContentProbe();
        await using var db = _dbFactory.CreateContext();

        var row = await SeedAsync(db, LegacyRow(alive: false, respawnAt: DateTimeOffset.UtcNow.AddSeconds(-1)));

        ContentDbContext seeded = await content.CreateContextAsync();
        var random = FakeRandomSource.Seeded(20260101);
        var store = new EfCoreEnemyInstanceStore(
            db,
            new ActorPoolResolver(
                new EnemyGenerator(
                    new EfCoreEnemyRepository(seeded, NullLogger<EfCoreEnemyRepository>.Instance, random),
                    NullLogger<EnemyGenerator>.Instance,
                    random),
                NullLogger<ActorPoolResolver>.Instance,
                random),
            Options.Create(new EnemyWorldOptions()),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

        await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.MaxHealth.Should().Be(20, "the archetype authors 20 health, not the 50 the legacy row stored");
        reloaded.CurrentHealth.Should().Be(20, "the revived row comes back at the authored maximum");
        reloaded.BaseXp.Should().Be(8);
        reloaded.GoldReward.Should().BeInRange(1, 3, "the archetype authors a 1–3 gold range");
        reloaded.StatsVersion.Should().Be(EnemyInstance.CurrentStatsVersion);
        reloaded.IsAlive.Should().BeTrue();
    }

    [Fact]
    public async Task Reconciliation_LeavesTheRowAloneWhenItsArchetypeIsGone()
    {
        await using var db = _dbFactory.CreateContext();
        var row = await SeedAsync(db, LegacyRow(alive: false, respawnAt: DateTimeOffset.UtcNow.AddMinutes(5)));
        var store = CreateStore(db, archetype: null);

        // Missing content is reported, never substituted: the pass must not throw or invent stats.
        await store.GetAliveOrSpawnAsync(ZoneGroup, LocationSlug, _ => Task.FromResult(new List<SpawnedEnemy>()));

        var reloaded = await ReloadAsync(db, row.Id);
        reloaded.MaxHealth.Should().Be(50);
        reloaded.BaseXp.Should().Be(25);
        reloaded.GoldReward.Should().Be(10);
        reloaded.StatsVersion.Should().Be(0, "it stays stale so the row is reconciled if the content comes back");
    }
}
