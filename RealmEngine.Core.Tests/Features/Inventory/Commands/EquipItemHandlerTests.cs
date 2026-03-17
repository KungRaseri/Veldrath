using FluentAssertions;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Inventory.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for EquipItemHandler.
/// </summary>
public class EquipItemHandlerTests
{
    [Fact]
    public async Task Handle_Should_Equip_Weapon_To_MainHand()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var weapon = new Item { Name = "Iron Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { weapon }
        };
        var command = new EquipItemCommand { Player = player, Item = weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedMainHand.Should().Be(weapon);
        player.Inventory.Should().NotContain(weapon);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Equipping_Consumable()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { potion }
        };
        var command = new EquipItemCommand { Player = player, Item = potion };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not equipable");
        player.EquippedMainHand.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Equipping_QuestItem()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var questItem = new Item { Name = "Ancient Key", Type = ItemType.QuestItem };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { questItem }
        };
        var command = new EquipItemCommand { Player = player, Item = questItem };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.UnequippedItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Unequip_Previous_Weapon()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var oldWeapon = new Item { Name = "Rusty Sword", Type = ItemType.Weapon };
        var newWeapon = new Item { Name = "Steel Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            EquippedMainHand = oldWeapon,
            Inventory = new List<Item> { newWeapon }
        };
        var command = new EquipItemCommand { Player = player, Item = newWeapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.UnequippedItem.Should().Be(oldWeapon);
        player.EquippedMainHand.Should().Be(newWeapon);
        player.Inventory.Should().Contain(oldWeapon);
        player.Inventory.Should().NotContain(newWeapon);
    }

    [Fact]
    public async Task Handle_Should_Equip_Shield_To_OffHand()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var shield = new Item { Name = "Iron Shield", Type = ItemType.Shield };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { shield }
        };
        var command = new EquipItemCommand { Player = player, Item = shield };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedOffHand.Should().Be(shield);
    }

    [Fact]
    public async Task Handle_Should_Equip_Helmet()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var helmet = new Item { Name = "Iron Helmet", Type = ItemType.Helmet };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { helmet }
        };
        var command = new EquipItemCommand { Player = player, Item = helmet };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedHelmet.Should().Be(helmet);
    }

    [Fact]
    public async Task Handle_Should_Equip_Chest()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var chest = new Item { Name = "Iron Chest", Type = ItemType.Chest };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { chest }
        };
        var command = new EquipItemCommand { Player = player, Item = chest };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedChest.Should().Be(chest);
    }

    [Fact]
    public async Task Handle_Should_Equip_Legs()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var legs = new Item { Name = "Iron Legs", Type = ItemType.Legs };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { legs }
        };
        var command = new EquipItemCommand { Player = player, Item = legs };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedLegs.Should().Be(legs);
    }

    [Fact]
    public async Task Handle_Should_Equip_Boots()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var boots = new Item { Name = "Iron Boots", Type = ItemType.Boots };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { boots }
        };
        var command = new EquipItemCommand { Player = player, Item = boots };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedBoots.Should().Be(boots);
    }

    [Fact]
    public async Task Handle_Should_Equip_Necklace()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var necklace = new Item { Name = "Gold Necklace", Type = ItemType.Necklace };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { necklace }
        };
        var command = new EquipItemCommand { Player = player, Item = necklace };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedNecklace.Should().Be(necklace);
    }

    [Fact]
    public async Task Handle_Should_Equip_First_Ring_To_Ring1()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var ring = new Item { Name = "Gold Ring", Type = ItemType.Ring };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { ring }
        };
        var command = new EquipItemCommand { Player = player, Item = ring };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedRing1.Should().Be(ring);
        player.EquippedRing2.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Equip_Second_Ring_To_Ring2()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var ring1 = new Item { Name = "Silver Ring", Type = ItemType.Ring };
        var ring2 = new Item { Name = "Gold Ring", Type = ItemType.Ring };
        var player = new Character
        {
            Name = "Hero",
            EquippedRing1 = ring1,
            Inventory = new List<Item> { ring2 }
        };
        var command = new EquipItemCommand { Player = player, Item = ring2 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedRing1.Should().Be(ring1);
        player.EquippedRing2.Should().Be(ring2);
        result.UnequippedItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Unequip_Ring1_When_Both_Rings_Equipped()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var ring1 = new Item { Name = "Silver Ring", Type = ItemType.Ring };
        var ring2 = new Item { Name = "Gold Ring", Type = ItemType.Ring };
        var ring3 = new Item { Name = "Diamond Ring", Type = ItemType.Ring };
        var player = new Character
        {
            Name = "Hero",
            EquippedRing1 = ring1,
            EquippedRing2 = ring2,
            Inventory = new List<Item> { ring3 }
        };
        var command = new EquipItemCommand { Player = player, Item = ring3 };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.UnequippedItem.Should().Be(ring1);
        player.EquippedRing1.Should().Be(ring3);
        player.EquippedRing2.Should().Be(ring2);
        player.Inventory.Should().Contain(ring1);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message_With_Item_Name()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var weapon = new Item { Name = "Legendary Blade", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { weapon }
        };
        var command = new EquipItemCommand { Player = player, Item = weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Be("Equipped Legendary Blade");
    }

    [Fact]
    public async Task Handle_Should_Equip_OffHand_Item()
    {
        // Arrange
        var handler = new EquipItemHandler(NullLogger<EquipItemHandler>.Instance);
        var offHand = new Item { Name = "Tome", Type = ItemType.OffHand };
        var player = new Character
        {
            Name = "Hero",
            Inventory = new List<Item> { offHand }
        };
        var command = new EquipItemCommand { Player = player, Item = offHand };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedOffHand.Should().Be(offHand);
    }
}
