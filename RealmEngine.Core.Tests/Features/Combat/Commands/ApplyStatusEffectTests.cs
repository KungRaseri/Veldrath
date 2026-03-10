using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
public class ApplyStatusEffectTests
{
    private readonly Mock<ILogger<ApplyStatusEffectHandler>> _mockLogger;
    private readonly ApplyStatusEffectHandler _handler;

    public ApplyStatusEffectTests()
    {
        _mockLogger = new Mock<ILogger<ApplyStatusEffectHandler>>();
        _handler = new ApplyStatusEffectHandler(_mockLogger.Object);
    }

    [Fact]
    public async Task Should_Apply_Status_Effect_To_Character()
    {
        // Arrange — Wisdom = 0 ensures no random resistance roll (Wisdom/10 = 0%)
        var character = new Character { Name = "Hero", Health = 100, MaxHealth = 100, Wisdom = 0 };
        var effect = new StatusEffect
        {
            Id = "poison1",
            Type = StatusEffectType.Poisoned,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Poison",
            RemainingDuration = 3,
            OriginalDuration = 3,
            TickDamage = 5
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = effect
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        character.ActiveStatusEffects.Should().HaveCount(1);
        character.ActiveStatusEffects[0].Type.Should().Be(StatusEffectType.Poisoned);
    }

    [Fact]
    public async Task Should_Apply_Status_Effect_To_Enemy()
    {
        // Arrange
        var enemy = new Enemy 
        { 
            Name = "Goblin", 
            Health = 30, 
            MaxHealth = 30,
            Traits = new Dictionary<string, TraitValue>()
        };
        var effect = new StatusEffect
        {
            Id = "burn1",
            Type = StatusEffectType.Burning,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Burning",
            RemainingDuration = 2,
            OriginalDuration = 2,
            TickDamage = 3,
            DamageType = "fire"
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = effect
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        enemy.ActiveStatusEffects.Should().HaveCount(1);
        enemy.ActiveStatusEffects[0].Type.Should().Be(StatusEffectType.Burning);
    }

    [Fact]
    public async Task Should_Fail_When_No_Target_Specified()
    {
        // Arrange
        var effect = new StatusEffect
        {
            Id = "poison1",
            Type = StatusEffectType.Poisoned,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Poison",
            RemainingDuration = 3
        };

        var command = new ApplyStatusEffectCommand { Effect = effect };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("target");
    }

    [Fact]
    public async Task Should_Stack_Effect_When_Allowed()
    {
        // Arrange
        var character = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var existingEffect = new StatusEffect
        {
            Id = "bleed1",
            Type = StatusEffectType.Bleeding,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Bleeding",
            RemainingDuration = 2,
            OriginalDuration = 2,
            TickDamage = 4,
            CanStack = true,
            MaxStacks = 5,
            StackCount = 1
        };
        character.ActiveStatusEffects.Add(existingEffect);

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "bleed2",
                Type = StatusEffectType.Bleeding,
                Category = StatusEffectCategory.DamageOverTime,
                Name = "Bleeding",
                RemainingDuration = 2
            },
            AllowStacking = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Stacked.Should().BeTrue();
        result.CurrentStacks.Should().Be(2);
        character.ActiveStatusEffects.Should().HaveCount(1);
        character.ActiveStatusEffects[0].StackCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_Not_Stack_Beyond_Max_Stacks()
    {
        // Arrange
        var character = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var existingEffect = new StatusEffect
        {
            Id = "poison1",
            Type = StatusEffectType.Poisoned,
            Category = StatusEffectCategory.DamageOverTime,
            Name = "Poison",
            RemainingDuration = 3,
            OriginalDuration = 3,
            TickDamage = 5,
            CanStack = true,
            MaxStacks = 3,
            StackCount = 3
        };
        character.ActiveStatusEffects.Add(existingEffect);

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "poison2",
                Type = StatusEffectType.Poisoned,
                Category = StatusEffectCategory.DamageOverTime,
                Name = "Poison",
                RemainingDuration = 3
            },
            AllowStacking = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("max stacks");
        character.ActiveStatusEffects[0].StackCount.Should().Be(3);
    }

    [Fact]
    public async Task Should_Refresh_Duration_When_Enabled()
    {
        // Arrange
        var character = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var existingEffect = new StatusEffect
        {
            Id = "shield1",
            Type = StatusEffectType.Shielded,
            Category = StatusEffectCategory.Buff,
            Name = "Shield",
            RemainingDuration = 1,
            OriginalDuration = 2,
            CanStack = false
        };
        character.ActiveStatusEffects.Add(existingEffect);

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "shield2",
                Type = StatusEffectType.Shielded,
                Category = StatusEffectCategory.Buff,
                Name = "Shield",
                RemainingDuration = 2,
                OriginalDuration = 2
            },
            RefreshDuration = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DurationRefreshed.Should().BeTrue();
        character.ActiveStatusEffects.Should().HaveCount(1);
        character.ActiveStatusEffects[0].RemainingDuration.Should().Be(2);
    }

    [Fact]
    public async Task Should_Resist_Effect_With_100_Percent_Resistance()
    {
        // Arrange
        var enemy = new Enemy 
        { 
            Name = "Fire Elemental", 
            Health = 50, 
            MaxHealth = 50,
            Traits = new Dictionary<string, TraitValue>
            {
                { "resistFire", new TraitValue(100, TraitType.Number) }
            }
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = new StatusEffect
            {
                Id = "burn1",
                Type = StatusEffectType.Burning,
                Category = StatusEffectCategory.DamageOverTime,
                Name = "Burning",
                RemainingDuration = 2,
                DamageType = "fire"
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Resisted.Should().BeTrue();
        result.ResistancePercentage.Should().Be(100);
        enemy.ActiveStatusEffects.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Be_Immune_To_Poison()
    {
        // Arrange
        var enemy = new Enemy 
        { 
            Name = "Undead", 
            Health = 40, 
            MaxHealth = 40,
            Traits = new Dictionary<string, TraitValue>
            {
                { "immuneToPoison", new TraitValue(true, TraitType.Boolean) }
            }
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = new StatusEffect
            {
                Id = "poison1",
                Type = StatusEffectType.Poisoned,
                Category = StatusEffectCategory.DamageOverTime,
                Name = "Poison",
                RemainingDuration = 3,
                TickDamage = 5
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("immune");
        enemy.ActiveStatusEffects.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Calculate_Character_Resistance_From_Wisdom()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "Hero", 
            Health = 100, 
            MaxHealth = 100,
            Wisdom = 50 // 50 Wisdom = 5% resistance
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "curse1",
                Type = StatusEffectType.Cursed,
                Category = StatusEffectCategory.Debuff,
                Name = "Curse",
                RemainingDuration = 3
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ResistancePercentage.Should().Be(5);
    }

    [Fact]
    public async Task Should_Apply_Magic_Resistance_To_Status_Effects()
    {
        // Arrange
        var enemy = new Enemy 
        { 
            Name = "Mage", 
            Health = 60, 
            MaxHealth = 60,
            Traits = new Dictionary<string, TraitValue>
            {
                { "resistMagic", new TraitValue(40, TraitType.Number) } // 40% magic resistance
            }
        };

        var command = new ApplyStatusEffectCommand
        {
            TargetEnemy = enemy,
            Effect = new StatusEffect
            {
                Id = "silence1",
                Type = StatusEffectType.Silenced,
                Category = StatusEffectCategory.CrowdControl,
                Name = "Silence",
                RemainingDuration = 2,
                DamageType = "magic"
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ResistancePercentage.Should().Be(20); // Half of magic resistance applies
    }

    [Fact]
    public async Task Should_Not_Apply_Same_Effect_Twice_Without_Stacking()
    {
        // Arrange
        var character = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var existingEffect = new StatusEffect
        {
            Id = "freeze1",
            Type = StatusEffectType.Frozen,
            Category = StatusEffectCategory.CrowdControl,
            Name = "Frozen",
            RemainingDuration = 2,
            OriginalDuration = 2,
            CanStack = false
        };
        character.ActiveStatusEffects.Add(existingEffect);

        var command = new ApplyStatusEffectCommand
        {
            TargetCharacter = character,
            Effect = new StatusEffect
            {
                Id = "freeze2",
                Type = StatusEffectType.Frozen,
                Category = StatusEffectCategory.CrowdControl,
                Name = "Frozen",
                RemainingDuration = 2
            },
            AllowStacking = false,
            RefreshDuration = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already affected");
        character.ActiveStatusEffects.Should().HaveCount(1);
    }
}
