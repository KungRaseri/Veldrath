using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="EfCoreCombatSessionStore"/> — the durable replacement for the deleted in-process
/// <c>CombatSessionStore</c>.
/// </summary>
public class EfCoreCombatSessionStoreTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    private static EfCoreCombatSessionStore CreateStore(ApplicationDbContext db) => new(db);

    public void Dispose() => _dbFactory.Dispose();

    private static ActiveCombatSession Session(Guid enemyId, int turn = 0) =>
        new("zone:test", "square", enemyId, false, turn, DateTimeOffset.UtcNow);

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

    private static async Task<EnemyInstance> SeedEnemyAsync(ApplicationDbContext db)
    {
        var enemy = new EnemyInstance
        {
            ZoneGroup = "zone:test",
            LocationSlug = "square",
            ArchetypeSlug = "goblin",
            Name = "Goblin",
            Level = 1,
            CurrentHealth = 20,
            MaxHealth = 20,
            BaseXp = 5,
            GoldReward = 1,
        };
        db.EnemyInstances.Add(enemy);
        await db.SaveChangesAsync();
        return enemy;
    }

    [Fact]
    public async Task SetAsync_Then_TryGetAsync_Round_Trips_The_Turn_State()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var character = await SeedCharacterAsync(db);
        var enemy = await SeedEnemyAsync(db);

        await store.SetAsync(character.Id, Session(enemy.Id, turn: 3), CancellationToken.None);

        var session = await store.TryGetAsync(character.Id, CancellationToken.None);
        session.Should().NotBeNull();
        session!.EnemyId.Should().Be(enemy.Id);
        session.TurnCount.Should().Be(3);
        session.ZoneGroup.Should().Be("zone:test");
        (await store.IsInCombatAsync(character.Id, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_Replaces_An_Existing_Session()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var character = await SeedCharacterAsync(db);
        var enemy = await SeedEnemyAsync(db);

        await store.SetAsync(character.Id, Session(enemy.Id), CancellationToken.None);
        await store.SetAsync(character.Id, Session(enemy.Id, turn: 4), CancellationToken.None);

        var session = await store.TryGetAsync(character.Id, CancellationToken.None);
        session!.TurnCount.Should().Be(4, "one fight per character means the row is replaced, not duplicated");
        db.CombatSessions.Count().Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_Ends_The_Session()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var character = await SeedCharacterAsync(db);
        var enemy = await SeedEnemyAsync(db);

        await store.SetAsync(character.Id, Session(enemy.Id), CancellationToken.None);
        await store.RemoveAsync(character.Id, CancellationToken.None);

        (await store.IsInCombatAsync(character.Id, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveByEnemyIdAsync_Releases_Every_Engaged_Player_And_Nobody_Else()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateStore(db);
        var enemy = await SeedEnemyAsync(db);
        var otherEnemy = await SeedEnemyAsync(db);
        var engagedA = await SeedCharacterAsync(db);
        var engagedB = await SeedCharacterAsync(db);
        var bystander = await SeedCharacterAsync(db);

        await store.SetAsync(engagedA.Id, Session(enemy.Id), CancellationToken.None);
        await store.SetAsync(engagedB.Id, Session(enemy.Id), CancellationToken.None);
        await store.SetAsync(bystander.Id, Session(otherEnemy.Id), CancellationToken.None);

        var released = await store.RemoveByEnemyIdAsync(enemy.Id, CancellationToken.None);

        released.Should().BeEquivalentTo([engagedA.Id, engagedB.Id]);
        (await store.IsInCombatAsync(bystander.Id, CancellationToken.None))
            .Should().BeTrue("a player fighting a different enemy keeps their session");
    }
}
