using FluentAssertions;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Inventory.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for SortInventoryHandler.
/// </summary>
public class SortInventoryHandlerTests
{
    [Fact]
    public async Task Handle_Should_Sort_By_Name()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var item1 = new Item { Name = "Zebra Potion", Type = ItemType.Consumable };
        var item2 = new Item { Name = "Apple Tart", Type = ItemType.Consumable };
        var item3 = new Item { Name = "Magic Scroll", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item1, item2, item3 }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Name };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Should().Be(item2); // Apple Tart
        player.Inventory[1].Should().Be(item3); // Magic Scroll
        player.Inventory[2].Should().Be(item1); // Zebra Potion
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Type()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var weapon = new Item { Name = "Sword", Type = ItemType.Weapon };
        var consumable = new Item { Name = "Potion", Type = ItemType.Consumable };
        var helmet = new Item { Name = "Helmet", Type = ItemType.Helmet };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { weapon, consumable, helmet }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Type };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        // Items should be sorted by type (enum value), then by name within each type
        // Consumable=0, Weapon=1, Helmet=4
        player.Inventory[0].Should().Be(consumable); // Potion (Consumable=0)
        player.Inventory[1].Should().Be(weapon);     // Sword (Weapon=1)
        player.Inventory[2].Should().Be(helmet);     // Helmet (Helmet=4)
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Rarity_Descending()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var common = new Item { Name = "Common Sword", Type = ItemType.Weapon, Rarity = ItemRarity.Common };
        var legendary = new Item { Name = "Legendary Blade", Type = ItemType.Weapon, Rarity = ItemRarity.Legendary };
        var rare = new Item { Name = "Rare Axe", Type = ItemType.Weapon, Rarity = ItemRarity.Rare };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { common, legendary, rare }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Rarity };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Should().Be(legendary); // Legendary first
        player.Inventory[1].Should().Be(rare);      // Rare second
        player.Inventory[2].Should().Be(common);    // Common last
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Value_Descending()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var cheap = new Item { Name = "Bread", Type = ItemType.Consumable, Price = 5 };
        var expensive = new Item { Name = "Diamond Ring", Type = ItemType.Ring, Price = 1000 };
        var medium = new Item { Name = "Iron Sword", Type = ItemType.Weapon, Price = 100 };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { cheap, expensive, medium }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Value };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Should().Be(expensive); // 1000 gold
        player.Inventory[1].Should().Be(medium);    // 100 gold
        player.Inventory[2].Should().Be(cheap);     // 5 gold
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Name_When_Same_Type()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var sword1 = new Item { Name = "Zebra Sword", Type = ItemType.Weapon };
        var sword2 = new Item { Name = "Alpha Sword", Type = ItemType.Weapon };
        var sword3 = new Item { Name = "Beta Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { sword1, sword2, sword3 }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Type };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Name.Should().Be("Alpha Sword");
        player.Inventory[1].Name.Should().Be("Beta Sword");
        player.Inventory[2].Name.Should().Be("Zebra Sword");
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Name_When_Same_Rarity()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var item1 = new Item { Name = "Zebra Item", Type = ItemType.Weapon, Rarity = ItemRarity.Epic };
        var item2 = new Item { Name = "Alpha Item", Type = ItemType.Weapon, Rarity = ItemRarity.Epic };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item1, item2 }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Rarity };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Name.Should().Be("Alpha Item");
        player.Inventory[1].Name.Should().Be("Zebra Item");
    }

    [Fact]
    public async Task Handle_Should_Sort_By_Name_When_Same_Value()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var item1 = new Item { Name = "Zebra Item", Type = ItemType.Weapon, Price = 100 };
        var item2 = new Item { Name = "Alpha Item", Type = ItemType.Weapon, Price = 100 };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item1, item2 }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Value };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Name.Should().Be("Alpha Item");
        player.Inventory[1].Name.Should().Be("Zebra Item");
    }

    [Fact]
    public async Task Handle_Should_Work_With_Empty_Inventory()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item>()
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Name };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Work_With_Single_Item()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var item = new Item { Name = "Lonely Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item }
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Name };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().ContainSingle();
        player.Inventory[0].Should().Be(item);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item>()
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Rarity };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Be("Inventory sorted by Rarity");
    }

    [Fact]
    public async Task Handle_Should_Default_To_Name_Sort_When_Invalid_Criteria()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var item1 = new Item { Name = "Zebra", Type = ItemType.Consumable };
        var item2 = new Item { Name = "Apple", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { item1, item2 }
        };
        // Use invalid sort criteria (cast from invalid int)
        var command = new SortInventoryCommand { Player = player, SortBy = (SortCriteria)999 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory[0].Name.Should().Be("Apple");
        player.Inventory[1].Name.Should().Be("Zebra");
    }

    [Fact]
    public async Task Handle_Should_Preserve_Item_Count()
    {
        // Arrange
        var handler = new SortInventoryHandler(NullLogger<SortInventoryHandler>.Instance);
        var items = new List<Item>
        {
            new Item { Name = "Item1", Type = ItemType.Consumable },
            new Item { Name = "Item2", Type = ItemType.Weapon },
            new Item { Name = "Item3", Type = ItemType.Helmet },
            new Item { Name = "Item4", Type = ItemType.Ring },
            new Item { Name = "Item5", Type = ItemType.Boots }
        };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item>(items)
        };
        var command = new SortInventoryCommand { Player = player, SortBy = SortCriteria.Type };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().HaveCount(5);
        player.Inventory.Should().Contain(items);
    }
}
