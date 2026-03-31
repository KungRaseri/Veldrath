using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters.Combat;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="FleeFromCombatHubCommandHandler"/>.</summary>
public class FleeFromCombatHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory              _dbFactory      = new();
    private readonly List<(string Key, Guid EnemyId)> _enemyCleanup   = [];
    private readonly List<Guid>                        _sessionCleanup = [];

    private FleeFromCombatHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(new CharacterRepository(db), NullLogger<FleeFromCombatHubCommandHandler>.Instance);

    public void Dispose()
    {
        foreach (var (key, id) in _enemyCleanup)  ZoneLocationEnemyStore.RemoveEnemy(key, id);
        foreach (var id        in _sessionCleanup) CombatSessionStore.Remove(id);
        _dbFactory.Dispose();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db)
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
            Attributes = """{"CurrentHealth":100,"MaxHealth":100}""",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy()
    {
        var zoneGroup    = $"test/{Guid.NewGuid():N}";
        const string loc = "forest";
        var key          = ZoneLocationEnemyStore.MakeKey(zoneGroup, loc);
        var enemy        = new SpawnedEnemy { Name = "Orc", Level = 1, CurrentHealth = 100, MaxHealth = 100 };
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

        var result = await MakeHandler(db).Handle(new FleeFromCombatHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Returns_Valid_Result_When_In_Combat()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new FleeFromCombatHubCommand(character.Id), CancellationToken.None);

        // Regardless of the RNG outcome, the handler must return a valid response.
        result.Success.Should().BeTrue();
        if (result.Fled)
        {
            CombatSessionStore.IsInCombat(character.Id).Should().BeFalse();
        }
        else
        {
            result.ErrorMessage.Should().BeNullOrEmpty();
        }
    }
}
