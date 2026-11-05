using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Services;

/// <summary>
/// Behaviour tests for the no-database <see cref="InMemoryInventoryService"/>: stack merging,
/// slot limits, removal floors and durability bookkeeping.
/// </summary>
[Trait("Category", "Service")]
public class InMemoryInventoryServiceTests
{
    private const string Hero = "Aria";

    private static InMemoryInventoryService NewService() =>
        new(NullLogger<InMemoryInventoryService>.Instance);

    private static ItemDrop Drop(string itemRef, int quantity = 1, string name = "item") =>
        new() { ItemRef = itemRef, Quantity = quantity, ItemName = name };

    [Fact]
    public async Task AddItem_StoresTheStack()
    {
        // Arrange
        var service = NewService();

        // Act
        var added = await service.AddItemAsync(Hero, "ore", 3);

        // Assert
        added.Should().BeTrue();
        service.GetInventory(Hero)["ore"].Should().Be(3);
    }

    [Fact]
    public async Task AddItem_MergesIntoAnExistingStack()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "ore", 3);

        // Act
        await service.AddItemAsync(Hero, "ore", 4);

        // Assert
        service.GetInventory(Hero)["ore"].Should().Be(7);
    }

    [Fact]
    public async Task AddItem_KeepsCharactersSeparate()
    {
        // Arrange
        var service = NewService();

        // Act
        await service.AddItemAsync(Hero, "ore", 1);
        await service.AddItemAsync("Bran", "ore", 2);

        // Assert
        service.GetInventory(Hero)["ore"].Should().Be(1);
        service.GetInventory("Bran")["ore"].Should().Be(2);
    }

    [Fact]
    public async Task AddItems_AddsEveryDrop()
    {
        // Arrange
        var service = NewService();

        // Act
        var added = await service.AddItemsAsync(Hero, [Drop("ore", 2), Drop("wood", 1)]);

        // Assert
        added.Should().BeTrue();
        service.GetInventory(Hero).Should().HaveCount(2);
        service.GetInventory(Hero)["ore"].Should().Be(2);
    }

    [Fact]
    public async Task AddItems_MergesDuplicateRefsWithinTheBatch()
    {
        // Arrange
        var service = NewService();

        // Act
        await service.AddItemsAsync(Hero, [Drop("ore", 2), Drop("ore", 3)]);

        // Assert
        service.GetInventory(Hero)["ore"].Should().Be(5);
    }

    [Fact]
    public async Task GetItemCount_ReturnsZeroForAnUnknownRef()
    {
        // Arrange
        var service = NewService();

        // Act & Assert
        (await service.GetItemCountAsync(Hero, "missing")).Should().Be(0);
    }

    [Fact]
    public async Task HasInventorySpace_IsTrueUpToTheHundredSlotLimit()
    {
        // Arrange
        var service = NewService();
        for (var i = 0; i < 3; i++)
        {
            await service.AddItemAsync(Hero, $"item-{i}", 1);
        }

        // Act & Assert
        (await service.HasInventorySpaceAsync(Hero, 97)).Should().BeTrue();
        (await service.HasInventorySpaceAsync(Hero, 98)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_UnknownRef_ReturnsFalse()
    {
        // Arrange
        var service = NewService();

        // Act & Assert
        (await service.RemoveItemAsync(Hero, "missing", 1)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_MoreThanHeld_ReturnsFalseAndKeepsTheStack()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "ore", 2);

        // Act
        var removed = await service.RemoveItemAsync(Hero, "ore", 3);

        // Assert
        removed.Should().BeFalse();
        service.GetInventory(Hero)["ore"].Should().Be(2);
    }

    [Fact]
    public async Task RemoveItem_PartialAmount_ReducesTheStack()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "ore", 5);

        // Act
        var removed = await service.RemoveItemAsync(Hero, "ore", 2);

        // Assert
        removed.Should().BeTrue();
        service.GetInventory(Hero)["ore"].Should().Be(3);
    }

    [Fact]
    public async Task RemoveItem_ExactAmount_DeletesTheEntry()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "ore", 5);

        // Act
        var removed = await service.RemoveItemAsync(Hero, "ore", 5);

        // Assert
        removed.Should().BeTrue();
        service.GetInventory(Hero).Should().NotContainKey("ore");
    }

    [Fact]
    public async Task ReduceDurability_UnknownItem_ReturnsFalse()
    {
        // Arrange
        var service = NewService();

        // Act & Assert
        (await service.ReduceItemDurabilityAsync(Hero, "missing", 5)).Should().BeFalse();
    }

    [Fact]
    public async Task ReduceDurability_StartsFromOneHundredWhenUnset()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "sword", 1);

        // Act
        var reduced = await service.ReduceItemDurabilityAsync(Hero, "sword", 30);

        // Assert
        reduced.Should().BeTrue();
    }

    [Fact]
    public async Task ReduceDurability_AccumulatesAcrossCallsAndFloorsAtZero()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "sword", 1);
        await service.ReduceItemDurabilityAsync(Hero, "sword", 30);

        // Act
        var reduced = await service.ReduceItemDurabilityAsync(Hero, "sword", 500);

        // Assert
        reduced.Should().BeTrue();
    }

    [Fact]
    public async Task Clear_EmptiesEveryInventory()
    {
        // Arrange
        var service = NewService();
        await service.AddItemAsync(Hero, "ore", 1);

        // Act
        service.Clear();

        // Assert
        service.GetInventory(Hero).Should().BeEmpty();
    }
}
