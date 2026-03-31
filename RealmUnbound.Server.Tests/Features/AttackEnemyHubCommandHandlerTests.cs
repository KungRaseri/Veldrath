using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters.Combat;
using RealmUnbound.Server.Hubs;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="AttackEnemyHubCommandHandler"/>.</summary>
public class AttackEnemyHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory          _dbFactory = new();
    private readonly List<(string Key, Guid EnemyId)> _enemyCleanup   = [];
    private readonly List<Guid>                       _sessionCleanup = [];

    private AttackEnemyHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IHubContext<GameHub>>(),
            NullLogger<AttackEnemyHubCommandHandler>.Instance);

    public void Dispose()
    {
        foreach (var (key, id) in _enemyCleanup)  ZoneLocationEnemyStore.RemoveEnemy(key, id);
        foreach (var id        in _sessionCleanup) CombatSessionStore.Remove(id);
        _dbFactory.Dispose();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string? attrsJson = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId  = account.Id,
            Name       = $"Char_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = attrsJson ?? "{}",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp, Guid charId)
    {
        var zoneGroup    = $"test/{Guid.NewGuid():N}";
        const string loc = "forest";
        var key          = ZoneLocationEnemyStore.MakeKey(zoneGroup, loc);
        var enemy        = new SpawnedEnemy
        {
            Name          = "Goblin",
            Level         = 1,
            CurrentHealth = hp,
            MaxHealth     = hp,
            BaseXp        = 10,
            GoldReward    = 5,
            ArchetypeSlug = "goblin",
        };
        enemy.Participants.Add(charId);
        enemy.DamageContributions[charId] = 0;
        ZoneLocationEnemyStore.AddEnemy(key, enemy);
        _enemyCleanup.Add((key, enemy.Id));
        return (zoneGroup, loc, enemy);
    }

    private void SetSession(Guid charId, string zoneGroup, string loc, Guid enemyId)
    {
        CombatSessionStore.Set(charId, new ActiveCombatSession(zoneGroup, loc, enemyId, false, 0, DateTimeOffset.UtcNow));
        _sessionCleanup.Add(charId);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_Not_In_Combat()
    {
        await using var db = _dbFactory.CreateContext();
        var charId = Guid.NewGuid();

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Enemy_Not_In_Store()
    {
        await using var db  = _dbFactory.CreateContext();
        var character       = await SeedCharacterAsync(db);
        var zoneGroup       = $"test/{Guid.NewGuid():N}";
        var enemyId         = Guid.NewGuid();

        CombatSessionStore.Set(character.Id, new ActiveCombatSession(zoneGroup, "loc", enemyId, false, 0, DateTimeOffset.UtcNow));
        _sessionCleanup.Add(character.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task Handle_Reduces_Enemy_Health_On_Normal_Attack()
    {
        await using var db  = _dbFactory.CreateContext();
        var character       = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy(100, character.Id);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeFalse();
        result.PlayerDamage.Should().BeGreaterThan(0);
        enemy.CurrentHealth.Should().BeLessThan(100);
    }

    [Fact]
    public async Task Handle_Returns_EnemyDefeated_When_Enemy_Killed()
    {
        await using var db  = _dbFactory.CreateContext();
        var character       = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy(1, character.Id); // 1 HP — will die from any hit
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeTrue();
        result.EnemyRemainingHealth.Should().Be(0);
        CombatSessionStore.IsInCombat(character.Id).Should().BeFalse();
    }
}
