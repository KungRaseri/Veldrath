using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="AttackEnemyHubCommandHandler"/>.</summary>
public class AttackEnemyHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();

    private AttackEnemyHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            _enemies,
            _sessions,
            Options.Create(new DeathPenaltyOptions()),
            NullLogger<AttackEnemyHubCommandHandler>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string? attrsJson = null)
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
            Attributes = attrsJson ?? "{}",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp, Guid charId)
    {
        var zoneGroup = $"test/{Guid.NewGuid():N}";
        const string loc = "forest";
        var enemy = new SpawnedEnemy
        {
            Name = "Goblin",
            Level = 1,
            CurrentHealth = hp,
            MaxHealth = hp,
            BaseXp = 10,
            GoldReward = 5,
            ArchetypeSlug = "goblin",
        };
        enemy.Participants.Add(charId);
        enemy.DamageContributions[charId] = 0;
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
        var charId = Guid.NewGuid();

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Enemy_Not_In_Store()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var zoneGroup = $"test/{Guid.NewGuid():N}";
        var enemyId = Guid.NewGuid();

        _sessions.Set(character.Id, new ActiveCombatSession(zoneGroup, "loc", enemyId, false, 0, DateTimeOffset.UtcNow));

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task Handle_Reduces_Enemy_Health_On_Normal_Attack()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
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
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy(1, character.Id); // 1 HP — will die from any hit
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeTrue();
        result.EnemyRemainingHealth.Should().Be(0);
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Solo_Kill_Still_Pays_The_Whole_Reward()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy(1, character.Id);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.XpEarned.Should().Be(10, "a solo player takes the enemy's whole XP");
        result.GoldEarned.Should().Be(5, "a solo player takes the enemy's whole gold");
        result.ParticipantIds.Should().Equal([character.Id]);
        result.EnemyArchetypeSlug.Should().Be("goblin");

        await using var read = _dbFactory.CreateContext();
        var stored = await read.Characters.SingleAsync(c => c.Id == character.Id);
        stored.Experience.Should().Be(10);
        stored.Attributes.Should().Contain("\"Gold\":5",
            "the gold must survive the cooldown write that follows the award");
    }

    [Fact]
    public async Task Handle_Credits_Every_Participant_And_Splits_The_Reward_Exactly()
    {
        await using var db = _dbFactory.CreateContext();
        var killer = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);
        var (zg, loc, enemy) = AddEnemy(1, killer.Id);
        enemy.Participants.Add(ally.Id);
        enemy.DamageContributions[ally.Id] = 30;
        enemy.DamageContributions[killer.Id] = 10;
        SetSession(killer.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(killer.Id), CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue();
        result.ParticipantIds.Should().BeEquivalentTo([killer.Id, ally.Id]);

        await using var read = _dbFactory.CreateContext();
        var killerXp = await read.Characters.Where(c => c.Id == killer.Id).Select(c => c.Experience).SingleAsync();
        var allyXp = await read.Characters.Where(c => c.Id == ally.Id).Select(c => c.Experience).SingleAsync();

        (killerXp + allyXp).Should().Be(10, "the split must not mint or lose experience");
        allyXp.Should().BeGreaterThan(killerXp, "the ally dealt three times the damage");
    }

    [Fact]
    public async Task Handle_Takes_Gold_First_When_The_Player_Dies_Carrying_Gold()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db, """{"CurrentHealth":1,"MaxHealth":100,"Gold":50,"Constitution":10}""");
        var (zg, loc, enemy) = AddEnemy(100, character.Id);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.PlayerDefeated.Should().BeTrue();
        result.GoldLost.Should().Be(5, "10% of 50 gold");
        result.XpLost.Should().Be(0, "gold is taken first and never both");

        await using var read = _dbFactory.CreateContext();
        var stored = await read.Characters.SingleAsync(c => c.Id == character.Id);
        stored.Attributes.Should().Contain("\"Gold\":45", "the penalty must be persisted, not just reported");
        stored.Experience.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Takes_Experience_When_The_Player_Dies_Broke()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, """{"CurrentHealth":1,"MaxHealth":100}""");
        character.Experience = 200;
        await db.SaveChangesAsync();
        var (zg, loc, enemy) = AddEnemy(100, character.Id);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(new AttackEnemyHubCommand(character.Id), CancellationToken.None);

        result.PlayerDefeated.Should().BeTrue();
        result.GoldLost.Should().Be(0, "the character carries no gold");
        result.XpLost.Should().Be(10, "5% of 200 experience");

        await using var read = _dbFactory.CreateContext();
        var stored = await read.Characters.SingleAsync(c => c.Id == character.Id);
        stored.Experience.Should().Be(190);
    }
}
