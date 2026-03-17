using FluentAssertions;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Inventory.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for DropItemHandler.
/// </summary>
public class DropItemHandlerTests
{
    [Fact]
    public async Task Handle_Should_Remove_Item_From_Inventory()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var item = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item }
        };
        var command = new DropItemCommand { Player = player, Item = item };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().NotContain(item);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var item = new Item { Name = "Iron Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item }
        };
        var command = new DropItemCommand { Player = player, Item = item };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Be("Dropped Iron Sword");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Item_Not_In_Inventory()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var item = new Item { Name = "Missing Item", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item>()
        };
        var command = new DropItemCommand { Player = player, Item = item };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Item not found in inventory");
    }

    [Fact]
    public async Task Handle_Should_Only_Remove_Specified_Item()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var item1 = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        var item2 = new Item { Name = "Mana Potion", Type = ItemType.Consumable };
        var item3 = new Item { Name = "Iron Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item1, item2, item3 }
        };
        var command = new DropItemCommand { Player = player, Item = item2 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().HaveCount(2);
        player.Inventory.Should().Contain(item1);
        player.Inventory.Should().NotContain(item2);
        player.Inventory.Should().Contain(item3);
    }

    [Fact]
    public async Task Handle_Should_Work_With_Empty_Inventory()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var item = new Item { Name = "Test Item", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item>()
        };
        var command = new DropItemCommand { Player = player, Item = item };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Work_With_Quest_Items()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var questItem = new Item { Name = "Ancient Key", Type = ItemType.QuestItem };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { questItem }
        };
        var command = new DropItemCommand { Player = player, Item = questItem };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().NotContain(questItem);
    }

    [Fact]
    public async Task Handle_Should_Work_With_Equipment()
    {
        // Arrange
        var handler = new DropItemHandler(NullLogger<DropItemHandler>.Instance);
        var weapon = new Item { Name = "Legendary Blade", Type = ItemType.Weapon, Rarity = ItemRarity.Legendary };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { weapon }
        };
        var command = new DropItemCommand { Player = player, Item = weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Legendary Blade");
    }
}
