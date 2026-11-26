using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Edge-case tests for <see cref="UseAbilityInCombatHubCommandHandler"/>) : the guard branches around a vanished
/// or dead enemy, a missing character, an unlearned ability, the healing branch, and the kill/reward branch.
/// </summary>
public class UseAbilityInCombatEdgeCaseTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();

    public void Dispose() => _dbFactory.Dispose();

    private UseAbilityInCombatHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<ICharacterClassRepository>(),
            Mock.Of<IPowerRepository>(),
            _enemies,
            _sessions,
            new Veldrath.Server.Features.Zones.EfCoreZoneInstanceService(
                db,
                new CharacterRepository(db),
                new CharacterUnlockedLocationRepository(db),
                Mock.Of<IZoneLocationRepository>(),
                NullLogger<Veldrath.Server.Features.Zones.EfCoreZoneInstanceService>.Instance),
            Options.Create(new DeathPenaltyOptions()),
            Mock.Of<ILootTableRepository>(),
            NullLogger<UseAbilityInCombatHubCommandHandler>.Instance);

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string abilities = """["fireball","heal"]""",
        string? attrsJson = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Mage",
            SlotIndex = 1,
            Attributes = attrsJson ?? """{"CurrentMana":50,"MaxMana":50,"CurrentHealth":50,"MaxHealth":100}""",
            AbilitiesBlob = abilities,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private SpawnedEnemy AddEnemy(int hp, int baseXp = 0, int goldReward = 0, string archetypeSlug = "golem")
    {
        var enemy = new SpawnedEnemy
        {
            Name = "Golem",
            ArchetypeSlug = archetypeSlug,
            Level = 1,
            CurrentHealth = hp,
            MaxHealth = Math.Max(1, hp),
            BaseXp = baseXp,
            GoldReward = goldReward
        };
        _enemies.Add("test/zone", "dungeon", enemy);
        return enemy;
    }

    private void SetSession(Guid characterId, Guid enemyId) =>
        _sessions.Set(characterId, new ActiveCombatSession("test/zone", "dungeon", enemyId, false, 0, DateTimeOffset.UtcNow));

    [Fact]
    public async Task Handle_ClearsTheSession_WhenTheEnemyNoLongerExists()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        SetSession(character.Id, Guid.NewGuid());

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Enemy no longer exists");
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ClearsTheSession_WhenTheEnemyIsAlreadyDead()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var enemy = AddEnemy(hp: 0);
        SetSession(character.Id, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Enemy is already dead");
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCharacterRowIsGone()
    {
        await using var db = _dbFactory.CreateContext();
        var enemy = AddEnemy(hp: 100);
        var missingCharacter = Guid.NewGuid();
        SetSession(missingCharacter, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(missingCharacter, "fireball"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Character not found");
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheAbilityIsNotLearned()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, abilities: """["heal"]""");
        var enemy = AddEnemy(hp: 100);
        SetSession(character.Id, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "meteor"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Ability 'meteor' is not learned");
    }

    [Fact]
    public async Task Handle_HealsTheCharacter_ForTheFallbackAmount()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var enemy = AddEnemy(hp: 100);
        SetSession(character.Id, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "heal"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(20, "the fallback heal restores 20 HP");
        result.AbilityDamage.Should().Be(0);
        result.EnemyDefeated.Should().BeFalse();
        result.EnemyRemainingHealth.Should().Be(100);
        result.ManaCost.Should().Be(10);
        result.PlayerRemainingMana.Should().Be(40);
    }

    [Fact]
    public async Task Handle_AwardsRewards_WhenTheAbilityKillsTheEnemy()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db, attrsJson: """{"CurrentMana":50,"MaxMana":50,"Intelligence":16,"Gold":7,"AbilityCooldown_heal":2}""");
        var enemy = AddEnemy(hp: 1, baseXp: 40, goldReward: 25);
        SetSession(character.Id, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeTrue();
        result.AbilityDamage.Should().BeGreaterThan(0);
        result.EnemyRemainingHealth.Should().Be(0);
        result.EnemyName.Should().Be("Golem");
        result.EnemyArchetypeSlug.Should().Be("golem", "the hub advances the kill objective by archetype slug, not just the display name");
        result.EnemyDamage.Should().Be(0, "a defeated enemy does not counter-attack");
        result.XpEarned.Should().Be(40, "a single contributor receives the enemy's whole XP pool");
        result.GoldEarned.Should().Be(25, "a single contributor receives the enemy's whole gold pool");
        result.PlayerRemainingMana.Should().Be(40, "fireball cost 10 of the character's 50 mana");
        enemy.CurrentHealth.Should().Be(0);
        _enemies.IsMarkedDead(enemy.Id).Should().BeTrue();
        _sessions.IsInCombat(character.Id).Should().BeFalse("the kill releases every engaged session");

        // Re-read through a fresh context: the reward and the cast mutations must both be persisted.
        await using var verify = _dbFactory.CreateContext();
        var persisted = await verify.Characters.SingleAsync(c => c.Id == character.Id);
        persisted.Experience.Should().Be(40);
        var attributes = JsonSerializer.Deserialize<Dictionary<string, int>>(persisted.Attributes)!;
        attributes["Gold"].Should().Be(32, "the 7 gold the character already had plus the 25 gold reward");
        attributes["CurrentMana"].Should().Be(40, "the 10-mana fireball cost was deducted when it was cast");
        attributes["AbilityCooldown_fireball"].Should().Be(2,
            "the fresh three-turn cooldown ticks down one turn on the turn it was cast");
        attributes["AbilityCooldown_heal"].Should().Be(1, "an unrelated cooldown still ticks down");
    }

    [Fact]
    public async Task Handle_PersistsTheCast_WhenAnotherRequestWinsTheDeathTransition()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db, attrsJson: """{"CurrentMana":50,"MaxMana":50,"Intelligence":16,"Gold":7}""");
        var enemy = AddEnemy(hp: 1, baseXp: 40, goldReward: 25);
        SetSession(character.Id, enemy.Id);
        // A racing request already claimed the death transition for this enemy.
        (await _enemies.TryMarkDeadAsync(enemy.Id, CancellationToken.None))
            .Should().NotBeNull("arranging the lost-transition precondition");

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeTrue();
        result.XpEarned.Should().Be(0, "the request that lost the transition distributes no reward");
        result.GoldEarned.Should().Be(0, "the request that lost the transition distributes no reward");

        // Losing the reward claim must not lose the cast: mana and cooldown were already spent.
        await using var verify = _dbFactory.CreateContext();
        var persisted = await verify.Characters.SingleAsync(c => c.Id == character.Id);
        persisted.Experience.Should().Be(0, "no XP was awarded to this request");
        var attributes = JsonSerializer.Deserialize<Dictionary<string, int>>(persisted.Attributes)!;
        attributes["Gold"].Should().Be(7, "no reward was distributed by this request");
        attributes["CurrentMana"].Should().Be(40, "the cast still cost its mana");
        attributes["AbilityCooldown_fireball"].Should().Be(2, "the cast still started its cooldown");
    }
}
