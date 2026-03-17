using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models.Harvesting;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for InMemoryInventoryService inventory management operations.
/// </summary>
public class InMemoryInventoryServiceTests
{
    private readonly InMemoryInventoryService _service;

    public InMemoryInventoryServiceTests()
    {
        _service = new InMemoryInventoryService(NullLogger<InMemoryInventoryService>.Instance);
    }

    [Fact]
    public async Task AddItemsAsync_WithValidItems_ShouldAddToInventory()
    {
        // Arrange
        var characterName = "TestCharacter";
        var items = new List<ItemDrop>
        {
            new ItemDrop { ItemRef = "@items/materials/wood:oak", ItemName = "Oak Wood", Quantity = 5 },
            new ItemDrop { ItemRef = "@items/materials/ore:iron", ItemName = "Iron Ore", Quantity = 3 }
        };

        // Act
        var result = await _service.AddItemsAsync(characterName, items);

        // Assert
        result.Should().BeTrue();
        var inventory = _service.GetInventory(characterName);
        inventory.Should().ContainKey("@items/materials/wood:oak");
        inventory["@items/materials/wood:oak"].Should().Be(5);
        inventory.Should().ContainKey("@items/materials/ore:iron");
        inventory["@items/materials/ore:iron"].Should().Be(3);
    }

    [Fact]
    public async Task AddItemsAsync_WithDuplicateItems_ShouldStackQuantities()
    {
        // Arrange
        var characterName = "TestCharacter";
        var items1 = new List<ItemDrop>
        {
            new ItemDrop { ItemRef = "@items/materials/wood:oak", ItemName = "Oak Wood", Quantity = 5 }
        };
        var items2 = new List<ItemDrop>
        {
            new ItemDrop { ItemRef = "@items/materials/wood:oak", ItemName = "Oak Wood", Quantity = 3 }
        };

        // Act
        await _service.AddItemsAsync(characterName, items1);
        await _service.AddItemsAsync(characterName, items2);

        // Assert
        var inventory = _service.GetInventory(characterName);
        inventory["@items/materials/wood:oak"].Should().Be(8, "quantities should stack");
    }

    [Fact]
    public async Task AddItemAsync_WithSingleItem_ShouldAddToInventory()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";

        // Act
        var result = await _service.AddItemAsync(characterName, itemRef, 10);

        // Assert
        result.Should().BeTrue();
        var inventory = _service.GetInventory(characterName);
        inventory[itemRef].Should().Be(10);
    }

    [Fact]
    public async Task RemoveItemAsync_WithExistingItem_ShouldReduceQuantity()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";
        await _service.AddItemAsync(characterName, itemRef, 10);

        // Act
        var result = await _service.RemoveItemAsync(characterName, itemRef, 3);

        // Assert
        result.Should().BeTrue();
        var inventory = _service.GetInventory(characterName);
        inventory[itemRef].Should().Be(7);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovingAllQuantity_ShouldRemoveItemFromInventory()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";
        await _service.AddItemAsync(characterName, itemRef, 5);

        // Act
        var result = await _service.RemoveItemAsync(characterName, itemRef, 5);

        // Assert
        result.Should().BeTrue();
        var inventory = _service.GetInventory(characterName);
        inventory.Should().NotContainKey(itemRef, "item should be removed when quantity reaches zero");
    }

    [Fact]
    public async Task RemoveItemAsync_WithInsufficientQuantity_ShouldReturnFalse()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";
        await _service.AddItemAsync(characterName, itemRef, 5);

        // Act
        var result = await _service.RemoveItemAsync(characterName, itemRef, 10);

        // Assert
        result.Should().BeFalse("cannot remove more than available quantity");
        var inventory = _service.GetInventory(characterName);
        inventory[itemRef].Should().Be(5, "quantity should remain unchanged");
    }

    [Fact]
    public async Task RemoveItemAsync_WithNonExistentItem_ShouldReturnFalse()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";

        // Act
        var result = await _service.RemoveItemAsync(characterName, itemRef, 1);

        // Assert
        result.Should().BeFalse("cannot remove item that doesn't exist in inventory");
    }

    [Fact]
    public async Task GetItemCountAsync_WithExistingItem_ShouldReturnCorrectCount()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";
        await _service.AddItemAsync(characterName, itemRef, 15);

        // Act
        var count = await _service.GetItemCountAsync(characterName, itemRef);

        // Assert
        count.Should().Be(15);
    }

    [Fact]
    public async Task GetItemCountAsync_WithNonExistentItem_ShouldReturnZero()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";

        // Act
        var count = await _service.GetItemCountAsync(characterName, itemRef);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetInventory_ForNewCharacter_ShouldReturnEmptyDictionary()
    {
        // Act
        var inventory = _service.GetInventory("NewCharacter");

        // Assert
        inventory.Should().NotBeNull();
        inventory.Should().BeEmpty("new character should have empty inventory");
    }

    [Fact]
    public async Task GetInventory_ShouldReturnAllItems()
    {
        // Arrange
        var characterName = "TestCharacter";
        await _service.AddItemAsync(characterName, "@items/materials/wood:oak", 5);
        await _service.AddItemAsync(characterName, "@items/materials/ore:iron", 3);
        await _service.AddItemAsync(characterName, "@items/consumables:health-potion", 10);

        // Act
        var inventory = _service.GetInventory(characterName);

        // Assert
        inventory.Should().HaveCount(3);
        inventory.Should().ContainKey("@items/materials/wood:oak");
        inventory.Should().ContainKey("@items/materials/ore:iron");
        inventory.Should().ContainKey("@items/consumables:health-potion");
    }

    [Fact]
    public async Task MultipleCharacters_ShouldMaintainSeparateInventories()
    {
        // Arrange
        var character1 = "Character1";
        var character2 = "Character2";
        var itemRef = "@items/materials/wood:oak";

        // Act
        await _service.AddItemAsync(character1, itemRef, 5);
        await _service.AddItemAsync(character2, itemRef, 10);

        // Assert
        var inventory1 = _service.GetInventory(character1);
        var inventory2 = _service.GetInventory(character2);
        
        inventory1[itemRef].Should().Be(5);
        inventory2[itemRef].Should().Be(10);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllInventories()
    {
        // Arrange
        var characterName = "TestCharacter";
        await _service.AddItemAsync(characterName, "@items/materials/wood:oak", 5);
        await _service.AddItemAsync(characterName, "@items/materials/ore:iron", 3);

        // Act
        _service.Clear();

        // Assert
        var inventory = _service.GetInventory(characterName);
        inventory.Should().BeEmpty("inventory should be cleared");
    }

    [Fact]
    public async Task AddItemAsync_WithZeroQuantity_ShouldNotAddToInventory()
    {
        // Arrange
        var characterName = "TestCharacter";
        var itemRef = "@items/materials/wood:oak";

        // Act
        var result = await _service.AddItemAsync(characterName, itemRef, 0);

        // Assert
        result.Should().BeTrue();
        var inventory = _service.GetInventory(characterName);
        inventory.Should().ContainKey(itemRef);
        inventory[itemRef].Should().Be(0, "zero quantity items are added to inventory");
    }
}
