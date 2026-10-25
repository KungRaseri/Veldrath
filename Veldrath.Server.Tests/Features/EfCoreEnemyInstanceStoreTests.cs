using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="EfCoreEnemyInstanceStore"/> — the durable replacement for the deleted in-process
/// <c>ZoneLocationEnemyStore</c>.
/// </summary>
public class EfCoreEnemyInstanceStoreTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    private EfCoreEnemyInstanceStore CreateStore(ApplicationDbContext db) =>
        new(
            db,
            new ActorPoolResolver(new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance)),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    private static SpawnedEnemy Enemy(string name = "Goblin", int hp = 40) => new()
    {
        ArchetypeSlug = "goblin",
        Name = name,
        Level = 1,
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

    [Fact]
    public async Task GetAliveOrSpawnAsync_Spawns_Once_And_Reuses_The_Roster()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var spawns = 0;

        Task<List<SpawnedEnemy>> Spawn(CancellationToken _)
        {
            spawns++;
            return Task.FromResult(new List<SpawnedEnemy> { Enemy("A"), Enemy("B") });
        }

        var first = await store.GetAliveOrSpawnAsync("zone:test", "square", Spawn);
        var second = await store.GetAliveOrSpawnAsync("zone:test", "square", Spawn);

        spawns.Should().Be(1, "the stored roster must be reused instead of spawning again");
        first.Should().HaveCount(2);
        second.Select(e => e.Id).Should().BeEquivalentTo(first.Select(e => e.Id));
    }

    [Fact]
    public async Task SaveAsync_Persists_Health_And_Damage_For_Every_Contributor()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var first = await SeedEnemyAsync(store, "zone:a", "square");
        var hero = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);

        first.CurrentHealth = 25;
        first.Participants.Add(hero.Id);
        first.Participants.Add(ally.Id);
        first.DamageContributions[hero.Id] = 10;
        first.DamageContributions[ally.Id] = 5;

        await store.SaveAsync(first, "zone:a", "square", CancellationToken.None);

        var reloaded = await store.TryGetAsync(first.Id);
        reloaded.Should().NotBeNull();
        reloaded!.CurrentHealth.Should().Be(25);
        reloaded.Participants.Should().BeEquivalentTo([hero.Id, ally.Id]);
        reloaded.DamageContributions[hero.Id].Should().Be(10);
        reloaded.DamageContributions[ally.Id].Should().Be(5);
    }

    [Fact]
    public async Task SaveAsync_Drops_A_Character_Who_Left_The_Fight()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SeedEnemyAsync(store, "zone:b", "square");
        var stayer = await SeedCharacterAsync(db);
        var fleer = await SeedCharacterAsync(db);

        enemy.Participants.Add(stayer.Id);
        enemy.Participants.Add(fleer.Id);
        await store.SaveAsync(enemy, "zone:b", "square", CancellationToken.None);

        enemy.Participants.Remove(fleer.Id);
        await store.SaveAsync(enemy, "zone:b", "square", CancellationToken.None);

        var reloaded = await store.TryGetAsync(enemy.Id);
        reloaded!.Participants.Should().BeEquivalentTo([stayer.Id]);
    }

    [Fact]
    public async Task TryMarkDeadAsync_Only_Succeeds_Once_And_Hides_The_Enemy()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SeedEnemyAsync(store, "zone:c", "square");

        var firstWin = await store.TryMarkDeadAsync(enemy.Id, TimeSpan.FromMinutes(1), CancellationToken.None);
        var secondWin = await store.TryMarkDeadAsync(enemy.Id, TimeSpan.FromMinutes(1), CancellationToken.None);

        firstWin.Should().BeTrue("the killing blow must be able to award rewards");
        secondWin.Should().BeFalse("a second caller must not award the same kill again");

        var alive = await store.GetAliveOrSpawnAsync("zone:c", "square", _ => Task.FromResult(new List<SpawnedEnemy>()));
        alive.Should().BeEmpty("a dead enemy is not part of the living roster");
    }

    [Fact]
    public async Task GetAliveOrSpawnAsync_Revives_An_Enemy_Whose_Respawn_Time_Has_Passed()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SeedEnemyAsync(store, "zone:d", "square");
        var hero = await SeedCharacterAsync(db);

        enemy.Participants.Add(hero.Id);
        await store.SaveAsync(enemy, "zone:d", "square", CancellationToken.None);
        await store.TryMarkDeadAsync(enemy.Id, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Move the timer into the past, as a real elapsed respawn would.
        var row = await db.EnemyInstances.SingleAsync(e => e.Id == enemy.Id);
        row.RespawnAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        var alive = await store.GetAliveOrSpawnAsync("zone:d", "square", _ => Task.FromResult(new List<SpawnedEnemy>()));

        alive.Should().HaveCount(1);
        alive[0].CurrentHealth.Should().Be(enemy.MaxHealth, "a respawned enemy comes back at full health");
        alive[0].Participants.Should().BeEmpty("a respawned enemy starts with a clean slate");
    }

    [Fact]
    public async Task RemoveAsync_Deletes_The_Enemy()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SeedEnemyAsync(store, "zone:e", "square");

        await store.RemoveAsync(enemy.Id, CancellationToken.None);

        (await store.TryGetAsync(enemy.Id)).Should().BeNull();
    }

    private static async Task<SpawnedEnemy> SeedEnemyAsync(
        EfCoreEnemyInstanceStore store,
        string zoneGroup,
        string locationSlug)
    {
        var roster = await store.GetAliveOrSpawnAsync(
            zoneGroup,
            locationSlug,
            _ => Task.FromResult(new List<SpawnedEnemy> { Enemy() }));

        return roster[0];
    }
}
