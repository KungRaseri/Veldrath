using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
public class EnemyTests
{
    [Fact]
    public void Enemy_Should_Initialize_With_Default_Values()
    {
        // Act
        var enemy = new Enemy();

        // Assert
        enemy.Id.Should().NotBeNullOrEmpty();
        enemy.Name.Should().BeEmpty();
        enemy.Description.Should().BeEmpty();
        enemy.Traits.Should().NotBeNull().And.BeEmpty();
        enemy.Level.Should().Be(1);
        enemy.Health.Should().Be(50);
        enemy.MaxHealth.Should().Be(50);
        enemy.Strength.Should().Be(10);
        enemy.Dexterity.Should().Be(10);
        enemy.Constitution.Should().Be(10);
        enemy.Intelligence.Should().Be(10);
        enemy.Wisdom.Should().Be(10);
        enemy.Charisma.Should().Be(10);
        enemy.BasePhysicalDamage.Should().Be(5);
        enemy.BaseMagicDamage.Should().Be(0);
        enemy.XP.Should().Be(25);
        enemy.GoldReward.Should().Be(10);
        enemy.Type.Should().Be(EnemyType.Common);
        enemy.Difficulty.Should().Be(EnemyDifficulty.Easy);
    }

    [Fact]
    public void Enemy_Should_Generate_Unique_Ids()
    {
        // Arrange & Act
        var enemy1 = new Enemy();
        var enemy2 = new Enemy();

        // Assert
        enemy1.Id.Should().NotBe(enemy2.Id);
    }

    [Fact]
    public void GetPhysicalDamageBonus_Should_Equal_Strength()
    {
        // Arrange
        var enemy = new Enemy { Strength = 15 };

        // Act
        var bonus = enemy.GetPhysicalDamageBonus();

        // Assert
        bonus.Should().Be(15);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(12, 12)]
    [InlineData(20, 20)]
    public void GetPhysicalDamageBonus_Should_Scale_With_Strength(int strength, int expectedBonus)
    {
        // Arrange
        var enemy = new Enemy { Strength = strength };

        // Act
        var bonus = enemy.GetPhysicalDamageBonus();

        // Assert
        bonus.Should().Be(expectedBonus);
    }

    [Fact]
    public void GetMagicDamageBonus_Should_Equal_Intelligence()
    {
        // Arrange
        var enemy = new Enemy { Intelligence = 18 };

        // Act
        var bonus = enemy.GetMagicDamageBonus();

        // Assert
        bonus.Should().Be(18);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(15, 15)]
    [InlineData(25, 25)]
    public void GetMagicDamageBonus_Should_Scale_With_Intelligence(int intelligence, int expectedBonus)
    {
        // Arrange
        var enemy = new Enemy { Intelligence = intelligence };

        // Act
        var bonus = enemy.GetMagicDamageBonus();

        // Assert
        bonus.Should().Be(expectedBonus);
    }

    [Fact]
    public void GetDodgeChance_Should_Be_Half_Of_Dexterity()
    {
        // Arrange
        var enemy = new Enemy { Dexterity = 20 };

        // Act
        var dodgeChance = enemy.GetDodgeChance();

        // Assert
        dodgeChance.Should().Be(10.0); // 20 * 0.5 = 10%
    }

    [Theory]
    [InlineData(10, 5.0)]   // 10 DEX = 5% dodge
    [InlineData(20, 10.0)]  // 20 DEX = 10% dodge
    [InlineData(30, 15.0)]  // 30 DEX = 15% dodge
    public void GetDodgeChance_Should_Calculate_Correctly(int dexterity, double expectedDodge)
    {
        // Arrange
        var enemy = new Enemy { Dexterity = dexterity };

        // Act
        var dodgeChance = enemy.GetDodgeChance();

        // Assert
        dodgeChance.Should().Be(expectedDodge);
    }

    [Fact]
    public void GetCriticalChance_Should_Be_ThirtyPercent_Of_Dexterity()
    {
        // Arrange
        var enemy = new Enemy { Dexterity = 20 };

        // Act
        var critChance = enemy.GetCriticalChance();

        // Assert
        critChance.Should().Be(6.0); // 20 * 0.3 = 6%
    }

    [Theory]
    [InlineData(10, 3.0)]   // 10 DEX = 3% crit
    [InlineData(20, 6.0)]   // 20 DEX = 6% crit
    [InlineData(30, 9.0)]   // 30 DEX = 9% crit
    public void GetCriticalChance_Should_Calculate_Correctly(int dexterity, double expectedCrit)
    {
        // Arrange
        var enemy = new Enemy { Dexterity = dexterity };

        // Act
        var critChance = enemy.GetCriticalChance();

        // Assert
        critChance.Should().Be(expectedCrit);
    }

    [Fact]
    public void GetPhysicalDefense_Should_Equal_Constitution()
    {
        // Arrange
        var enemy = new Enemy { Constitution = 14 };

        // Act
        var defense = enemy.GetPhysicalDefense();

        // Assert
        defense.Should().Be(14);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(12, 12)]
    [InlineData(18, 18)]
    public void GetPhysicalDefense_Should_Scale_With_Constitution(int constitution, int expectedDefense)
    {
        // Arrange
        var enemy = new Enemy { Constitution = constitution };

        // Act
        var defense = enemy.GetPhysicalDefense();

        // Assert
        defense.Should().Be(expectedDefense);
    }

    [Fact]
    public void GetMagicResistance_Should_Be_EightyPercent_Of_Wisdom()
    {
        // Arrange
        var enemy = new Enemy { Wisdom = 15 };

        // Act
        var resistance = enemy.GetMagicResistance();

        // Assert
        resistance.Should().Be(12.0); // 15 * 0.8 = 12%
    }

    [Theory]
    [InlineData(10, 8.0)]   // 10 WIS = 8% resist
    [InlineData(20, 16.0)]  // 20 WIS = 16% resist
    [InlineData(25, 20.0)]  // 25 WIS = 20% resist
    public void GetMagicResistance_Should_Calculate_Correctly(int wisdom, double expectedResistance)
    {
        // Arrange
        var enemy = new Enemy { Wisdom = wisdom };

        // Act
        var resistance = enemy.GetMagicResistance();

        // Assert
        resistance.Should().Be(expectedResistance);
    }

    [Fact]
    public void IsAlive_Should_Return_True_When_Health_Above_Zero()
    {
        // Arrange
        var enemy = new Enemy { Health = 30 };

        // Act
        var isAlive = enemy.IsAlive();

        // Assert
        isAlive.Should().BeTrue();
    }

    [Fact]
    public void IsAlive_Should_Return_False_When_Health_Is_Zero()
    {
        // Arrange
        var enemy = new Enemy { Health = 0 };

        // Act
        var isAlive = enemy.IsAlive();

        // Assert
        isAlive.Should().BeFalse();
    }

    [Fact]
    public void IsAlive_Should_Return_False_When_Health_Is_Negative()
    {
        // Arrange
        var enemy = new Enemy { Health = -10 };

        // Act
        var isAlive = enemy.IsAlive();

        // Assert
        isAlive.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(100, true)]
    public void IsAlive_Should_Determine_Life_Status_Correctly(int health, bool expectedAlive)
    {
        // Arrange
        var enemy = new Enemy { Health = health };

        // Act
        var isAlive = enemy.IsAlive();

        // Assert
        isAlive.Should().Be(expectedAlive);
    }

    [Theory]
    [InlineData(EnemyType.Common)]
    [InlineData(EnemyType.Beast)]
    [InlineData(EnemyType.Undead)]
    [InlineData(EnemyType.Demon)]
    [InlineData(EnemyType.Elemental)]
    [InlineData(EnemyType.Humanoid)]
    [InlineData(EnemyType.Dragon)]
    [InlineData(EnemyType.Boss)]
    public void Enemy_Should_Support_All_EnemyTypes(EnemyType type)
    {
        // Arrange & Act
        var enemy = new Enemy { Type = type };

        // Assert
        enemy.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(EnemyDifficulty.Easy)]
    [InlineData(EnemyDifficulty.Normal)]
    [InlineData(EnemyDifficulty.Hard)]
    [InlineData(EnemyDifficulty.Elite)]
    [InlineData(EnemyDifficulty.Boss)]
    public void Enemy_Should_Support_All_Difficulty_Levels(EnemyDifficulty difficulty)
    {
        // Arrange & Act
        var enemy = new Enemy { Difficulty = difficulty };

        // Assert
        enemy.Difficulty.Should().Be(difficulty);
    }

    [Fact]
    public void Enemy_Properties_Should_Be_Settable()
    {
        // Arrange
        var enemy = new Enemy();

        // Act
        enemy.Name = "Goblin Warrior";
        enemy.Description = "A fierce goblin fighter";
        enemy.Level = 5;
        enemy.Health = 80;
        enemy.MaxHealth = 100;
        enemy.Strength = 12;
        enemy.Dexterity = 14;
        enemy.Constitution = 11;
        enemy.Intelligence = 8;
        enemy.Wisdom = 9;
        enemy.Charisma = 6;
        enemy.BasePhysicalDamage = 15;
        enemy.BaseMagicDamage = 5;
        enemy.XP = 50;
        enemy.GoldReward = 25;
        enemy.Type = EnemyType.Humanoid;
        enemy.Difficulty = EnemyDifficulty.Normal;

        // Assert
        enemy.Name.Should().Be("Goblin Warrior");
        enemy.Description.Should().Be("A fierce goblin fighter");
        enemy.Level.Should().Be(5);
        enemy.Health.Should().Be(80);
        enemy.MaxHealth.Should().Be(100);
        enemy.Strength.Should().Be(12);
        enemy.Dexterity.Should().Be(14);
        enemy.Constitution.Should().Be(11);
        enemy.Intelligence.Should().Be(8);
        enemy.Wisdom.Should().Be(9);
        enemy.Charisma.Should().Be(6);
        enemy.BasePhysicalDamage.Should().Be(15);
        enemy.BaseMagicDamage.Should().Be(5);
        enemy.XP.Should().Be(50);
        enemy.GoldReward.Should().Be(25);
        enemy.Type.Should().Be(EnemyType.Humanoid);
        enemy.Difficulty.Should().Be(EnemyDifficulty.Normal);
    }

    [Fact]
    public void Enemy_Traits_Dictionary_Should_Be_Mutable()
    {
        // Arrange
        var enemy = new Enemy();
        var trait1 = new TraitValue(5, TraitType.Number);
        var trait2 = new TraitValue("Fire", TraitType.String);

        // Act
        enemy.Traits.Add("AttackSpeed", trait1);
        enemy.Traits.Add("Element", trait2);

        // Assert
        enemy.Traits.Should().HaveCount(2);
        enemy.Traits["AttackSpeed"].Should().Be(trait1);
        enemy.Traits["Element"].Should().Be(trait2);
    }

    [Fact]
    public void Enemy_Combat_Calculations_Should_Work_Together()
    {
        // Arrange - Create a balanced enemy
        var enemy = new Enemy
        {
            Strength = 15,      // +15 physical damage
            Dexterity = 20,     // 10% dodge, 6% crit
            Constitution = 14,  // 14 physical defense
            Intelligence = 12,  // +12 magic damage
            Wisdom = 10         // 8% magic resist
        };

        // Act & Assert - Verify all calculations
        enemy.GetPhysicalDamageBonus().Should().Be(15);
        enemy.GetMagicDamageBonus().Should().Be(12);
        enemy.GetDodgeChance().Should().Be(10.0);
        enemy.GetCriticalChance().Should().Be(6.0);
        enemy.GetPhysicalDefense().Should().Be(14);
        enemy.GetMagicResistance().Should().Be(8.0);
    }
}
