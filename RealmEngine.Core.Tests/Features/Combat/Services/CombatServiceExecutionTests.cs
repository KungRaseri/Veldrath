using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using QuestModel = RealmEngine.Shared.Models.Quest;

namespace RealmEngine.Core.Tests.Features.Combat.Services;

/// <summary>
/// Branch-level tests for <see cref="CombatService"/>: attack resolution, damage modifiers,
/// elemental status application, enemy ability/spell execution, item use, flee and victory outcomes.
/// Every test injects a scripted <see cref="FakeRandomSource"/> so the chosen arm is deterministic.
/// </summary>
[Trait("Category", "Service")]
public class CombatServiceExecutionTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISaveGameService> _saveGameService = new();

    public CombatServiceExecutionTests()
    {
        _saveGameService.Setup(s => s.GetDifficultySettings()).Returns(new DifficultySettings
        {
            Name = "Normal",
            PlayerDamageMultiplier = 1.0,
            EnemyDamageMultiplier = 1.0,
            EnemyHealthMultiplier = 1.0,
            GoldXPMultiplier = 1.0
        });
        _saveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        _mediator
            .Setup(m => m.Send(It.IsAny<ProcessStatusEffectsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult());
        _mediator
            .Setup(m => m.Send(It.IsAny<ApplyStatusEffectCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyStatusEffectResult { Success = true });
        _mediator
            .Setup(m => m.Send(It.IsAny<AwardSkillXPCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXPResult { SkillId = "test" });
        _mediator
            .Setup(m => m.Send(It.IsAny<UpdateQuestProgressCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateQuestProgressResult(true, false, false));
    }

    private CombatService CreateService(IRandomSource randomSource) =>
        new(
            _saveGameService.Object,
            _mediator.Object,
            null!,
            NullLogger<CombatService>.Instance,
            NullLoggerFactory.Instance,
            itemGenerator: null,
            randomSource: randomSource);

    private void SetStatusResult(ProcessStatusEffectsResult result) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<ProcessStatusEffectsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static Character CreatePlayer(int dexterity = 10, int strength = 10, int constitution = 10) =>
        new()
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            Mana = 50,
            MaxMana = 50,
            Dexterity = dexterity,
            Strength = strength,
            Constitution = constitution,
            Skills = new Dictionary<string, CharacterSkill>()
        };

    private static Enemy CreateEnemy(string name = "Orc") => new()
    {
        Id = "orc",
        Name = name,
        Health = 100,
        MaxHealth = 100,
        Constitution = 0,
        Strength = 0, // keeps enemy damage arithmetic pinned to BasePhysicalDamage
        BasePhysicalDamage = 10
    };

    private static Item CreateWeapon(ItemRarity rarity = ItemRarity.Common, Dictionary<string, TraitValue>? traits = null) =>
        new()
        {
            Name = "Blade",
            Type = ItemType.Weapon,
            Rarity = rarity,
            Traits = traits ?? new Dictionary<string, TraitValue>()
        };

    private List<T> SentCommands<T>() where T : class =>
        _mediator.Invocations
            .Where(i => i.Method.Name == nameof(IMediator.Send))
            .Select(i => i.Arguments[0])
            .OfType<T>()
            .ToList();

    // ── InitializeCombat ────────────────────────────────────────────────────────

    [Fact]
    public void InitializeCombat_Should_Scale_Health_And_Refill()
    {
        // Arrange
        _saveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { EnemyHealthMultiplier = 1.5 });
        var service = CreateService(FakeRandomSource.Returning());
        var enemy = new Enemy { Name = "Goblin", MaxHealth = 40, Health = 7 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.MaxHealth.Should().Be(60);
        enemy.Health.Should().Be(60);
    }

    // ── ExecutePlayerAttack: status-effect plumbing ─────────────────────────────

    [Fact]
    public async Task ExecutePlayerAttack_Should_Copy_Status_Effect_Results_Onto_Result()
    {
        // Arrange — no dodge, no critical, neutral damage variance.
        var burning = new StatusEffect
        {
            Id = "burn-1",
            Type = StatusEffectType.Burning,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Burning"
        };
        SetStatusResult(new ProcessStatusEffectsResult
        {
            TotalDamageTaken = 7,
            TotalHealingReceived = 3,
            ExpiredEffectTypes = new List<StatusEffectType> { StatusEffectType.Poisoned },
            ActiveEffectTypes = new List<StatusEffectType> { StatusEffectType.Burning }
        });
        var player = CreatePlayer();
        player.ActiveStatusEffects = new List<StatusEffect> { burning };
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        result.DotDamage.Should().Be(7);
        result.HotHealing.Should().Be(3);
        result.ActiveStatusEffects.Should().ContainSingle().Which.Should().BeSameAs(burning);
        result.StatusEffectsExpired.Should().ContainSingle().Which.Should().Be(StatusEffectType.Poisoned);
    }

    [Theory]
    [InlineData(StatusEffectType.Stunned, "You are stunned and cannot act!")]
    [InlineData(StatusEffectType.Frozen, "You are frozen solid and cannot move!")]
    [InlineData(StatusEffectType.Paralyzed, "You are paralyzed and cannot act!")]
    public async Task ExecutePlayerAttack_Should_Refuse_To_Act_When_Crowd_Controlled(
        StatusEffectType effectType,
        string expectedMessage)
    {
        // Arrange
        var player = CreatePlayer();
        player.ActiveStatusEffects =
        [
            new StatusEffect
            {
                Id = "cc",
                Type = effectType,
                Category = StatusEffectCategory.CrowdControl,
                Name = effectType.ToString()
            }
        ];
        var service = CreateService(FakeRandomSource.Returning());
        var enemy = CreateEnemy();

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(expectedMessage);
        result.Damage.Should().Be(0);
        enemy.Health.Should().Be(100);
        // No dodge/crit roll was consumed.
        SentCommands<AwardSkillXPCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Still_Act_When_Only_Non_CrowdControl_Effects_Are_Active()
    {
        // Arrange
        var player = CreatePlayer();
        player.ActiveStatusEffects =
        [
            new StatusEffect
            {
                Id = "burn",
                Type = StatusEffectType.Burning,
                Category = StatusEffectCategory.DamageOverTime,
                Name = "Burning"
            }
        ];
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var enemy = CreateEnemy();

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Success.Should().BeTrue();
        result.Damage.Should().Be(15);
    }

    // ── ExecutePlayerAttack: dodge / crit ───────────────────────────────────────

    [Fact]
    public async Task ExecutePlayerAttack_Should_Return_Dodge_Without_Rolling_Damage()
    {
        // Arrange — a 0 dodge roll always beats the enemy's 5% dodge chance.
        var service = CreateService(FakeRandomSource.Returning(0));
        var enemy = CreateEnemy();

        // Act
        var result = await service.ExecutePlayerAttack(CreatePlayer(), enemy);

        // Assert
        result.IsDodged.Should().BeTrue();
        result.Message.Should().Be("Orc dodged your attack!");
        result.IsCritical.Should().BeFalse();
        enemy.Health.Should().Be(100);
        _mediator.Invocations
            .Count(i => i.Method.Name == nameof(IMediator.Send)
                        && i.Arguments[0] is AwardSkillXPCommand)
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecutePlayerAttack_With_Critical_Should_Double_Damage_And_Award_Precision_XP()
    {
        // Arrange — no dodge, critical (0 roll), 100% variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 0, 100));
        var enemy = CreateEnemy();

        // Act
        var result = await service.ExecutePlayerAttack(CreatePlayer(), enemy);

        // Assert
        result.IsCritical.Should().BeTrue();
        result.Damage.Should().Be(30); // (5 unarmed + 10 STR) * 2
        result.Message.Should().Be("CRITICAL HIT! You dealt 30 damage to Orc!");
        enemy.Health.Should().Be(70);
        SentCommands<AwardSkillXPCommand>()
            .Should().ContainSingle(c => c.SkillId == "precision" && c.XPAmount == 10 && c.ActionSource == "critical_hit");
    }

    [Theory]
    [InlineData(ItemRarity.Common, 20)]
    [InlineData(ItemRarity.Uncommon, 23)]
    [InlineData(ItemRarity.Rare, 28)]
    [InlineData(ItemRarity.Epic, 36)]
    [InlineData(ItemRarity.Legendary, 48)]
    public async Task ExecutePlayerAttack_Should_Scale_Damage_With_Weapon_Rarity(ItemRarity rarity, int expectedDamage)
    {
        // Arrange — weaponDamage = 8 + rarity bonus, plus STR 10, neutral variance, no crit.
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(rarity);
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        result.Damage.Should().Be(expectedDamage);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Add_Weapon_Strength_Trait_To_Damage()
    {
        // Arrange — Rare blade (+10) with a +5 Strength trait: 8 + 10 + 5 + STR 10 = 33.
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Rare, new Dictionary<string, TraitValue>
        {
            ["Strength"] = new(5, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        result.Damage.Should().Be(33);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Ignore_Non_Weapon_MainHand_For_Base_Damage()
    {
        // Arrange — a shield in the main hand leaves the unarmed base of 5.
        var player = CreatePlayer();
        player.EquippedMainHand = new Item { Name = "Buckler", Type = ItemType.Shield, Rarity = ItemRarity.Legendary };
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        result.Damage.Should().Be(15); // 5 + STR 10
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Award_Weapon_Skill_XP_On_A_Normal_Hit()
    {
        // Arrange
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["skillReference"] = new("@skills/weapon:light-blades", TraitType.String)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        var award = SentCommands<AwardSkillXPCommand>().Should().ContainSingle().Subject;
        award.SkillId.Should().Be("light-blades");
        award.XPAmount.Should().Be(5);
        award.ActionSource.Should().Be("light_blades_hit");
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Award_Fifteen_Weapon_XP_On_A_Critical_Hit()
    {
        // Arrange
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["skillReference"] = new("@skills/weapon:light-blades", TraitType.String)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 0, 100));

        // Act
        await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        SentCommands<AwardSkillXPCommand>()
            .Should().ContainSingle(c => c.SkillId == "light-blades")
            .Which.XPAmount.Should().Be(15);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Apply_Status_Effect_Attack_Modifier_To_Damage()
    {
        // Arrange — +25 attack from a debuff/buff effect raises damage from 15 to 40.
        SetStatusResult(new ProcessStatusEffectsResult
        {
            TotalStatModifiers = new Dictionary<string, int> { ["attack"] = 25 }
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(CreatePlayer(), CreateEnemy());

        // Assert
        result.Damage.Should().Be(40);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Clamp_Damage_To_At_Least_One_Against_Heavy_Armour()
    {
        // Arrange — 20 Constitution defence versus 15 raw damage.
        var enemy = CreateEnemy();
        enemy.Constitution = 50;
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(CreatePlayer(), enemy);

        // Assert
        result.Damage.Should().Be(1);
        enemy.Health.Should().Be(99);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Apply_Difficulty_Player_Damage_Multiplier()
    {
        // Arrange — 15 damage at 2.0x multiplier becomes 30.
        _saveGameService.Setup(s => s.GetDifficultySettings()).Returns(new DifficultySettings
        {
            PlayerDamageMultiplier = 2.0,
            EnemyDamageMultiplier = 1.0,
            EnemyHealthMultiplier = 1.0
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));

        // Act
        var result = await service.ExecutePlayerAttack(CreatePlayer(), CreateEnemy());

        // Assert
        result.Damage.Should().Be(30);
    }

    // ── ExecutePlayerAttack: elemental damage and resistances ───────────────────

    [Theory]
    [InlineData("fireDamage", "fire", StatusEffectType.Burning, 3, 5, StatusEffectCategory.DamageOverTime)]
    [InlineData("iceDamage", "ice", StatusEffectType.Frozen, 2, 0, StatusEffectCategory.CrowdControl)]
    [InlineData("lightningDamage", "lightning", StatusEffectType.Stunned, 2, 0, StatusEffectCategory.CrowdControl)]
    [InlineData("poisonDamage", "poison", StatusEffectType.Poisoned, 5, 4, StatusEffectCategory.DamageOverTime)]
    public async Task ExecutePlayerAttack_Should_Apply_Elemental_Status_Effect_When_The_Roll_Succeeds(
        string traitName,
        string expectedDamageType,
        StatusEffectType expectedType,
        int expectedDuration,
        int expectedTickDamage,
        StatusEffectCategory expectedCategory)
    {
        // Arrange — no dodge, no crit, 100% variance, and a 0 elemental-proc roll (< 20).
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            [traitName] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 0));
        var enemy = CreateEnemy();

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(30); // 8 + 2 (common blade) + 10 STR + 10 elemental
        var applied = SentCommands<ApplyStatusEffectCommand>().Should().ContainSingle().Subject;
        applied.TargetEnemy.Should().BeSameAs(enemy);
        applied.Effect.Type.Should().Be(expectedType);
        applied.Effect.Category.Should().Be(expectedCategory);
        applied.Effect.RemainingDuration.Should().Be(expectedDuration);
        applied.Effect.OriginalDuration.Should().Be(expectedDuration);
        applied.Effect.TickDamage.Should().Be(expectedTickDamage);
        applied.Effect.DamageType.Should().Be(expectedDamageType);
        applied.Effect.Name.Should().Be(expectedType.ToString());
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Not_Apply_Elemental_Status_When_The_Roll_Is_High()
    {
        // Arrange — a 99 roll misses the 20% proc window.
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        SentCommands<ApplyStatusEffectCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Not_Proc_A_Status_For_An_Unmapped_Damage_Type()
    {
        // Arrange — a generic "damageType" trait of "holy" maps to no status effect.
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["damageType"] = new("Holy", TraitType.String)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 0));

        // Act
        var result = await service.ExecutePlayerAttack(player, CreateEnemy());

        // Assert
        result.Damage.Should().Be(20);
        SentCommands<ApplyStatusEffectCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Deal_One_Damage_To_An_Immune_Enemy()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Traits["immuneToFire"] = new TraitValue(true, TraitType.Boolean);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(1);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Halve_Damage_Against_Strong_Resistance()
    {
        // Arrange — 50 fire resistance = 0.5 multiplier on 25 damage.
        var enemy = CreateEnemy();
        enemy.Traits["resistFire"] = new TraitValue(50, TraitType.Number);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(15); // (int)(30 * 0.5)
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Apply_Moderate_Resistance_Multiplier()
    {
        // Arrange — 25 fire resistance = 0.75 multiplier on 25 damage.
        var enemy = CreateEnemy();
        enemy.Traits["resistFire"] = new TraitValue(25, TraitType.Number);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(22); // (int)(30 * 0.75)
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Double_Damage_Against_A_Vulnerable_Enemy()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Traits["vulnerability"] = new TraitValue("fire", TraitType.String);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(60);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Apply_Weakness_Multiplier()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Traits["weakness"] = new TraitValue("fire", TraitType.String);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(45); // (int)(30 * 1.5)
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Ignore_Weakness_For_A_Different_Damage_Type()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Traits["weakness"] = new TraitValue("ice", TraitType.String);
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["fireDamage"] = new(10, TraitType.Number)
        });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100, 99));

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.Damage.Should().Be(30);
    }

    // ── ExecuteEnemyAttack ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(StatusEffectType.Stunned)]
    [InlineData(StatusEffectType.Frozen)]
    [InlineData(StatusEffectType.Paralyzed)]
    public async Task ExecuteEnemyAttack_Should_Skip_When_The_Enemy_Is_Crowd_Controlled(StatusEffectType effectType)
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.ActiveStatusEffects =
        [
            new StatusEffect
            {
                Id = "cc",
                Type = effectType,
                Category = StatusEffectCategory.CrowdControl,
                Name = effectType.ToString()
            }
        ];
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Orc is crowd controlled and cannot act!");
        player.Health.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_On_Dodge_Should_Award_Acrobatics_XP()
    {
        // Arrange — a 0 dodge roll always succeeds.
        var service = CreateService(FakeRandomSource.Returning(0));
        var player = CreatePlayer();

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player);

        // Assert
        result.IsDodged.Should().BeTrue();
        result.Damage.Should().Be(0);
        result.Message.Should().Be("You dodged Orc's attack!");
        var award = SentCommands<AwardSkillXPCommand>().Should().ContainSingle().Subject;
        award.SkillId.Should().Be("acrobatics");
        award.XPAmount.Should().Be(8);
        award.ActionSource.Should().Be("successful_dodge");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_On_Block_Should_Award_Block_XP_And_Deal_No_Damage()
    {
        // Arrange — no dodge (9999), block succeeds (0 < 50).
        var service = CreateService(FakeRandomSource.Returning(9999, 0));
        var player = CreatePlayer();

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player, isDefending: true);

        // Assert
        result.IsBlocked.Should().BeTrue();
        result.Damage.Should().Be(0);
        player.Health.Should().Be(100);
        SentCommands<AwardSkillXPCommand>()
            .Should().ContainSingle(c => c.SkillId == "block" && c.XPAmount == 8 && c.ActionSource == "successful_block");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_While_Defending_Should_Halve_Damage_And_Award_Armor_XP()
    {
        // Arrange — no dodge, no block (99), no crit, 100% variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 99, 9999, 100));
        var player = CreatePlayer(constitution: 0);

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player, isDefending: true);

        // Assert
        result.IsBlocked.Should().BeFalse();
        result.Damage.Should().Be(5); // 10 base halved while defending
        result.IsCritical.Should().BeFalse();
        result.Message.Should().Be("Orc attacked! You defended and took 5 damage.");
        var award = SentCommands<AwardSkillXPCommand>().Should().ContainSingle().Subject;
        award.SkillId.Should().Be("armor");
        award.XPAmount.Should().Be(5);
        award.ActionSource.Should().Be("damage_reduction");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_With_Critical_Should_Double_Damage_And_Report_It()
    {
        // Arrange — no dodge, no crit-dodge, critical (0), 100% variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 0, 100));
        var player = CreatePlayer(constitution: 0);

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player);

        // Assert
        result.IsCritical.Should().BeTrue();
        result.Damage.Should().Be(20);
        result.Message.Should().Be("Orc scored a CRITICAL HIT! You took 20 damage!");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Subtract_Player_Defense()
    {
        // Arrange — 18 Constitution defense against 10 raw damage clamps to 1.
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = CreatePlayer(constitution: 18);
        var enemy = CreateEnemy();
        enemy.BasePhysicalDamage = 10;

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player);

        // Assert
        result.Damage.Should().Be(1);
        player.Health.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Apply_Enemy_Attack_Modifier()
    {
        // Arrange — enemy attack modifier raises base damage from 10 to 40.
        _mediator
            .Setup(m => m.Send(It.Is<ProcessStatusEffectsCommand>(c => c.TargetEnemy != null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult
            {
                TotalStatModifiers = new Dictionary<string, int> { ["attack"] = 30 }
            });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = CreatePlayer(constitution: 0);

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player);

        // Assert
        result.Damage.Should().Be(40);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Apply_Player_Defense_Modifier()
    {
        // Arrange — a -10 defense modifier drops the 10 Constitution defense to 0.
        _mediator
            .Setup(m => m.Send(It.Is<ProcessStatusEffectsCommand>(c => c.TargetCharacter != null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult
            {
                TotalStatModifiers = new Dictionary<string, int> { ["defense"] = -10 }
            });
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = CreatePlayer(constitution: 10);

        // Act
        var result = await service.ExecuteEnemyAttack(CreateEnemy(), player);

        // Assert
        result.Damage.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Award_Armor_Skill_XP_For_Each_Equipped_Armor_Piece()
    {
        // Arrange — a helm and chests with skill references plus a piece without one.
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = CreatePlayer(constitution: 0);
        player.EquippedHelmet = new Item
        {
            Name = "Helm",
            Type = ItemType.Helmet,
            Traits = new Dictionary<string, TraitValue>
            {
                ["skillReference"] = new("@skills/armor:plate-armor", TraitType.String)
            }
        };
        player.EquippedChest = new Item
        {
            Name = "Chest",
            Type = ItemType.Chest,
            Traits = new Dictionary<string, TraitValue>
            {
                ["skillReference"] = new("@skills/armor:mail-armor", TraitType.String)
            }
        };
        player.EquippedBoots = new Item
        {
            Name = "Boots",
            Type = ItemType.Boots,
            Traits = new Dictionary<string, TraitValue>
            {
                // No colon — ExtractSkillIdFromReference yields empty and no XP is awarded.
                ["skillReference"] = new("leatherworking", TraitType.String)
            }
        };

        // Act
        await service.ExecuteEnemyAttack(CreateEnemy(), player);

        // Assert
        var awards = SentCommands<AwardSkillXPCommand>();
        awards.Should().HaveCount(2);
        awards.Select(a => a.SkillId).Should().BeEquivalentTo(["plate-armor", "mail-armor"]);
        awards.Should().OnlyContain(a => a.ActionSource == "armor_damage_taken");
    }

    // ── ExecuteEnemyAbility ────────────────────────────────────────────────────

    [Fact]
    public void ExecuteEnemyAbility_With_No_Abilities_Returns_Null()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();

        // Act
        var result = service.ExecuteEnemyAbility(CreateEnemy(), player, new Dictionary<string, int>());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExecuteEnemyAbility_With_All_Abilities_On_Cooldown_Returns_Null()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Abilities = [new Power { Id = "slam", Name = "Slam", EffectType = PowerEffectType.Damage, Cooldown = 3 }];
        var service = CreateService(FakeRandomSource.Returning());
        var states = new Dictionary<string, int> { ["slam"] = 2 };

        // Act
        var result = service.ExecuteEnemyAbility(enemy, CreatePlayer(), states);

        // Assert
        result.Should().BeNull();
        states["slam"].Should().Be(2);
    }

    [Fact]
    public void ExecuteEnemyAbility_Damage_Should_Reduce_Player_Health_And_Set_Cooldown()
    {
        // Arrange — AI picks the highest-priority ability (0), 2d6 rolls 3+4, 100% variance.
        var enemy = CreateEnemy();
        enemy.Intelligence = 0;
        enemy.Abilities =
        [
            new Power
            {
                Id = "slam",
                Name = "Crushing Slam",
                Description = "deals damage",
                EffectType = PowerEffectType.Damage,
                BaseDamage = "2d6",
                Cooldown = 4
            }
        ];
        var service = CreateService(FakeRandomSource.Returning(0, 3, 4, 100));
        var player = CreatePlayer();
        var states = new Dictionary<string, int>();

        // Act
        var result = service.ExecuteEnemyAbility(enemy, player, states);

        // Assert
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.DamageDealt.Should().Be(7);
        result.PowerUsed!.Id.Should().Be("slam");
        result.Message.Should().Be("Orc used Crushing Slam! You took 7 damage!");
        result.ManaCost.Should().Be(0);
        player.Health.Should().Be(93);
        states["slam"].Should().Be(4);
    }

    [Fact]
    public void ExecuteEnemyAbility_Heal_Should_Restore_Health_Capped_At_Max()
    {
        // Arrange — heal ability with 1d6 rolling 6, capped at MaxHealth.
        var enemy = CreateEnemy();
        enemy.Intelligence = 0;
        enemy.Health = 96;
        enemy.MaxHealth = 100;
        enemy.Abilities =
        [
            new Power
            {
                Id = "mend",
                Name = "Mend",
                EffectType = PowerEffectType.Heal,
                BaseDamage = "1d6",
                Cooldown = 2
            }
        ];
        var service = CreateService(FakeRandomSource.Returning(0, 6, 100));
        var player = CreatePlayer();

        // Act
        var result = service.ExecuteEnemyAbility(enemy, player, new Dictionary<string, int>());

        // Assert
        result.Should().NotBeNull();
        result!.HealingDone.Should().Be(6);
        enemy.Health.Should().Be(100);
        result.Message.Should().Be("Orc used Mend and restored 6 health!");
    }

    [Fact]
    public void ExecuteEnemyAbility_Protection_Should_Be_Treated_As_Healing()
    {
        // Arrange — Protection shares the heal arm.
        var enemy = CreateEnemy();
        enemy.Intelligence = 0;
        enemy.Health = 50;
        enemy.Abilities =
        [
            new Power
            {
                Id = "ward",
                Name = "Ward",
                EffectType = PowerEffectType.Protection,
                BaseDamage = "1d4",
                Cooldown = 1
            }
        ];
        var service = CreateService(FakeRandomSource.Returning(0, 4, 100));

        // Act
        var result = service.ExecuteEnemyAbility(enemy, CreatePlayer(), new Dictionary<string, int>());

        // Assert
        result!.HealingDone.Should().Be(4);
        enemy.Health.Should().Be(54);
    }

    [Fact]
    public void ExecuteEnemyAbility_With_Unparsable_Dice_Falls_Back_To_Ten_Damage()
    {
        // Arrange — "abc" is not dice notation, so RollDice returns its 10 fallback.
        var enemy = CreateEnemy();
        enemy.Intelligence = 0;
        enemy.Abilities =
        [
            new Power
            {
                Id = "weird",
                Name = "Weird Strike",
                EffectType = PowerEffectType.Damage,
                BaseDamage = "abc",
                Cooldown = 1
            }
        ];
        var service = CreateService(FakeRandomSource.Returning(0, 100));
        var player = CreatePlayer();

        // Act
        var result = service.ExecuteEnemyAbility(enemy, player, new Dictionary<string, int>());

        // Assert
        result!.DamageDealt.Should().Be(10);
        player.Health.Should().Be(90);
    }

    [Theory]
    [InlineData(PowerEffectType.Buff, "Orc used Battle Cry!")]
    [InlineData(PowerEffectType.Debuff, "Orc used Battle Cry on you!")]
    public void ExecuteEnemyAbility_Non_Damaging_Types_Should_Only_Report_A_Message(
        PowerEffectType effectType,
        string expectedMessage)
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.Abilities =
        [
            new Power { Id = "cry", Name = "Battle Cry", EffectType = effectType, Cooldown = 3 }
        ];
        var service = CreateService(FakeRandomSource.Returning(0));
        var player = CreatePlayer();
        var states = new Dictionary<string, int>();

        // Act
        var result = service.ExecuteEnemyAbility(enemy, player, states);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Be(expectedMessage);
        result.DamageDealt.Should().Be(0);
        result.HealingDone.Should().Be(0);
        player.Health.Should().Be(100);
        states["cry"].Should().Be(3);
    }

    // ── ExecuteEnemySpell ──────────────────────────────────────────────────────

    [Fact]
    public void ExecuteEnemySpell_Without_Spells_Returns_Null()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var catalog = new RealmEngine.Core.Features.Progression.Services.PowerDataService(
            new RealmEngine.Data.Repositories.InMemoryPowerRepository(),
            NullLogger<RealmEngine.Core.Features.Progression.Services.PowerDataService>.Instance);

        // Act
        var result = service.ExecuteEnemySpell(CreateEnemy(), CreatePlayer(), catalog);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExecuteEnemySpell_Without_Mana_Returns_Null()
    {
        // Arrange
        var enemy = CreateEnemy();
        enemy.SpellIds = ["fireball"];
        enemy.Mana = 0;
        var service = CreateService(FakeRandomSource.Returning());
        var catalog = new RealmEngine.Core.Features.Progression.Services.PowerDataService(
            new RealmEngine.Data.Repositories.InMemoryPowerRepository(),
            NullLogger<RealmEngine.Core.Features.Progression.Services.PowerDataService>.Instance);

        // Act
        var result = service.ExecuteEnemySpell(enemy, CreatePlayer(), catalog);

        // Assert
        result.Should().BeNull();
    }

    // ── AttemptFlee ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true, "You successfully fled from combat!")]
    [InlineData(99, false, "Failed to escape! The enemy blocks your path!")]
    public void AttemptFlee_Should_Resolve_Against_The_Dexterity_Difference(int roll, bool expectedSuccess, string message)
    {
        // Arrange — equal DEX gives a flat 50% chance.
        var service = CreateService(FakeRandomSource.Returning(roll));
        var player = CreatePlayer(dexterity: 10);
        var enemy = CreateEnemy();
        enemy.Dexterity = 10;

        // Act
        var result = service.AttemptFlee(player, enemy);

        // Assert
        result.Success.Should().Be(expectedSuccess);
        result.Message.Should().Be(message);
    }

    [Theory]
    [InlineData(60, 10, 50, true)]  // 50 + 250 clamps to 90, a 50 roll still succeeds
    [InlineData(60, 10, 95, false)] // 95 still misses the clamped-to-90 ceiling
    [InlineData(10, 60, 50, false)] // 50 - 250 clamps to 10, a 50 roll fails
    public void AttemptFlee_Should_Clamp_The_Chance_Between_Ten_And_Ninety(
        int playerDexterity,
        int enemyDexterity,
        int roll,
        bool expected)
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning(roll));
        var player = CreatePlayer(dexterity: playerDexterity);
        var enemy = CreateEnemy();
        enemy.Dexterity = enemyDexterity;

        // Act
        var result = service.AttemptFlee(player, enemy);

        // Assert
        result.Success.Should().Be(expected);
    }

    // ── UseItemInCombat ────────────────────────────────────────────────────────

    [Fact]
    public void UseItemInCombat_With_A_Non_Consumable_Should_Fail_And_Keep_The_Item()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();
        var sword = new Item { Name = "Short Sword", Type = ItemType.Weapon };
        player.Inventory = [sword];

        // Act
        var result = service.UseItemInCombat(player, sword);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Short Sword cannot be used in combat.");
        player.Inventory.Should().ContainSingle();
    }

    [Fact]
    public void UseItemInCombat_With_A_Health_Potion_Should_Heal_Thirty_And_Consume_It()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();
        player.Health = 40;
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        player.Inventory = [potion];

        // Act
        var result = service.UseItemInCombat(player, potion);

        // Assert
        result.Success.Should().BeTrue();
        result.Healing.Should().Be(30);
        player.Health.Should().Be(70);
        result.Message.Should().Be("You used Health Potion and restored 30 health!");
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public void UseItemInCombat_Should_Not_Heal_Past_Max_Health()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();
        player.Health = 90;
        player.MaxHealth = 100;
        var potion = new Item { Name = "Potion of Vigour", Type = ItemType.Consumable };
        player.Inventory = [potion];

        // Act
        var result = service.UseItemInCombat(player, potion);

        // Assert
        player.Health.Should().Be(100);
        result.Healing.Should().Be(30);
    }

    [Fact]
    public void UseItemInCombat_With_A_Mana_Draught_Should_Restore_Mana()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();
        player.Mana = 10;
        player.MaxMana = 50;
        var draught = new Item { Name = "Mana Draught", Type = ItemType.Consumable };
        player.Inventory = [draught];

        // Act
        var result = service.UseItemInCombat(player, draught);

        // Assert
        player.Mana.Should().Be(30);
        result.Message.Should().Be("You used Mana Draught and restored 20 mana!");
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public void UseItemInCombat_With_An_Unrecognised_Consumable_Should_Only_Produce_A_Message()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var player = CreatePlayer();
        var food = new Item { Name = "Elven Bread", Type = ItemType.Consumable };
        player.Inventory = [food];

        // Act
        var result = service.UseItemInCombat(player, food);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("You used Elven Bread.");
        result.Healing.Should().Be(0);
        player.Health.Should().Be(100);
        player.Inventory.Should().BeEmpty();
    }

    // ── GenerateVictoryOutcome ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Report_XP_Gold_And_Enemy_Identity()
    {
        // Arrange
        var service = CreateService(FakeRandomSource.Returning());
        var enemy = CreateEnemy("Skeleton King");
        enemy.Id = "skeleton-king";
        enemy.Type = EnemyType.Undead;
        enemy.XP = 80;
        enemy.GoldReward = 40;

        // Act
        var outcome = await service.GenerateVictoryOutcome(CreatePlayer(), enemy);

        // Assert
        outcome.PlayerVictory.Should().BeTrue();
        outcome.XPGained.Should().Be(80);
        outcome.GoldGained.Should().Be(40);
        outcome.DefeatedEnemyId.Should().Be("skeleton-king");
        outcome.DefeatedEnemyType.Should().Be("Undead");
        outcome.LootDropped.Should().BeEmpty();
        outcome.Summary.Should().Contain("Victory! Defeated Skeleton King!");
        outcome.Summary.Should().Contain("+80 XP");
        outcome.Summary.Should().Contain("+40 Gold");
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Award_Twenty_Weapon_Skill_XP()
    {
        // Arrange
        var player = CreatePlayer();
        player.EquippedMainHand = CreateWeapon(ItemRarity.Common, new Dictionary<string, TraitValue>
        {
            ["skillReference"] = new("@skills/weapon:heavy-blades", TraitType.String)
        });
        var service = CreateService(FakeRandomSource.Returning());

        // Act
        await service.GenerateVictoryOutcome(player, CreateEnemy());

        // Assert
        var award = SentCommands<AwardSkillXPCommand>().Should().ContainSingle().Subject;
        award.SkillId.Should().Be("heavy-blades");
        award.XPAmount.Should().Be(20);
        award.ActionSource.Should().Be("heavy_blades_kill");
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Update_Matching_Quest_Objectives()
    {
        // Arrange
        var quest = new QuestModel { Id = "q1", Title = "Cull the Orcs" };
        quest.Objectives["defeat_orc"] = 1;
        quest.Objectives["defeat_beast"] = 2;
        _saveGameService.Setup(s => s.GetCurrentSave()).Returns(new SaveGame
        {
            ActiveQuests = [quest]
        });
        _mediator
            .Setup(m => m.Send(It.IsAny<UpdateQuestProgressCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateQuestProgressResult(true, true, true));
        var service = CreateService(FakeRandomSource.Returning());
        var enemy = CreateEnemy();
        enemy.Type = EnemyType.Beast;

        // Act
        var outcome = await service.GenerateVictoryOutcome(CreatePlayer(), enemy);

        // Assert
        SentCommands<UpdateQuestProgressCommand>().Select(c => c.ObjectiveId)
            .Should().BeEquivalentTo(["defeat_orc", "defeat_beast"]);
        outcome.QuestObjectivesCompleted.Should().BeEquivalentTo(
            ["Cull the Orcs: defeat_orc", "Cull the Orcs: defeat_beast"]);
        outcome.QuestsCompleted.Should().Equal("Cull the Orcs", "Cull the Orcs");
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Skip_Quest_Progress_When_No_Objective_Matches()
    {
        // Arrange
        var quest = new QuestModel { Id = "q1", Title = "Another Task" };
        quest.Objectives["collect_herbs"] = 3;
        _saveGameService.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { ActiveQuests = [quest] });
        var service = CreateService(FakeRandomSource.Returning());

        // Act
        var outcome = await service.GenerateVictoryOutcome(CreatePlayer(), CreateEnemy());

        // Assert
        SentCommands<UpdateQuestProgressCommand>().Should().BeEmpty();
        outcome.QuestObjectivesCompleted.Should().BeEmpty();
        outcome.QuestsCompleted.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Report_Nothing_When_An_Objective_Is_Not_Completed()
    {
        // Arrange
        var quest = new QuestModel { Id = "q1", Title = "Cull the Orcs" };
        quest.Objectives["defeat_orc"] = 5;
        _saveGameService.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { ActiveQuests = [quest] });
        var service = CreateService(FakeRandomSource.Returning());

        // Act
        var outcome = await service.GenerateVictoryOutcome(CreatePlayer(), CreateEnemy());

        // Assert
        SentCommands<UpdateQuestProgressCommand>().Should().ContainSingle();
        outcome.QuestObjectivesCompleted.Should().BeEmpty();
        outcome.QuestsCompleted.Should().BeEmpty();
    }
}
