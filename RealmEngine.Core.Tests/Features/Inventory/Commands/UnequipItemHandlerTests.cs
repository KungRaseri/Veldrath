using FluentAssertions;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Inventory.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UnequipItemHandler.
/// </summary>
public class UnequipItemHandlerTests
{
    [Fact]
    public async Task Handle_Should_Unequip_Weapon()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var weapon = new Item { Name = "Iron Sword", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            EquippedMainHand = weapon
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedMainHand.Should().BeNull();
        player.Inventory.Should().Contain(weapon);
        result.UnequippedItem.Should().Be(weapon);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Shield()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var shield = new Item { Name = "Iron Shield", Type = ItemType.Shield };
        var player = new Character
        {
            Name = "Hero",
            EquippedOffHand = shield
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Shield };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedOffHand.Should().BeNull();
        player.Inventory.Should().Contain(shield);
    }

    [Fact]
    public async Task Handle_Should_Unequip_OffHand()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var offHand = new Item { Name = "Tome", Type = ItemType.OffHand };
        var player = new Character
        {
            Name = "Hero",
            EquippedOffHand = offHand
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.OffHand };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedOffHand.Should().BeNull();
        player.Inventory.Should().Contain(offHand);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Helmet()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var helmet = new Item { Name = "Iron Helmet", Type = ItemType.Helmet };
        var player = new Character
        {
            Name = "Hero",
            EquippedHelmet = helmet
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Helmet };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedHelmet.Should().BeNull();
        player.Inventory.Should().Contain(helmet);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Chest()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var chest = new Item { Name = "Iron Chest", Type = ItemType.Chest };
        var player = new Character
        {
            Name = "Hero",
            EquippedChest = chest
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Chest };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedChest.Should().BeNull();
        player.Inventory.Should().Contain(chest);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Legs()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var legs = new Item { Name = "Iron Legs", Type = ItemType.Legs };
        var player = new Character
        {
            Name = "Hero",
            EquippedLegs = legs
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Legs };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedLegs.Should().BeNull();
        player.Inventory.Should().Contain(legs);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Boots()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var boots = new Item { Name = "Iron Boots", Type = ItemType.Boots };
        var player = new Character
        {
            Name = "Hero",
            EquippedBoots = boots
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Boots };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedBoots.Should().BeNull();
        player.Inventory.Should().Contain(boots);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Necklace()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var necklace = new Item { Name = "Gold Necklace", Type = ItemType.Necklace };
        var player = new Character
        {
            Name = "Hero",
            EquippedNecklace = necklace
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Necklace };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.EquippedNecklace.Should().BeNull();
        player.Inventory.Should().Contain(necklace);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Ring1_First()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var ring1 = new Item { Name = "Silver Ring", Type = ItemType.Ring };
        var ring2 = new Item { Name = "Gold Ring", Type = ItemType.Ring };
        var player = new Character
        {
            Name = "Hero",
            EquippedRing1 = ring1,
            EquippedRing2 = ring2
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Ring };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.UnequippedItem.Should().Be(ring1);
        player.EquippedRing1.Should().BeNull();
        player.EquippedRing2.Should().Be(ring2);
        player.Inventory.Should().Contain(ring1);
    }

    [Fact]
    public async Task Handle_Should_Unequip_Ring2_When_Ring1_Empty()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var ring2 = new Item { Name = "Gold Ring", Type = ItemType.Ring };
        var player = new Character
        {
            Name = "Hero",
            EquippedRing1 = null,
            EquippedRing2 = ring2
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Ring };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.UnequippedItem.Should().Be(ring2);
        player.EquippedRing2.Should().BeNull();
        player.Inventory.Should().Contain(ring2);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Slot_Empty()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var player = new Character
        {
            Name = "Hero",
            EquippedMainHand = null
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("No Weapon equipped");
        result.UnequippedItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_No_Ring_Equipped()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var player = new Character
        {
            Name = "Hero",
            EquippedRing1 = null,
            EquippedRing2 = null
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Ring };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("No Ring equipped");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message_With_Item_Name()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var weapon = new Item { Name = "Legendary Blade", Type = ItemType.Weapon };
        var player = new Character
        {
            Name = "Hero",
            EquippedMainHand = weapon
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Weapon };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Be("Unequipped Legendary Blade");
    }

    [Fact]
    public async Task Handle_Should_Add_To_Inventory_After_Unequip()
    {
        // Arrange
        var handler = new UnequipItemHandler(NullLogger<UnequipItemHandler>.Instance);
        var helmet = new Item { Name = "Diamond Helmet", Type = ItemType.Helmet, Rarity = ItemRarity.Legendary };
        var existingItem = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        var player = new Character
        {
            Name = "Hero",
            EquippedHelmet = helmet,
            Inventory = new List<Item> { existingItem }
        };
        var command = new UnequipItemCommand { Player = player, SlotType = ItemType.Helmet };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().HaveCount(2);
        player.Inventory.Should().Contain(helmet);
        player.Inventory.Should().Contain(existingItem);
    }
}
