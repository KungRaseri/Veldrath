using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class LootTableServiceTests
{
    private readonly Mock<ILogger<LootTableService>> _mockLogger;
    private readonly LootTableService _service;

    public LootTableServiceTests()
    {
        _mockLogger = new Mock<ILogger<LootTableService>>();
        _service = new LootTableService(_mockLogger.Object);
    }

    [Fact]
    public void Should_Build_Loot_Table_With_Correct_Weights()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Test Location",
            Description = "Test",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/weapons/swords:Iron Sword",
                "@items/armor/helmets:*",
                "@items/consumables/potions:Health Potion"
            }
        };

        // Act
        var lootWeights = _service.GetLootSpawnWeights(location);

        // Assert
        lootWeights.Should().HaveCount(3);
        lootWeights["weapons/swords"].Should().Be(20); // 2 sword refs = 20 weight
        lootWeights["armor/helmets"].Should().Be(10); // 1 helmet ref = 10 weight
        lootWeights["consumables/potions"].Should().Be(10); // 1 potion ref = 10 weight
    }

    [Fact]
    public void Should_Return_Empty_Loot_Table_For_Null_Location()
    {
        // Act
        var lootWeights = _service.GetLootSpawnWeights(null!);

        // Assert
        lootWeights.Should().BeEmpty();
    }

    [Fact]
    public void Should_Return_Empty_Loot_Table_For_Location_Without_Loot()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Safe Town",
            Description = "Safe",
            Type = "town",
            Loot = new List<string>()
        };

        // Act
        var lootWeights = _service.GetLootSpawnWeights(location);

        // Assert
        lootWeights.Should().BeEmpty();
    }

    [Fact]
    public void Should_Extract_Loot_Categories_Correctly()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Treasure Cave",
            Description = "Full of loot",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/weapons/axes:*",
                "@items/armor/helmets:*",
                "@items/consumables/potions:*"
            }
        };

        // Act
        var categories = _service.GetLootCategories(location);

        // Assert
        categories.Should().HaveCount(4);
        categories.Should().Contain("weapons/swords");
        categories.Should().Contain("weapons/axes");
        categories.Should().Contain("armor/helmets");
        categories.Should().Contain("consumables/potions");
    }

    [Fact]
    public void Should_Calculate_Total_Loot_Weight_Correctly()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Loot Room",
            Description = "Lots of loot",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons:*",      // 10 weight
                "@items/armor:*",        // 10 weight
                "@items/consumables:*"   // 10 weight
            }
        };

        // Act
        var totalWeight = _service.GetTotalLootWeight(location);

        // Assert
        totalWeight.Should().Be(30);
    }

    [Fact]
    public void Should_Generate_Loot_For_Location()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Dungeon",
            Description = "Dangerous",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons:*",
                "@items/armor:*"
            }
        };

        // Act
        var generatedLoot = _service.GenerateLootForLocation(location, count: 5);

        // Assert
        generatedLoot.Should().HaveCount(5);
        generatedLoot.Should().OnlyContain(loot => 
            loot.StartsWith("@items/"));
    }

    [Fact]
    public void Should_Return_Empty_List_When_Generating_Loot_For_Null_Location()
    {
        // Act
        var generatedLoot = _service.GenerateLootForLocation(null!, count: 5);

        // Assert
        generatedLoot.Should().BeEmpty();
    }

    [Fact]
    public void Should_Return_Empty_List_When_Location_Has_No_Loot()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Empty Cave",
            Description = "Nothing here",
            Type = "dungeon",
            Loot = new List<string>()
        };

        // Act
        var generatedLoot = _service.GenerateLootForLocation(location, count: 5);

        // Assert
        generatedLoot.Should().BeEmpty();
    }

    [Fact]
    public void Should_Generate_Loot_With_Weighted_Distribution()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Weapon Cache",
            Description = "Mostly weapons",
            Type = "dungeon",
            Loot = new List<string>
            {
                // Weapons: 60 weight (6 references)
                "@items/weapons/swords:*",
                "@items/weapons/swords:Iron Sword",
                "@items/weapons/swords:Steel Sword",
                "@items/weapons/axes:*",
                "@items/weapons/axes:Battle Axe",
                "@items/weapons/bows:*",
                // Armor: 10 weight (1 reference)
                "@items/armor/helmets:*"
            }
        };

        // Act - Generate a large sample to test distribution
        var generatedLoot = _service.GenerateLootForLocation(location, count: 100);

        // Assert
        var weaponCount = generatedLoot.Count(l => l.Contains("weapons"));
        var armorCount = generatedLoot.Count(l => l.Contains("armor"));

        // With 60 weight for weapons and 10 for armor (6:1 ratio),
        // we expect roughly 6x more weapons than armor
        weaponCount.Should().BeGreaterThan(armorCount * 3); // At least 3:1 ratio
    }

    [Fact]
    public void Should_Generate_Loot_With_Category_Preference()
    {
        // Arrange - Use fixed seed for deterministic results
        var location = new Location
        {
            Id = "test",
            Name = "Mixed Cache",
            Description = "Various items",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/armor/helmets:*",
                "@items/consumables/potions:*"
            }
        };

        // Act - Generate larger sample with fixed seed for stability
        // With 50% boost to weapons category
        var generatedLoot = _service.GenerateLootWithPreference(
            location, 
            "weapons/swords", 
            count: 100,
            seed: 42);

        // Assert
        var weaponCount = generatedLoot.Count(l => l.Contains("weapons"));
        var totalCount = generatedLoot.Count;

        // With 50% boost to weapons, expect at least 38% (vs 33% baseline)
        // Larger sample size (100) provides more stable distribution
        var expectedMinimum = (int)(totalCount * 0.38); // 38 weapons minimum
        weaponCount.Should().BeGreaterThanOrEqualTo(expectedMinimum,
            $"with 50% preference boost and seed=42, weapons should be ~40-50% of loot");
    }

    [Fact]
    public void Should_Handle_Complex_Category_Paths()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Complex Cave",
            Description = "Deep item structure",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons/swords/longswords:*",
                "@items/weapons/swords/shortswords:*",
                "@items/armor/heavy/plate:*"
            }
        };

        // Act
        var categories = _service.GetLootCategories(location);

        // Assert
        categories.Should().HaveCount(3);
        categories.Should().Contain("weapons/swords/longswords");
        categories.Should().Contain("weapons/swords/shortswords");
        categories.Should().Contain("armor/heavy/plate");
    }

    [Fact]
    public void Should_Generate_Wildcard_References()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Random Loot",
            Description = "Random items",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons:*"
            }
        };

        // Act
        var generatedLoot = _service.GenerateLootForLocation(location, count: 1);

        // Assert
        generatedLoot.Should().HaveCount(1);
        generatedLoot[0].Should().Contain(":*"); // Should be a wildcard reference
    }

    [Fact]
    public void Should_Return_Empty_Categories_For_Location_With_No_Item_References()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Strange Place",
            Description = "Non-item references",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@enemies/beasts:*",  // Wrong reference type
                "some_invalid_ref"
            }
        };

        // Act
        var categories = _service.GetLootCategories(location);

        // Assert
        categories.Should().BeEmpty();
    }

    [Fact]
    public void Should_Ignore_Non_Item_References_In_Weight_Calculation()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Mixed Refs",
            Description = "Mixed reference types",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons:*",     // Valid: 10 weight
                "@enemies/beasts:*",    // Invalid: ignored
                "plain_text",           // Invalid: ignored
                "@items/armor:*"        // Valid: 10 weight
            }
        };

        // Act
        var totalWeight = _service.GetTotalLootWeight(location);

        // Assert
        totalWeight.Should().Be(20); // Only the 2 valid @items references
    }

    [Fact]
    public void Should_Handle_Empty_Loot_Generation_Gracefully()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Safe Zone",
            Description = "No loot",
            Type = "town",
            Loot = new List<string>()
        };

        // Act
        var generatedLoot = _service.GenerateLootForLocation(location, count: 10);

        // Assert
        generatedLoot.Should().BeEmpty();
    }

    [Fact]
    public void Should_Generate_Multiple_Loot_Items_Independently()
    {
        // Arrange
        var location = new Location
        {
            Id = "test",
            Name = "Rich Cache",
            Description = "Multiple items",
            Type = "dungeon",
            Loot = new List<string>
            {
                "@items/weapons:*",
                "@items/armor:*",
                "@items/consumables:*"
            }
        };

        // Act
        var generatedLoot = _service.GenerateLootForLocation(location, count: 20);

        // Assert
        generatedLoot.Should().HaveCount(20);
        // Each item should be independently generated (might have duplicates)
        generatedLoot.Should().OnlyContain(loot => loot.StartsWith("@items/"));
    }

    [Fact]
    public void Should_Return_Zero_Weight_For_Null_Location()
    {
        // Act
        var totalWeight = _service.GetTotalLootWeight(null!);

        // Assert
        totalWeight.Should().Be(0);
    }
}
