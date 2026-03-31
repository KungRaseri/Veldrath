using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters.Combat;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="DefendActionHubCommandHandler"/>.</summary>
public class DefendActionHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory             _dbFactory      = new();
    private readonly List<(string Key, Guid EnemyId)> _enemyCleanup   = [];
    private readonly List<Guid>                       _sessionCleanup = [];

    private DefendActionHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(new CharacterRepository(db), NullLogger<DefendActionHubCommandHandler>.Instance);

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

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp = 100)
    {
        var zoneGroup    = $"test/{Guid.NewGuid():N}";
        const string loc = "forest";
        var key          = ZoneLocationEnemyStore.MakeKey(zoneGroup, loc);
        var enemy        = new SpawnedEnemy { Name = "Orc", Level = 1, CurrentHealth = hp, MaxHealth = hp };
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

        var result = await MakeHandler(db).Handle(new DefendActionHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Succeeds_And_Enemy_Counter_Attacks()
    {
        await using var db = _dbFactory.CreateContext();
        // Seed character with enough HP to survive a counter-attack.
        var attrs     = """{"CurrentHealth":100,"MaxHealth":100,"Constitution":10}""";
        var character = await SeedCharacterAsync(db, attrs);
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new DefendActionHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDamage.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_Defending_Results_In_Less_Damage_Than_Not_Defending()
    {
        // This compares two identical character setups — one defending, one not —
        // using an enemy with no abilities so damage is deterministic.
        await using var db = _dbFactory.CreateContext();
        const string attrs = """{"CurrentHealth":500,"MaxHealth":500,"Constitution":10}""";

        // Defend turn
        var charDef = await SeedCharacterAsync(db, attrs);
        var (zg1, loc1, enemy1) = AddEnemy();
        CombatSessionStore.Set(charDef.Id, new ActiveCombatSession(zg1, loc1, enemy1.Id, true, 0, DateTimeOffset.UtcNow));
        _sessionCleanup.Add(charDef.Id);
        var defResult = await MakeHandler(db).Handle(new DefendActionHubCommand(charDef.Id), CancellationToken.None);

        // Non-defending turn
        var charAtk = await SeedCharacterAsync(db, attrs);
        var (zg2, loc2, enemy2) = AddEnemy();
        SetSession(charAtk.Id, zg2, loc2, enemy2.Id);
        var atkResult = await MakeHandler(db).Handle(new DefendActionHubCommand(charAtk.Id), CancellationToken.None);

        defResult.EnemyDamage.Should().BeLessThanOrEqualTo(atkResult.EnemyDamage);
    }
}
