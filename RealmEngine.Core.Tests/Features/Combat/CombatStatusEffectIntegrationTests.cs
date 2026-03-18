using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Combat;

/// <summary>
/// Integration tests for combat status effects system.
/// Tests the interaction between ProcessStatusEffectsCommand, ApplyStatusEffectCommand,
/// and combat damage calculations with stat modifiers.
/// </summary>
[Trait("Category", "Integration")]
public class CombatStatusEffectIntegrationTests
{
    private readonly Mock<ILogger<ProcessStatusEffectsHandler>> _mockStatusLogger;
    private readonly Mock<ILogger<ApplyStatusEffectHandler>> _mockApplyLogger;
    private readonly ProcessStatusEffectsHandler _processHandler;
    private readonly ApplyStatusEffectHandler _applyHandler;

    public CombatStatusEffectIntegrationTests()
    {
        _mockStatusLogger = new Mock<ILogger<ProcessStatusEffectsHandler>>();
        _mockApplyLogger = new Mock<ILogger<ApplyStatusEffectHandler>>();

        _processHandler = new ProcessStatusEffectsHandler(_mockStatusLogger.Object);
        _applyHandler = new ApplyStatusEffectHandler(_mockApplyLogger.Object);
    }

    [Fact]
    public async Task ProcessStatusEffects_Should_Apply_DoT_Damage_And_Accumulate_Stat_Modifiers()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {                new StatusEffect
                {
                    Id = "poison1",
                    Type = StatusEffectType.Poisoned,
                    Category = StatusEffectCategory.DamageOverTime,
                    Name = "Poisoned",
                    OriginalDuration = 3,
                    RemainingDuration = 3,
                    TickDamage = 4,
                    DamageType = "poison"
                },
                new StatusEffect
                {
                    Id = "strength1",
                    Type = StatusEffectType.Strengthened,
                    Category = StatusEffectCategory.Buff,
                    Name = "Strengthened",
                    OriginalDuration = 5,
                    RemainingDuration = 5,
                    StatModifiers = new Dictionary<string, int> { { "attack", 10 } }
                }
            }
        };

        var command = new ProcessStatusEffectsCommand { TargetCharacter = character };

        // Act
        var result = await _processHandler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalDamageTaken.Should().Be(4, "poison deals 4 DoT damage");
        result.TotalStatModifiers.Should().ContainKey("attack");
        result.TotalStatModifiers["attack"].Should().Be(10, "strengthened adds +10 attack");
        character.Health.Should().Be(96, "health reduced by DoT");
        character.ActiveStatusEffects.Should().HaveCount(2, "both effects still active");
    }

    [Fact]
    public async Task ApplyStatusEffect_Should_Stack_Burning_Up_To_MaxStacks()
    {
        // Arrange
        var enemy = new Enemy
        {
            Name = "Orc",
            Health = 100,
            MaxHealth = 100
        };

        var burningEffect = new StatusEffect
        {
            Id = "burn1",
            Type = StatusEffectType.Burning,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Burning",
            OriginalDuration = 3,
            RemainingDuration = 3,
            TickDamage = 5,
            DamageType = "fire",
            CanStack = true,
            MaxStacks = 5
        };

        // Act - Apply 3 stacks of burning
        for (int i = 0; i < 3; i++)
        {
            await _applyHandler.Handle(new ApplyStatusEffectCommand
            {
                TargetEnemy = enemy,
                Effect = new StatusEffect
                {
                    Id = $"burn{i + 1}",
                    Type = burningEffect.Type,
                    Category = burningEffect.Category,
                    Name = burningEffect.Name,
                    OriginalDuration = burningEffect.RemainingDuration,
                    RemainingDuration = burningEffect.RemainingDuration,
                    TickDamage = burningEffect.TickDamage,
                    DamageType = burningEffect.DamageType,
                    CanStack = burningEffect.CanStack,
                    MaxStacks = burningEffect.MaxStacks
                },
                AllowStacking = true
            }, CancellationToken.None);
        }

        // Process status effects to verify stacking damage
        var result = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetEnemy = enemy }, CancellationToken.None);

        // Assert
        enemy.ActiveStatusEffects.Should().HaveCount(1, "burning stacks consolidate into one effect");
        enemy.ActiveStatusEffects[0].StackCount.Should().Be(3, "3 stacks applied");
        result.TotalDamageTaken.Should().Be(15, "3 stacks * 5 damage = 15");
        enemy.Health.Should().Be(85, "100 - 15 = 85");
    }

    [Fact]
    public async Task StatusEffect_Should_Expire_After_Duration_Reaches_Zero()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "poison1",
                    Type = StatusEffectType.Poisoned,
                    Category = StatusEffectCategory.DamageOverTime,
                    Name = "Poisoned",
                    OriginalDuration = 2,
                    RemainingDuration = 2,
                    TickDamage = 3,
                    DamageType = "poison"
                }
            }
        };

        // Act - Process for 2 turns to exhaust duration
        await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);
        character.ActiveStatusEffects.Should().HaveCount(1, "effect still active after turn 1");

        var finalResult = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);

        // Assert
        character.ActiveStatusEffects.Should().BeEmpty("effect expires after duration reaches 0");
        finalResult.ExpiredEffectTypes.Should().Contain(StatusEffectType.Poisoned);
        character.Health.Should().Be(94, "100 - (3 * 2) = 94");
    }

    [Fact]
    public async Task Multiple_Stat_Modifiers_Should_Accumulate()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "strength1",
                    Type = StatusEffectType.Strengthened,
                    Category = StatusEffectCategory.Buff,
                    Name = "Strengthened",
                    OriginalDuration = 5,
                    RemainingDuration = 5,
                    StatModifiers = new Dictionary<string, int> { { "attack", 10 } }
                },
                new StatusEffect
                {
                    Id = "blessed1",
                    Type = StatusEffectType.Blessed,
                    Category = StatusEffectCategory.Buff,
                    Name = "Blessed",
                    OriginalDuration = 5,
                    RemainingDuration = 5,
                    StatModifiers = new Dictionary<string, int>
                    {
                        { "attack", 5 },
                        { "defense", 5 }
                    }
                }
            }
        };

        // Act
        var result = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);

        // Assert
        result.TotalStatModifiers.Should().ContainKey("attack");
        result.TotalStatModifiers["attack"].Should().Be(15, "10 from strengthened + 5 from blessed");
        result.TotalStatModifiers.Should().ContainKey("defense");
        result.TotalStatModifiers["defense"].Should().Be(5, "5 from blessed");
    }

    [Fact]
    public async Task HoT_Should_Heal_Character_Up_To_MaxHealth()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 50,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "regen1",
                    Type = StatusEffectType.Regenerating,
                    Category = StatusEffectCategory.HealOverTime,
                    Name = "Regeneration",
                    OriginalDuration = 4,
                    RemainingDuration = 4,
                    TickHealing = 8
                }
            }
        };

        // Act - Process 4 turns
        for (int i = 0; i < 4; i++)
        {
            await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);
        }

        // Assert
        character.Health.Should().Be(82, "50 + (8 * 4) = 82");
        character.ActiveStatusEffects.Should().BeEmpty("regeneration expires after 4 turns");
    }

    [Fact]
    public async Task ApplyStatusEffect_Should_Respect_Immunity()
    {
        // Arrange — enemy is immune to poison via the immuneToPoison trait
        var enemy = new Enemy
        {
            Name = "Undead Skeleton",
            Health = 100,
            MaxHealth = 100,
            Traits = new Dictionary<string, TraitValue>
            {
                { "immuneToPoison", new TraitValue { Value = "true" } }
            }
        };

        var poisonEffect = new StatusEffect
        {
            Id = "poison1",
            Type = StatusEffectType.Poisoned,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Poisoned",
            OriginalDuration = 3,
            RemainingDuration = 3,
            TickDamage = 5,
            DamageType = "poison"
        };

        // Act
        var result = await _applyHandler.Handle(new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = poisonEffect
        }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse("immune enemy should not be affected");
        result.Resisted.Should().BeTrue("result should report immunity as a resist");
        result.ResistancePercentage.Should().Be(100);
        enemy.ActiveStatusEffects.Should().BeEmpty("no effect should be applied to an immune target");
    }

    [Fact]
    public async Task ApplyStatusEffect_Should_Respect_Resistance()
    {
        // Arrange — enemy has 100% fire resistance so burning is guaranteed to be resisted
        var enemy = new Enemy
        {
            Name = "Fire Elemental",
            Health = 100,
            MaxHealth = 100,
            Traits = new Dictionary<string, TraitValue>
            {
                { "resistFire", new TraitValue { Value = "100" } }
            }
        };

        var burningEffect = new StatusEffect
        {
            Id = "burn1",
            Type = StatusEffectType.Burning,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Burning",
            OriginalDuration = 3,
            RemainingDuration = 3,
            TickDamage = 8,
            DamageType = "fire"
        };

        // Act
        var result = await _applyHandler.Handle(new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = burningEffect
        }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse("100% resistance should always resist the effect");
        result.Resisted.Should().BeTrue();
        result.ResistancePercentage.Should().Be(100);
        enemy.ActiveStatusEffects.Should().BeEmpty("fully resistant enemy should not have the effect applied");
    }

    [Fact]
    public async Task Stacking_Should_Respect_MaxStacks_Limit()
    {
        // Arrange
        var enemy = new Enemy
        {
            Name = "Target",
            Health = 100,
            MaxHealth = 100
        };

        var bleedEffect = new StatusEffect
        {
            Id = "bleed_base",
            Type = StatusEffectType.Bleeding,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Bleeding",
            OriginalDuration = 3,
            RemainingDuration = 3,
            TickDamage = 3,
            DamageType = "physical",
            CanStack = true,
            MaxStacks = 3
        };

        // Act - Try to apply 5 stacks (max is 3)
        for (int i = 0; i < 5; i++)
        {
            await _applyHandler.Handle(new ApplyStatusEffectCommand
            {
                TargetEnemy = enemy,
                Effect = new StatusEffect
                {
                    Id = $"bleed{i + 1}",
                    Type = bleedEffect.Type,
                    Category = bleedEffect.Category,
                    Name = bleedEffect.Name,
                    OriginalDuration = bleedEffect.RemainingDuration,
                    RemainingDuration = bleedEffect.RemainingDuration,
                    TickDamage = bleedEffect.TickDamage,
                    DamageType = bleedEffect.DamageType,
                    CanStack = bleedEffect.CanStack,
                    MaxStacks = bleedEffect.MaxStacks
                },
                AllowStacking = true
            }, CancellationToken.None);
        }

        // Assert
        enemy.ActiveStatusEffects.Should().HaveCount(1);
        enemy.ActiveStatusEffects[0].StackCount.Should().Be(3, "capped at MaxStacks of 3");
    }

    [Fact]
    public async Task CrowdControl_Effects_Should_Not_Tick_Damage()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "stun1",
                    Type = StatusEffectType.Stunned,
                    Category = StatusEffectCategory.CrowdControl,
                    Name = "Stunned",
                    OriginalDuration = 2,
                    RemainingDuration = 2
                }
            }
        };

        // Act
        var result = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);

        // Assert
        result.TotalDamageTaken.Should().Be(0, "crowd control effects don't deal damage");
        result.TotalHealingReceived.Should().Be(0);
        character.Health.Should().Be(100, "health unchanged");
        character.ActiveStatusEffects[0].RemainingDuration.Should().Be(1, "duration still decrements");
    }

    [Fact]
    public async Task Debuffs_With_Negative_Stat_Modifiers_Should_Accumulate_Correctly()
    {
        // Arrange
        var enemy = new Enemy
        {
            Name = "Weakened Orc",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "weak1",
                    Type = StatusEffectType.Weakened,
                    Category = StatusEffectCategory.Debuff,
                    Name = "Weakened",
                    OriginalDuration = 3,
                    RemainingDuration = 3,
                    StatModifiers = new Dictionary<string, int> { { "attack", -5 } }
                },
                new StatusEffect
                {
                    Id = "cursed1",
                    Type = StatusEffectType.Cursed,
                    Category = StatusEffectCategory.Debuff,
                    Name = "Cursed",
                    OriginalDuration = 4,
                    RemainingDuration = 4,
                    StatModifiers = new Dictionary<string, int>
                    {
                        { "attack", -3 },
                        { "defense", -3 }
                    }
                }
            }
        };

        // Act
        var result = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetEnemy = enemy }, CancellationToken.None);

        // Assert
        result.TotalStatModifiers["attack"].Should().Be(-8, "-5 from weakened + -3 from cursed");
        result.TotalStatModifiers["defense"].Should().Be(-3, "-3 from cursed");
    }

    [Fact]
    public async Task RefreshDuration_Should_Reset_Effect_Duration()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "strength1",
                    Type = StatusEffectType.Strengthened,
                    Category = StatusEffectCategory.Buff,
                    Name = "Strengthened",
                    OriginalDuration = 1,
                    RemainingDuration = 1, // About to expire
                    StatModifiers = new Dictionary<string, int> { { "attack", 10 } }
                }
            }
        };

        // Act - Reapply same effect with RefreshDuration = true
        await _applyHandler.Handle(new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "strength2",
                Type = StatusEffectType.Strengthened,
                Category = StatusEffectCategory.Buff,
                Name = "Strengthened",                OriginalDuration = 5,                RemainingDuration = 5,
                StatModifiers = new Dictionary<string, int> { { "attack", 10 } }
            },
            RefreshDuration = true
        }, CancellationToken.None);

        // Assert
        character.ActiveStatusEffects.Should().HaveCount(1, "refreshed existing effect");
        character.ActiveStatusEffects[0].RemainingDuration.Should().Be(5, "duration reset to 5");
    }

    [Fact]
    public async Task ProcessStatusEffects_Should_Track_Expired_Effects()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            ActiveStatusEffects = new List<StatusEffect>
            {
                new StatusEffect
                {
                    Id = "poison1",
                    Type = StatusEffectType.Poisoned,
                    Category = StatusEffectCategory.DamageOverTime,
                    Name = "Poisoned",
                    OriginalDuration = 1,
                    RemainingDuration = 1 // Will expire this turn
                },
                new StatusEffect
                {
                    Id = "strength1",
                    Type = StatusEffectType.Strengthened,
                    Category = StatusEffectCategory.Buff,
                    Name = "Strengthened",
                    OriginalDuration = 1,
                    RemainingDuration = 1 // Will expire this turn
                }
            }
        };

        // Act
        var result = await _processHandler.Handle(new ProcessStatusEffectsCommand { TargetCharacter = character }, CancellationToken.None);

        // Assert
        result.ExpiredEffectTypes.Should().HaveCount(2);
        result.ExpiredEffectTypes.Should().Contain(StatusEffectType.Poisoned);
        result.ExpiredEffectTypes.Should().Contain(StatusEffectType.Strengthened);
        character.ActiveStatusEffects.Should().BeEmpty("both effects expired");
    }
}
