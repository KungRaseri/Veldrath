using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Services;

/// <summary>
/// Covers the two remaining pieces of <see cref="CombatService"/>: the budget-driven victory loot
/// roll and enemy spell casting. Both the casting AI's priority rolls and the damage/healing dice are
/// driven through the same injected <see cref="IRandomSource"/>, so every outcome below is scripted
/// rather than sampled.
/// </summary>
[Trait("Category", "Service")]
public class CombatServiceLootAndSpellTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISaveGameService> _saveGameService = new();

    public CombatServiceLootAndSpellTests()
    {
        _saveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { PlayerDamageMultiplier = 1, EnemyDamageMultiplier = 1, EnemyHealthMultiplier = 1 });
        _saveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        _mediator.Setup(m => m.Send(It.IsAny<ProcessStatusEffectsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult());
        _mediator.Setup(m => m.Send(It.IsAny<AwardSkillXPCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXPResult { SkillId = "test" });
        _mediator.Setup(m => m.Send(It.IsAny<ApplyStatusEffectCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyStatusEffectResult { Success = true });
        _mediator.Setup(m => m.Send(It.IsAny<UpdateQuestProgressCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateQuestProgressResult(true, false, false));
    }

    private CombatService CreateService(
        IRandomSource random,
        PowerDataService? powerCatalog = null,
        ItemGenerator? itemGenerator = null) =>
        new(
            _saveGameService.Object,
            _mediator.Object,
            powerCatalog!,
            NullLogger<CombatService>.Instance,
            NullLoggerFactory.Instance,
            itemGenerator,
            random);

    // ── Victory loot ──────────────────────────────────────────────────────────

    private sealed class StubDbContextFactory(string databaseName) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private sealed class EmptyNamePatternRepository : RealmEngine.Core.Abstractions.INamePatternRepository
    {
        public Task<IEnumerable<RealmEngine.Data.Entities.NamePatternSet>> GetAllAsync() =>
            Task.FromResult<IEnumerable<RealmEngine.Data.Entities.NamePatternSet>>([]);

        public Task<RealmEngine.Data.Entities.NamePatternSet?> GetByEntityPathAsync(string entityPath) =>
            Task.FromResult<RealmEngine.Data.Entities.NamePatternSet?>(null);
    }

    private static ItemGenerator CreateItemGenerator()
    {
        // An empty in-memory content database: the budget generator already returns a usable
        // minimal item when it has nothing to work with, which is all the loot roll needs.
        var databaseName = Guid.NewGuid().ToString();
        var factory = new StubDbContextFactory(databaseName);
        var db = factory.CreateDbContext();

        var calculator = new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        var configFactory = new BudgetConfigFactory(new NullGameConfigService(), NullLogger<BudgetConfigFactory>.Instance);
        var patternService = new NamePatternService(
            new EmptyNamePatternRepository(),
            NullLogger<NamePatternService>.Instance);
        patternService.InitializeAsync().GetAwaiter().GetResult();

        var budgetGenerator = new BudgetItemGenerationService(
            calculator,
            new MaterialPoolService(
                factory,
                calculator,
                configFactory.GetMaterialPools(),
                configFactory.GetEnemyTypes(),
                NullLogger<MaterialPoolService>.Instance,
                FakeRandomSource.Seeded(11)),
            configFactory,
            patternService,
            new ItemDataService(factory, NullLogger<ItemDataService>.Instance),
            NullLogger<BudgetItemGenerationService>.Instance);

        return new ItemGenerator(
            db,
            new EnchantmentGenerator(db, NullLogger<EnchantmentGenerator>.Instance),
            new SocketGenerator(new SocketConfig(), NullLogger<SocketGenerator>.Instance),
            budgetGenerator,
            NullLogger<ItemGenerator>.Instance);
    }

    private sealed class NullGameConfigService : RealmEngine.Data.Services.GameConfigService
    {
        public override string? GetData(string key) => null;
    }

    [Fact]
    public async Task GenerateVictoryOutcome_For_A_Boss_Should_Always_Drop_Three_Pieces_Of_Loot()
    {
        // Arrange — bosses have a 100% loot chance per slot, so all three rolls land.
        var service = CreateService(FakeRandomSource.Returning(0, 0, 0), itemGenerator: CreateItemGenerator());
        var enemy = new Enemy
        {
            Id = "ash-wyrm",
            Name = "Ash Wyrm",
            Type = EnemyType.Beast, // beasts resolve their category without an RNG roll
            Difficulty = EnemyDifficulty.Boss,
            Level = 12,
            XP = 500,
            GoldReward = 250
        };

        // Act
        var outcome = await service.GenerateVictoryOutcome(new Character { Name = "Hero" }, enemy);

        // Assert
        outcome.LootDropped.Should().HaveCount(3);
        outcome.LootDropped.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.Name));
        outcome.Summary.Should().Contain("Victory! Defeated Ash Wyrm!");
        outcome.Summary.Should().Contain("+500 XP");
        outcome.Summary.Should().Contain("+250 Gold");
        foreach (var item in outcome.LootDropped)
        {
            outcome.Summary.Should().Contain($"  • {item.Name} ({item.Rarity})");
        }
    }

    [Fact]
    public async Task GenerateVictoryOutcome_For_An_Elite_Should_Roll_Two_Loot_Slots()
    {
        // Arrange — elites have an 80% chance per slot; a 0 roll always beats it.
        var service = CreateService(FakeRandomSource.Returning(0, 0), itemGenerator: CreateItemGenerator());
        var enemy = new Enemy
        {
            Id = "elite-orc",
            Name = "Elite Orc",
            Type = EnemyType.Elemental,
            Difficulty = EnemyDifficulty.Elite,
            Level = 8
        };

        // Act
        var outcome = await service.GenerateVictoryOutcome(new Character { Name = "Hero" }, enemy);

        // Assert
        outcome.LootDropped.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Skip_Loot_When_The_Roll_Exceeds_The_Drop_Chance()
    {
        // Arrange — a hard enemy only drops on a roll below 60, so 99 drops nothing.
        var service = CreateService(FakeRandomSource.Returning(99), itemGenerator: CreateItemGenerator());
        var enemy = new Enemy
        {
            Id = "hard-orc",
            Name = "Hard Orc",
            Type = EnemyType.Elemental,
            Difficulty = EnemyDifficulty.Hard,
            Level = 6
        };

        // Act
        var outcome = await service.GenerateVictoryOutcome(new Character { Name = "Hero" }, enemy);

        // Assert
        outcome.LootDropped.Should().BeEmpty();
        outcome.Summary.Should().NotContain("•");
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Skip_Loot_For_A_Normal_Enemy_When_The_Half_Chance_Misses()
    {
        // Arrange — normal enemies only get a slot on a roll below 50.
        var service = CreateService(FakeRandomSource.Returning(50), itemGenerator: CreateItemGenerator());
        var enemy = new Enemy
        {
            Id = "plain-orc",
            Name = "Plain Orc",
            Type = EnemyType.Elemental,
            Difficulty = EnemyDifficulty.Normal,
            Level = 4
        };

        // Act
        var outcome = await service.GenerateVictoryOutcome(new Character { Name = "Hero" }, enemy);

        // Assert
        outcome.LootDropped.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EnemyType.Humanoid, EnemyDifficulty.Hard)]
    [InlineData(EnemyType.Undead, EnemyDifficulty.Hard)]
    [InlineData(EnemyType.Demon, EnemyDifficulty.Hard)]
    [InlineData(EnemyType.Dragon, EnemyDifficulty.Hard)]
    [InlineData(EnemyType.Common, EnemyDifficulty.Easy)]
    public async Task GenerateVictoryOutcome_Should_Pick_A_Category_For_Each_Enemy_Type(
        EnemyType enemyType,
        EnemyDifficulty difficulty)
    {
        // Arrange — both the loot roll and the category roll land on 0.
        var service = CreateService(FakeRandomSource.Returning(0, 0), itemGenerator: CreateItemGenerator());
        var enemy = new Enemy
        {
            Id = "loot-target",
            Name = "Loot Target",
            Type = enemyType,
            Difficulty = difficulty,
            Level = 5
        };

        // Act
        var outcome = await service.GenerateVictoryOutcome(new Character { Name = "Hero" }, enemy);

        // Assert
        outcome.LootDropped.Should().ContainSingle();
    }

    [Fact]
    public void Server_Constructor_Should_Use_The_Injected_Settings()
    {
        // Arrange — the multiplayer constructor takes ICombatSettings directly.
        var settings = new DifficultySettings
        {
            EnemyHealthMultiplier = 2.0,
            PlayerDamageMultiplier = 1.0,
            EnemyDamageMultiplier = 1.0
        };
        var service = new CombatService(
            settings,
            _mediator.Object,
            null!,
            NullLogger<CombatService>.Instance,
            NullLoggerFactory.Instance,
            itemGenerator: null,
            randomSource: FakeRandomSource.Returning());
        var enemy = new Enemy { Name = "Goblin", MaxHealth = 30, Health = 5 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.MaxHealth.Should().Be(60);
        enemy.Health.Should().Be(60);
    }

    [Theory]
    [InlineData("1d6+2", 6)]
    [InlineData("1d6-2", 2)]
    public void ExecuteEnemyAbility_Should_Apply_Dice_Notation_Modifiers(string dice, int expectedDamage)
    {
        // Arrange — AI picks the ability (0), the d6 lands on 4, neutral variance.
        var enemy = new Enemy
        {
            Id = "brute",
            Name = "Brute",
            Health = 50,
            MaxHealth = 50,
            Intelligence = 0,
            Strength = 0,
            Abilities =
            [
                new Power
                {
                    Id = "smash",
                    Name = "Smash",
                    Description = "deals damage",
                    EffectType = PowerEffectType.Damage,
                    BaseDamage = dice,
                    Cooldown = 2
                }
            ]
        };
        var service = CreateService(FakeRandomSource.Returning(0, 4, 100));
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemyAbility(enemy, player, new Dictionary<string, int>());

        // Assert
        result.Should().NotBeNull();
        result!.DamageDealt.Should().Be(expectedDamage);
        player.Health.Should().Be(100 - expectedDamage);
    }

    // ── Enemy spell casting ───────────────────────────────────────────────────

    private sealed class StubPowerRepository(IEnumerable<Power> powers) : IPowerRepository
    {
        private readonly List<Power> _powers = powers.ToList();

        public Task<List<Power>> GetAllAsync() => Task.FromResult(_powers.ToList());

        public Task<Power?> GetBySlugAsync(string slug) =>
            Task.FromResult(_powers.FirstOrDefault(p => p.Slug == slug));

        public Task<List<Power>> GetByTypeAsync(string powerType) => Task.FromResult(new List<Power>());

        public Task<List<Power>> GetBySchoolAsync(string school) => Task.FromResult(new List<Power>());
    }

    private static async Task<PowerDataService> CreateCatalogAsync(Power spell)
    {
        var catalog = new PowerDataService(
            new StubPowerRepository([spell]),
            NullLogger<PowerDataService>.Instance);
        await catalog.InitializeAsync();
        return catalog;
    }

    private static Power CreateSpell(
        PowerEffectType effectType,
        string? baseEffectValue = null,
        int manaCost = 5,
        int cooldown = 3) => new()
        {
            Id = $"spell-{effectType.ToString().ToLowerInvariant()}",
            Slug = $"spell-{effectType.ToString().ToLowerInvariant()}",
            Name = $"{effectType} Spell",
            DisplayName = "Sunfire",
            EffectType = effectType,
            BaseEffectValue = baseEffectValue,
            ManaCost = manaCost,
            Cooldown = cooldown,
            Rank = 3
        };

    private static Enemy CreateCaster(string spellId, int health = 100, int maxHealth = 100, int mana = 100) => new()
    {
        Id = "caster",
        Name = "Cinder Mage",
        Health = health,
        MaxHealth = maxHealth,
        Mana = mana,
        MaxMana = 100,
        Intelligence = 10,
        Strength = 5,
        SpellIds = [spellId]
    };

    [Fact]
    public async Task ExecuteEnemySpell_Damage_Should_Hurt_The_Player_And_Start_The_Cooldown()
    {
        // Arrange — a healthy caster prefers its rank-3 damage spell (50% priority roll), the two d6
        // land on 3 and 4, and the ±15% variance roll is neutral.
        var spell = CreateSpell(PowerEffectType.Damage, baseEffectValue: "2d6", manaCost: 20, cooldown: 4);
        var catalog = await CreateCatalogAsync(spell);
        var service = CreateService(FakeRandomSource.Returning(49, 3, 4, 100), catalog);
        var enemy = CreateCaster(spell.Id);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemySpell(enemy, player, catalog);

        // Assert — 2d6 (7) + intelligence (10) = 17 damage at neutral variance.
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.SpellCast.Should().BeSameAs(spell);
        result.ManaCostPaid.Should().Be(20);
        result.EffectValue.Should().Be("17");
        result.Message.Should().Be("Cinder Mage cast Sunfire and dealt 17 damage!");
        enemy.Mana.Should().Be(80);
        enemy.SpellCooldowns[spell.Id].Should().Be(4);
        player.Health.Should().Be(83);
    }

    [Fact]
    public async Task ExecuteEnemySpell_Damage_Without_Dice_Notation_Should_Use_The_Fifteen_Point_Base()
    {
        // Arrange — no BaseEffectValue means the flat 15 base plus the intelligence bonus.
        var spell = CreateSpell(PowerEffectType.Damage, baseEffectValue: null);
        var catalog = await CreateCatalogAsync(spell);
        var service = CreateService(FakeRandomSource.Returning(49, 100), catalog);
        var enemy = CreateCaster(spell.Id);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemySpell(enemy, player, catalog);

        // Assert — (15 base + 10 intelligence) scaled by a neutral variance roll.
        result.Should().NotBeNull();
        result!.EffectValue.Should().Be("25");
        player.Health.Should().Be(75);
    }

    [Fact]
    public async Task ExecuteEnemySpell_Heal_Should_Restore_Health_Capped_At_Max()
    {
        // Arrange — 8 of 30 HP is the wounded band that prioritises a heal (80% priority roll).
        var spell = CreateSpell(PowerEffectType.Heal, baseEffectValue: "3d6");
        var catalog = await CreateCatalogAsync(spell);
        var service = CreateService(FakeRandomSource.Returning(10, 3, 4, 5, 100), catalog);
        var enemy = CreateCaster(spell.Id, health: 8, maxHealth: 30);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemySpell(enemy, player, catalog);

        // Assert — 3d6 (12) + intelligence/2 (5) = 17 healed, clamped to the 30 HP ceiling.
        result.Should().NotBeNull();
        result!.EffectValue.Should().Be("17");
        result.Message.Should().Be("Cinder Mage cast Sunfire and restored 17 health!");
        enemy.Health.Should().Be(25, "8 + 17 is clamped to MaxHealth");
        player.Health.Should().Be(100, "a self-heal never touches the player");
        enemy.Mana.Should().Be(95);
    }

    [Fact]
    public async Task ExecuteEnemySpell_Returns_Null_When_The_AI_Declines_Both_The_Priority_And_Fallback_Rolls()
    {
        // Arrange — the fallback priority is 20%, and the 15% random-spell fallback also misses.
        var spell = CreateSpell(PowerEffectType.Buff);
        var catalog = await CreateCatalogAsync(spell);
        var service = CreateService(FakeRandomSource.Returning(99, 99), catalog);
        var enemy = CreateCaster(spell.Id);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemySpell(enemy, player, catalog);

        // Assert — no cast, no mana spent, no cooldown started.
        result.Should().BeNull();
        enemy.Mana.Should().Be(100);
        enemy.SpellCooldowns.Should().BeEmpty();
        player.Health.Should().Be(100);
    }

    [Theory]
    [InlineData(PowerEffectType.Buff, "Cinder Mage cast Sunfire!")]
    [InlineData(PowerEffectType.Protection, "Cinder Mage cast Sunfire!")]
    [InlineData(PowerEffectType.Debuff, "Cinder Mage cast Sunfire on you!")]
    [InlineData(PowerEffectType.Control, "Cinder Mage cast Sunfire on you!")]
    [InlineData(PowerEffectType.Utility, "Cinder Mage cast Sunfire!")]
    [InlineData(PowerEffectType.Summon, "Cinder Mage cast Sunfire!")]
    public async Task ExecuteEnemySpell_Non_Damaging_Types_Should_Only_Produce_A_Message(
        PowerEffectType effectType,
        string expectedMessage)
    {
        // Arrange — the low-priority fallback arm (20%) is taken by a roll of 10.
        var spell = CreateSpell(effectType);
        var catalog = await CreateCatalogAsync(spell);
        var service = CreateService(FakeRandomSource.Returning(10), catalog);
        var enemy = CreateCaster(spell.Id);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var result = service.ExecuteEnemySpell(enemy, player, catalog);

        // Assert — a message and the bookkeeping, with no effect on either combatant.
        result.Should().NotBeNull();
        result!.Message.Should().Be(expectedMessage);
        result.EffectValue.Should().BeEmpty();
        result.ManaCostPaid.Should().Be(5);
        enemy.Mana.Should().Be(95);
        enemy.SpellCooldowns.Should().ContainKey(spell.Id).WhoseValue.Should().Be(3);
        player.Health.Should().Be(100);
        enemy.Health.Should().Be(100);
    }
}
