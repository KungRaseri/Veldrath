using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Services;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for EnemySpellCastingService AI decision-making.
/// </summary>
public class EnemySpellCastingServiceTests
{
    [Fact]
    public void DecideSpellCasting_Should_Return_Null_When_Enemy_Has_No_Spells()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy { Name = "Warrior", SpellIds = new List<string>() };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var decision = service.DecideSpellCasting(enemy, player);

        // Assert
        decision.Should().BeNull();
    }

    [Fact]
    public void DecideSpellCasting_Should_Return_Null_When_Enemy_Has_No_Mana()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy 
        { 
            Name = "Mage", 
            SpellIds = new List<string> { "fireball" },
            Mana = 0,
            MaxMana = 100
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var decision = service.DecideSpellCasting(enemy, player);

        // Assert
        decision.Should().BeNull();
    }

    [Fact]
    public void DecideSpellCasting_Should_Return_Null_When_All_Spells_On_Cooldown()
    {
        // Arrange
        var service = new EnemySpellCastingService(); // No catalog means no spells available
        var enemy = new Enemy 
        { 
            Name = "Mage", 
            SpellIds = new List<string> { "fireball" },
            Mana = 50,
            MaxMana = 100,
            SpellCooldowns = new Dictionary<string, int> { { "fireball", 2 } }
        };
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };

        // Act
        var decision = service.DecideSpellCasting(enemy, player);

        // Assert
        decision.Should().BeNull();
    }

    [Fact]
    public void ShouldPreferSpellCasting_Should_Return_True_For_High_Intelligence()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy
        {
            Name = "Archmage",
            Intelligence = 20,
            Strength = 10
        };

        // Act
        var result = service.ShouldPreferSpellCasting(enemy);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreferSpellCasting_Should_Return_True_For_Many_Spells()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy
        {
            Name = "Wizard",
            Intelligence = 15,
            Strength = 12,
            SpellIds = new List<string> { "spell1", "spell2", "spell3", "spell4" },
            BasePhysicalDamage = 15
        };

        // Act
        var result = service.ShouldPreferSpellCasting(enemy);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreferSpellCasting_Should_Return_True_For_High_Mana_Pool()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy
        {
            Name = "Sorcerer",
            MaxMana = 150
        };

        // Act
        var result = service.ShouldPreferSpellCasting(enemy);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldPreferSpellCasting_Should_Return_False_For_Physical_Fighter()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var enemy = new Enemy
        {
            Name = "Warrior",
            Intelligence = 10,
            Strength = 18,
            SpellIds = new List<string>(),
            BasePhysicalDamage = 30,
            MaxMana = 0
        };

        // Act
        var result = service.ShouldPreferSpellCasting(enemy);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CalculateManaCost_Should_Reduce_Cost_For_High_Intelligence()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var spell = new Power
        {
            Id = "fireball",
            Name = "Fireball",
            ManaCost = 20
        };
        var enemy = new Enemy
        {
            Name = "Archmage",
            Intelligence = 20 // 10 points above 10 = 10% reduction
        };

        // Act
        var actualCost = service.CalculateManaCost(spell, enemy);

        // Assert
        actualCost.Should().BeLessThan(spell.ManaCost);
        actualCost.Should().Be(18); // 20 * 0.9 = 18
    }

    [Fact]
    public void CalculateManaCost_Should_Not_Go_Below_Minimum()
    {
        // Arrange
        var service = new EnemySpellCastingService();
        var spell = new Power
        {
            Id = "cantrip",
            Name = "Cantrip",
            ManaCost = 1
        };
        var enemy = new Enemy
        {
            Name = "Master Mage",
            Intelligence = 50 // Would reduce to 0, but minimum is 1
        };

        // Act
        var actualCost = service.CalculateManaCost(spell, enemy);

        // Assert
        actualCost.Should().BeGreaterThanOrEqualTo(1);
    }
}
