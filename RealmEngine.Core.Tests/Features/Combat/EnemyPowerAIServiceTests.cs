using System.Collections.Generic;
using FluentAssertions;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Combat;

/// <summary>
/// Tests for <see cref="EnemyPowerAIService"/>.
/// Every test scripts the exact rolls the service should receive, so the assertions describe the
/// priority rules instead of a statistical average — the previous unseeded 100-draw thresholds
/// failed by chance (e.g. 30 buff uses for a <c>&gt; 30</c> assertion) and are gone for good.
/// </summary>
public class EnemyPowerAIServiceTests
{
    [Fact]
    public void DecideAbilityUsage_Should_Return_Null_When_Enemy_Has_No_Abilities()
    {
        // Arrange
        var random = FakeRandomSource.Returning();
        var aiService = new EnemyPowerAIService(randomSource: random);
        var enemy = new Enemy
        {
            Name = "Goblin",
            Health = 50,
            MaxHealth = 50,
            Abilities = new List<Power>()
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var result = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        result.Should().BeNull();
        random.IntCallCount.Should().Be(0, "no roll is needed when there is nothing to choose from");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Return_Null_When_All_Abilities_On_Cooldown()
    {
        // Arrange
        var random = FakeRandomSource.Returning();
        var aiService = new EnemyPowerAIService(randomSource: random);
        var ability1 = new Power
        {
            Id = "fireball",
            Name = "Fireball",
            EffectType = PowerEffectType.Damage,
            Cooldown = 3
        };
        var ability2 = new Power
        {
            Id = "heal",
            Name = "Heal",
            EffectType = PowerEffectType.Heal,
            Cooldown = 5
        };

        var enemy = new Enemy
        {
            Name = "Wizard",
            Health = 80,
            MaxHealth = 100,
            Abilities = new List<Power> { ability1, ability2 }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>
        {
            { "fireball", 2 },  // Still on cooldown
            { "heal", 3 }       // Still on cooldown
        };

        // Act
        var result = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        result.Should().BeNull("all abilities are on cooldown");
        random.IntCallCount.Should().Be(0, "cooldown filtering happens before any roll");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Prefer_Defensive_Abilities_When_Low_Health()
    {
        // Arrange: at 15% health the heal has priority 70 and the attack only 20.
        // First call: 0  < 70 -> emergency-heal. Second call: 70 < 70 is false, then 19 < 20 -> attack.
        var random = FakeRandomSource.Returning(0, 70, 19);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var healAbility = new Power
        {
            Id = "emergency-heal",
            Name = "Emergency Heal",
            Description = "Restore health to the caster",
            EffectType = PowerEffectType.Heal,
            Cooldown = 5
        };
        var attackAbility = new Power
        {
            Id = "attack",
            Name = "Attack",
            EffectType = PowerEffectType.Damage,
            Cooldown = 0
        };

        var enemy = new Enemy
        {
            Name = "Cleric",
            Health = 15,  // 15% health
            MaxHealth = 100,
            Abilities = new List<Power> { healAbility, attackAbility }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var firstChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);
        var secondChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        firstChoice.Should().Be("emergency-heal", "a draw below 70 picks the 70-priority defensive ability");
        secondChoice.Should().Be("attack", "when the heal roll fails, the 20-priority attack is the next candidate");
        random.IntCallCount.Should().Be(3, "one roll for the heal, then one failed and one successful roll for the attack");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Prefer_Offensive_Abilities_When_High_Health()
    {
        // Arrange: at 90% health the damaging ability has priority 40 and the heal only 20.
        // First call: 39 < 40 -> fireball. Second call: 40 < 40 is false, then 19 < 20 -> heal.
        var random = FakeRandomSource.Returning(39, 40, 19);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var attackAbility = new Power
        {
            Id = "fireball",
            Name = "Fireball",
            Description = "Deal massive damage",
            EffectType = PowerEffectType.Damage,
            Cooldown = 2
        };
        var healAbility = new Power
        {
            Id = "heal",
            Name = "Heal",
            EffectType = PowerEffectType.Heal,
            Cooldown = 5
        };

        var enemy = new Enemy
        {
            Name = "Wizard",
            Health = 90,  // 90% health
            MaxHealth = 100,
            Abilities = new List<Power> { attackAbility, healAbility }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var firstChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);
        var secondChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        firstChoice.Should().Be("fireball", "a draw below 40 picks the 40-priority offensive ability");
        secondChoice.Should().Be("heal", "when the offensive roll fails, the 20-priority heal is the next candidate");
        random.IntCallCount.Should().Be(3, "one roll for fireball, then one failed and one successful roll for heal");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Prefer_Buffs_At_Start_Of_Combat()
    {
        // Arrange: at full health the buff has priority 50 and the attack 40.
        // First call: 49 < 50 -> strengthen. Second call: 50 < 50 is false, then 39 < 40 -> slash.
        // The buff description deliberately avoids the words IsOffensiveAbility matches
        // ("attack"/"damage"); otherwise the offensive branch (40) would claim the buff first and
        // the priority-50 buff branch would never run.
        var random = FakeRandomSource.Returning(49, 50, 39);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var buffAbility = new Power
        {
            Id = "strengthen",
            Name = "Strengthen",
            Description = "Enhance power",
            EffectType = PowerEffectType.Buff,
            Cooldown = 0
        };
        var attackAbility = new Power
        {
            Id = "slash",
            Name = "Slash",
            EffectType = PowerEffectType.Damage,
            Cooldown = 0
        };

        var enemy = new Enemy
        {
            Name = "Warrior",
            Health = 100,  // Full health
            MaxHealth = 100,
            Abilities = new List<Power> { buffAbility, attackAbility }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var firstChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);
        var secondChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        firstChoice.Should().Be("strengthen", "a draw below 50 picks the 50-priority buff at the start of combat");
        secondChoice.Should().Be("slash", "when the buff roll fails, the 40-priority attack is the next candidate");
        random.IntCallCount.Should().Be(3, "one roll for the buff, then one failed and one successful roll for slash");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Consider_Debuffs_When_Player_Strong()
    {
        // Arrange: player at 90% health gives the debuff priority 45.
        // First call: 44 < 45 -> weaken. Second call: 45 < 45 is false, the 20% fallback roll
        // succeeds (19) and index 0 picks the only available ability.
        var random = FakeRandomSource.Returning(44, 45, 19, 0);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var debuffAbility = new Power
        {
            Id = "weaken",
            Name = "Weaken",
            Description = "Reduce enemy defense",
            EffectType = PowerEffectType.Debuff,
            Cooldown = 3
        };

        var enemy = new Enemy
        {
            Name = "Necromancer",
            Health = 60,
            MaxHealth = 100,
            Abilities = new List<Power> { debuffAbility }
        };
        var player = new Character
        {
            Name = "Hero",
            Health = 90,  // Strong player (>70%)
            MaxHealth = 100
        };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var firstChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);
        var secondChoice = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        firstChoice.Should().Be("weaken", "a draw below 45 picks the 45-priority debuff");
        secondChoice.Should().Be("weaken", "the 20% fallback roll can still pick an available ability");
        random.IntCallCount.Should().Be(4, "one debuff roll, one failed debuff roll, the fallback roll and the index roll");
    }

    [Fact]
    public void DecideAbilityUsage_Should_Skip_Abilities_On_Cooldown()
    {
        // Arrange: big-spell is filtered out before priorities are calculated, so only
        // small-spell (priority 40 above 50% health) is ever offered to the roll.
        var random = FakeRandomSource.Returning(39);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var onCooldownAbility = new Power
        {
            Id = "big-spell",
            Name = "Big Spell",
            EffectType = PowerEffectType.Damage,
            Cooldown = 5
        };
        var availableAbility = new Power
        {
            Id = "small-spell",
            Name = "Small Spell",
            EffectType = PowerEffectType.Damage,
            Cooldown = 1
        };

        var enemy = new Enemy
        {
            Name = "Mage",
            Health = 80,
            MaxHealth = 100,
            Abilities = new List<Power> { onCooldownAbility, availableAbility }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>
        {
            { "big-spell", 3 }  // On cooldown
        };

        // Act
        var result = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        result.Should().Be("small-spell", "the ability on cooldown is not a candidate");
        random.IntCallCount.Should().Be(1);
    }

    [Fact]
    public void DecideAbilityUsage_Should_Return_Null_When_Priority_And_Fallback_Rolls_Fail()
    {
        // Arrange: at 50% health the only ability has priority 20.
        // 20 < 20 is false for the priority roll and 20 < 20 is false for the fallback roll.
        var random = FakeRandomSource.Returning(20, 20);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var ability = new Power
        {
            Id = "weak-spell",
            Name = "Weak Spell",
            EffectType = PowerEffectType.Damage,
            Cooldown = 1
        };

        var enemy = new Enemy
        {
            Name = "Apprentice",
            Health = 50,
            MaxHealth = 100,
            Abilities = new List<Power> { ability }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var result = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        result.Should().BeNull("both the priority roll and the fallback roll failed, so the enemy uses a basic attack");
        random.IntCallCount.Should().Be(2);
    }

    [Fact]
    public void DecideAbilityUsage_Should_Use_Fallback_Index_When_Priority_Rolls_Fail()
    {
        // Arrange: both abilities have priority 20 at 50% health. After both priority rolls fail,
        // the fallback roll (19 < 20) succeeds and index 1 selects the second available ability.
        var random = FakeRandomSource.Returning(20, 20, 19, 1);
        var aiService = new EnemyPowerAIService(randomSource: random);
        var firstAbility = new Power
        {
            Id = "alpha-strike",
            Name = "Alpha Strike",
            EffectType = PowerEffectType.Damage,
            Cooldown = 0
        };
        var secondAbility = new Power
        {
            Id = "beta-strike",
            Name = "Beta Strike",
            EffectType = PowerEffectType.Damage,
            Cooldown = 0
        };

        var enemy = new Enemy
        {
            Name = "Duelist",
            Health = 50,
            MaxHealth = 100,
            Abilities = new List<Power> { firstAbility, secondAbility }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var abilityStates = new Dictionary<string, int>();

        // Act
        var result = aiService.DecideAbilityUsage(enemy, player, abilityStates);

        // Assert
        result.Should().Be("beta-strike", "the fallback roll indexes into the available-ability list");
        random.IntCallCount.Should().Be(4, "two priority rolls, the fallback roll and the index roll");
    }
}
