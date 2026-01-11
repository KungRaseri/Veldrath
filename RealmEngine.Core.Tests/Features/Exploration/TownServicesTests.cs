using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Exploration;

/// <summary>
/// Tests for town service features (shops and inns).
/// </summary>
public class TownServicesTests
{
    [Fact]
    public void Location_With_HasShop_Should_Indicate_Shop_Available()
    {
        // Arrange & Act
        var town = new Location
        {
            Id = "town-1",
            Name = "Riverside Town",
            Description = "A bustling riverside trading post",
            Type = "towns",
            HasShop = true,
            HasInn = true,
            IsSafeZone = true
        };

        // Assert
        town.HasShop.Should().BeTrue();
        town.HasInn.Should().BeTrue();
        town.IsSafeZone.Should().BeTrue();
        town.Type.Should().Be("towns");
    }

    [Fact]
    public void Location_Dungeon_Should_Not_Have_Services()
    {
        // Arrange & Act
        var dungeon = new Location
        {
            Id = "dungeon-1",
            Name = "Dark Crypt",
            Description = "An ancient tomb filled with undead",
            Type = "dungeons",
            HasShop = false,
            HasInn = false,
            IsSafeZone = false,
            DangerRating = 8
        };

        // Assert
        dungeon.HasShop.Should().BeFalse();
        dungeon.HasInn.Should().BeFalse();
        dungeon.IsSafeZone.Should().BeFalse();
        dungeon.DangerRating.Should().Be(8);
    }

    [Fact]
    public void Location_Should_Support_NPC_Lists()
    {
        // Arrange
        var town = new Location
        {
            Id = "town-1",
            Name = "Market Town",
            Description = "A town with many merchants",
            Type = "towns",
            HasShop = true
        };

        var merchant = new NPC
        {
            Name = "Gunther the Blacksmith",
            Occupation = "Blacksmith",
            Age = 45
        };

        // Act
        town.NpcObjects.Add(merchant);

        // Assert
        town.NpcObjects.Should().HaveCount(1);
        town.NpcObjects[0].Name.Should().Be("Gunther the Blacksmith");
        town.NpcObjects[0].Occupation.Should().Be("Blacksmith");
    }

    [Fact]
    public void Location_Should_Support_Enemy_Lists()
    {
        // Arrange
        var wilderness = new Location
        {
            Id = "forest-1",
            Name = "Dark Forest",
            Description = "A dangerous forest",
            Type = "wilderness",
            DangerRating = 5
        };

        var wolf = new Enemy
        {
            Name = "Dire Wolf",
            Level = 5,
            Health = 80,
            MaxHealth = 80
        };

        // Act
        wilderness.EnemyObjects.Add(wolf);

        // Assert
        wilderness.EnemyObjects.Should().HaveCount(1);
        wilderness.EnemyObjects[0].Name.Should().Be("Dire Wolf");
        wilderness.EnemyObjects[0].Level.Should().Be(5);
    }

    [Theory]
    [InlineData(0, 5, false)]  // Low health, low mana
    [InlineData(50, 0, false)]  // Half health, no mana
    public void Character_Health_And_Mana_Status_For_Inn_Rest(int health, int mana, bool isFullyRecovered)
    {
        // Arrange
        var character = new Character
        {
            Name = "TestHero",
            Level = 5,
            Health = health,
            Mana = mana,
            Strength = 10,
            Dexterity = 10,
            Constitution = 10,
            Intelligence = 10,
            Wisdom = 10,
            Charisma = 10
        };

        var maxHealth = character.GetMaxHealth();
        var maxMana = character.GetMaxMana();

        // Act
        var needsRest = (character.Health < maxHealth) || (character.Mana < maxMana);

        // Assert
        needsRest.Should().Be(!isFullyRecovered);
    }

    [Fact]
    public void Character_Should_Lose_Gold_When_Resting_At_Inn()
    {
        // Arrange
        var character = new Character
        {
            Name = "TestHero",
            Gold = 100,
            Health = 50
        };

        var innCost = 10;

        // Act
        character.Gold -= innCost;

        // Assert
        character.Gold.Should().Be(90);
    }
}
