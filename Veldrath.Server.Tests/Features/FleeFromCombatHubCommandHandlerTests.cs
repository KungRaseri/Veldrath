using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="FleeFromCombatHubCommandHandler"/>.</summary>
public class FleeFromCombatHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();

    private FleeFromCombatHubCommandHandler MakeHandler(ApplicationDbContext db, IRandomSource? randomSource = null) =>
        new(
            new CharacterRepository(db),
            _enemies,
            _sessions,
            Options.Create(new DeathPenaltyOptions()),
            NullLogger<FleeFromCombatHubCommandHandler>.Instance,
            randomSource ?? FakeRandomSource.Seeded(20260101));

    public void Dispose() => _dbFactory.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

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
            Attributes = """{"CurrentHealth":100,"MaxHealth":100}""",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy()
    {
        var zoneGroup = $"test/{Guid.NewGuid():N}";
        const string loc = "forest";
        var enemy = new SpawnedEnemy { Name = "Orc", Level = 1, CurrentHealth = 100, MaxHealth = 100 };
        _enemies.Add(zoneGroup, loc, enemy);
        return (zoneGroup, loc, enemy);
    }

    private void SetSession(Guid charId, string zoneGroup, string loc, Guid enemyId)
    {
        _sessions.Set(charId, new ActiveCombatSession(zoneGroup, loc, enemyId, false, 0, DateTimeOffset.UtcNow));
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
    public async Task Handle_Ends_Combat_When_The_Flee_Roll_Succeeds()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        // Roll 0 is below the 50% flee chance.
        var result = await MakeHandler(db, FakeRandomSource.Returning(0))
            .Handle(new FleeFromCombatHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Fled.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Counterattacks_When_The_Flee_Roll_Fails()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy();
        enemy.Participants.Add(character.Id);
        SetSession(character.Id, zg, loc, enemy.Id);

        // Roll 99 is at or above the 50% flee chance; the enemy has no abilities, so no further roll.
        var result = await MakeHandler(db, FakeRandomSource.Returning(99))
            .Handle(new FleeFromCombatHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Fled.Should().BeFalse();
        result.EnemyDamage.Should().BeGreaterThan(0);
        _sessions.IsInCombat(character.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Keeps_The_Damage_Already_Dealt_When_The_Player_Flees()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy();
        enemy.Participants.Add(character.Id);
        enemy.DamageContributions[character.Id] = 17;
        SetSession(character.Id, zg, loc, enemy.Id);

        // Roll 0 forces a successful flee.
        var result = await MakeHandler(db, FakeRandomSource.Returning(0))
            .Handle(new FleeFromCombatHubCommand(character.Id), CancellationToken.None);

        result.Fled.Should().BeTrue();
        enemy.Participants.Should().NotContain(character.Id, "a fleeing member stops being a target");
        enemy.DamageContributions.Should().ContainKey(character.Id)
            .WhoseValue.Should().Be(17, "damage already dealt is still theirs when the rest of the party finishes the kill");
    }
}
