using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Equipment.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Equipment.Commands;

/// <summary>
/// Behavioural tests for <see cref="EquipItemHandler"/>: each equipment slot moves the item from the
/// inventory into the slot, returns the displaced item to the inventory and persists the save.
/// </summary>
[Trait("Category", "Feature")]
public class EquipItemHandlerTests
{
    private static (EquipItemHandler Handler, Mock<ISaveGameService> SaveService, SaveGame Save) CreateHandler(
        Item? equippedBefore = null)
    {
        var item = new Item { Id = "new-item", Name = "Iron Sword", Type = ItemType.Weapon };
        var character = new Character
        {
            Name = "Hero",
            Inventory = [item],
            EquippedMainHand = equippedBefore
        };
        var save = new SaveGame { PlayerName = "Hero", Character = character };

        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        return (
            new EquipItemHandler(saveSvc.Object, NullLogger<EquipItemHandler>.Instance),
            saveSvc,
            save);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenThereIsNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = new EquipItemHandler(saveSvc.Object, NullLogger<EquipItemHandler>.Instance);

        var result = await handler.Handle(new EquipItemCommand("hero", "item", EquipmentSlot.MainHand), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("No active game session");
        saveSvc.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never());
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenTheItemIsNotInTheInventory()
    {
        var (handler, saveSvc, _) = CreateHandler();

        var result = await handler.Handle(new EquipItemCommand("hero", "missing-item", EquipmentSlot.MainHand), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("missing-item");
        saveSvc.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never());
    }

    [Theory]
    [InlineData(EquipmentSlot.MainHand)]
    [InlineData(EquipmentSlot.OffHand)]
    [InlineData(EquipmentSlot.Head)]
    [InlineData(EquipmentSlot.Shoulders)]
    [InlineData(EquipmentSlot.Chest)]
    [InlineData(EquipmentSlot.Bracers)]
    [InlineData(EquipmentSlot.Gloves)]
    [InlineData(EquipmentSlot.Belt)]
    [InlineData(EquipmentSlot.Legs)]
    [InlineData(EquipmentSlot.Boots)]
    [InlineData(EquipmentSlot.Necklace)]
    [InlineData(EquipmentSlot.Ring1)]
    [InlineData(EquipmentSlot.Ring2)]
    public async Task Handle_EquipsTheItemIntoTheRequestedSlot(EquipmentSlot slot)
    {
        var (handler, saveSvc, save) = CreateHandler();

        var result = await handler.Handle(new EquipItemCommand("hero", "new-item", slot), default);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Iron Sword").And.Contain(slot.ToString());
        EquippedItem(save.Character, slot)!.Id.Should().Be("new-item");
        save.Character.Inventory.Should().NotContain(i => i.Id == "new-item");
        saveSvc.Verify(s => s.SaveGame(save), Times.Once());
    }

    [Fact]
    public async Task Handle_ReturnsPreviouslyEquippedItemToTheInventory()
    {
        var oldWeapon = new Item { Id = "old-sword", Name = "Rusty Sword", Type = ItemType.Weapon };
        var (handler, _, save) = CreateHandler(equippedBefore: oldWeapon);

        var result = await handler.Handle(new EquipItemCommand("hero", "new-item", EquipmentSlot.MainHand), default);

        result.Success.Should().BeTrue();
        result.PreviousItem.Should().BeSameAs(oldWeapon);
        save.Character.EquippedMainHand!.Id.Should().Be("new-item");
        save.Character.Inventory.Should().Contain(oldWeapon);
    }

    /// <summary>Maps an <see cref="EquipmentSlot"/> to the matching <see cref="Character"/> property.</summary>
    private static Item? EquippedItem(Character character, EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => character.EquippedMainHand,
        EquipmentSlot.OffHand => character.EquippedOffHand,
        EquipmentSlot.Head => character.EquippedHelmet,
        EquipmentSlot.Shoulders => character.EquippedShoulders,
        EquipmentSlot.Chest => character.EquippedChest,
        EquipmentSlot.Bracers => character.EquippedBracers,
        EquipmentSlot.Gloves => character.EquippedGloves,
        EquipmentSlot.Belt => character.EquippedBelt,
        EquipmentSlot.Legs => character.EquippedLegs,
        EquipmentSlot.Boots => character.EquippedBoots,
        EquipmentSlot.Necklace => character.EquippedNecklace,
        EquipmentSlot.Ring1 => character.EquippedRing1,
        EquipmentSlot.Ring2 => character.EquippedRing2,
        _ => null
    };
}
