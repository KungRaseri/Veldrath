using FluentAssertions;
using RealmEngine.Core.Features.Death;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Death;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for DeathService (currently 0% coverage → target 90%+).
/// </summary>
public class DeathServiceTests
{
    private readonly DeathService _deathService;

    public DeathServiceTests()
    {
        _deathService = new DeathService(NullLogger<DeathService>.Instance);
    }

    [Fact]
    public void HandleItemDropping_Should_Return_Empty_List_When_No_Items_To_Drop()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Sword" },
                new Item { Name = "Potion" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            ItemsDroppedOnDeath = 0,
            DropAllInventoryOnDeath = false
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Forest", difficulty);

        // Assert
        droppedItems.Should().BeEmpty();
        player.Inventory.Should().HaveCount(2, "No items should be removed");
    }

    [Fact]
    public void HandleItemDropping_Should_Drop_All_Items_When_DropAllInventoryOnDeath_Is_True()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Sword" },
                new Item { Name = "Shield" },
                new Item { Name = "Potion" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            DropAllInventoryOnDeath = true
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Dark Forest", difficulty);

        // Assert
        droppedItems.Should().HaveCount(3);
        droppedItems.Should().Contain(i => i.Name == "Sword");
        droppedItems.Should().Contain(i => i.Name == "Shield");
        droppedItems.Should().Contain(i => i.Name == "Potion");
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public void HandleItemDropping_Should_Drop_Specified_Number_Of_Random_Items()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Item1" },
                new Item { Name = "Item2" },
                new Item { Name = "Item3" },
                new Item { Name = "Item4" },
                new Item { Name = "Item5" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            ItemsDroppedOnDeath = 3,
            DropAllInventoryOnDeath = false
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Mountain", difficulty);

        // Assert
        droppedItems.Should().HaveCount(3, "Should drop exactly 3 items");
        player.Inventory.Should().HaveCount(2, "Should have 2 items remaining");
    }

    [Fact]
    public void HandleItemDropping_Should_Not_Drop_More_Items_Than_In_Inventory()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Sword" },
                new Item { Name = "Potion" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            ItemsDroppedOnDeath = 10, // More than inventory size
            DropAllInventoryOnDeath = false
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Cave", difficulty);

        // Assert
        droppedItems.Should().HaveCount(2, "Should only drop available items");
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public void HandleItemDropping_Should_Store_Dropped_Items_At_Location()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Gold Coins" },
                new Item { Name = "Magic Ring" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            DropAllInventoryOnDeath = true
        };

        // Act
        _deathService.HandleItemDropping(player, saveGame, "Ancient Ruins", difficulty);

        // Assert
        saveGame.DroppedItemsAtLocations.Should().ContainKey("Ancient Ruins");
        saveGame.DroppedItemsAtLocations["Ancient Ruins"].Should().HaveCount(2);
        saveGame.DroppedItemsAtLocations["Ancient Ruins"].Should().Contain(i => i.Name == "Gold Coins");
        saveGame.DroppedItemsAtLocations["Ancient Ruins"].Should().Contain(i => i.Name == "Magic Ring");
    }

    [Fact]
    public void HandleItemDropping_Should_Append_Items_To_Existing_Location()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "New Sword" }
            }
        };

        var saveGame = new SaveGame();
        saveGame.DroppedItemsAtLocations["Dragon's Lair"] = new List<Item>
        {
            new Item { Name = "Old Shield" }
        };

        var difficulty = new DifficultySettings
        {
            ItemsDroppedOnDeath = 1
        };

        // Act
        _deathService.HandleItemDropping(player, saveGame, "Dragon's Lair", difficulty);

        // Assert
        saveGame.DroppedItemsAtLocations["Dragon's Lair"].Should().HaveCount(2);
        saveGame.DroppedItemsAtLocations["Dragon's Lair"].Should().Contain(i => i.Name == "Old Shield");
        saveGame.DroppedItemsAtLocations["Dragon's Lair"].Should().Contain(i => i.Name == "New Sword");
    }

    [Fact]
    public void HandleItemDropping_Should_Handle_Empty_Inventory()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>()
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            ItemsDroppedOnDeath = 5
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Nowhere", difficulty);

        // Assert
        droppedItems.Should().BeEmpty();
        player.Inventory.Should().BeEmpty();
        saveGame.DroppedItemsAtLocations.Should().NotContainKey("Nowhere");
    }

    [Fact]
    public void RetrieveDroppedItems_Should_Return_Items_From_Location()
    {
        // Arrange
        var saveGame = new SaveGame();
        saveGame.DroppedItemsAtLocations["Beach"] = new List<Item>
        {
            new Item { Name = "Seashell" },
            new Item { Name = "Treasure Chest" }
        };

        // Act
        var retrievedItems = _deathService.RetrieveDroppedItems(saveGame, "Beach");

        // Assert
        retrievedItems.Should().HaveCount(2);
        retrievedItems.Should().Contain(i => i.Name == "Seashell");
        retrievedItems.Should().Contain(i => i.Name == "Treasure Chest");
    }

    [Fact]
    public void RetrieveDroppedItems_Should_Remove_Items_From_SaveGame_After_Retrieval()
    {
        // Arrange
        var saveGame = new SaveGame();
        saveGame.DroppedItemsAtLocations["Castle"] = new List<Item>
        {
            new Item { Name = "Crown" }
        };

        // Act
        _deathService.RetrieveDroppedItems(saveGame, "Castle");

        // Assert
        saveGame.DroppedItemsAtLocations.Should().NotContainKey("Castle", "Items should be removed after retrieval");
    }

    [Fact]
    public void RetrieveDroppedItems_Should_Return_Empty_List_When_No_Items_At_Location()
    {
        // Arrange
        var saveGame = new SaveGame();

        // Act
        var retrievedItems = _deathService.RetrieveDroppedItems(saveGame, "Empty Field");

        // Assert
        retrievedItems.Should().BeEmpty();
    }

    [Fact]
    public void RetrieveDroppedItems_Should_Return_Empty_List_When_Location_Doesnt_Exist()
    {
        // Arrange
        var saveGame = new SaveGame();
        saveGame.DroppedItemsAtLocations["Town"] = new List<Item>
        {
            new Item { Name = "Apple" }
        };

        // Act
        var retrievedItems = _deathService.RetrieveDroppedItems(saveGame, "Desert");

        // Assert
        retrievedItems.Should().BeEmpty();
        saveGame.DroppedItemsAtLocations.Should().ContainKey("Town", "Other locations should not be affected");
    }

    [Fact]
    public void HandleItemDropping_Should_Return_Dropped_Items_List()
    {
        // Arrange
        var player = new Character
        {
            Name = "TestHero",
            Inventory = new List<Item>
            {
                new Item { Name = "Axe" },
                new Item { Name = "Helmet" }
            }
        };

        var saveGame = new SaveGame();
        var difficulty = new DifficultySettings
        {
            DropAllInventoryOnDeath = true
        };

        // Act
        var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Graveyard", difficulty);

        // Assert
        droppedItems.Should().NotBeNull();
        droppedItems.Should().BeOfType<List<Item>>();
        droppedItems.Should().HaveCount(2);
    }

    [Fact]
    public void HandleItemDropping_Should_Drop_Items_Randomly_Over_Multiple_Calls()
    {
        // This test verifies randomness by running multiple times
        // and ensuring different items are dropped
        var dropPatterns = new HashSet<string>();

        for (int run = 0; run < 10; run++)
        {
            // Arrange
            var player = new Character
            {
                Name = "TestHero",
                Inventory = new List<Item>
                {
                    new Item { Name = "A" },
                    new Item { Name = "B" },
                    new Item { Name = "C" },
                    new Item { Name = "D" },
                    new Item { Name = "E" }
                }
            };

            var saveGame = new SaveGame();
            var difficulty = new DifficultySettings
            {
                ItemsDroppedOnDeath = 2
            };

            // Act
            var droppedItems = _deathService.HandleItemDropping(player, saveGame, "Test", difficulty);

            // Record the pattern
            var pattern = string.Join(",", droppedItems.Select(i => i.Name).OrderBy(n => n));
            dropPatterns.Add(pattern);
        }

        // Assert
        // With 5 items dropping 2 at a time, there are C(5,2) = 10 possible combinations
        // We should see some variation (at least 2 different patterns out of 10 runs)
        dropPatterns.Should().HaveCountGreaterThan(1, "Should produce different random patterns");
    }
}
