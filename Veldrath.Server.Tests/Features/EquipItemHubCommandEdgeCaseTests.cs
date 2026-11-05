using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Edge-case tests for <see cref="EquipItemHubCommandHandler"/>: the unequip guard, a corrupt equipment blob, and a
/// catalog fault while deriving a slot from an item.
/// </summary>
public class EquipItemHubCommandEdgeCaseTests
{
    private static Character Character(string equipment = "{}") =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
            EquipmentBlob = equipment,
            InventoryBlob = "[]",
        };

    private static (EquipItemHubCommandHandler Handler, Mock<ICharacterRepository> Characters, Mock<IItemRepository> Items)
        Arrange(Character? character, Item? item = null, Exception? catalogFailure = null)
    {
        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(character);
        characters.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var items = new Mock<IItemRepository>();
        if (catalogFailure is not null)
            items.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(catalogFailure);
        else
            items.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync(item);

        var handler = new EquipItemHubCommandHandler(
            characters.Object, items.Object, NullLogger<EquipItemHubCommandHandler>.Instance);

        return (handler, characters, items);
    }

    [Fact]
    public async Task Handle_RequiresASlot_WhenNeitherSlotNorItemIsGiven()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(
            new EquipItemHubCommand { CharacterId = character.Id },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("A slot is required to unequip an item.");
    }

    [Fact]
    public async Task Handle_TreatsACorruptEquipmentBlobAsEmpty_AndEquipsTheItem()
    {
        var character = Character(equipment: "{not json");
        var (handler, _, _) = Arrange(character, new Item { Slug = "iron_sword", Type = ItemType.Weapon });

        var result = await handler.Handle(
            new EquipItemHubCommand { CharacterId = character.Id, Slot = "MainHand", ItemRef = "iron_sword" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Slot.Should().Be(EquipmentSlots.MainHand);
        result.AllEquippedItems.Should().ContainKey(EquipmentSlots.MainHand).WhoseValue.Should().Be("iron_sword");
        character.EquipmentBlob.Should().Contain("iron_sword");
    }

    [Fact]
    public async Task Handle_RejectsTheItem_WhenTheCatalogFaultsWhileDerivingTheSlot()
    {
        var character = Character();
        var (handler, characters, _) = Arrange(character, catalogFailure: new InvalidOperationException("offline"));

        var result = await handler.Handle(
            new EquipItemHubCommand { CharacterId = character.Id, ItemRef = "iron_sword" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("'iron_sword' cannot be equipped.");
        characters.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DerivesTheSlotFromTheCatalogItem_WhenNoSlotIsGiven()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, new Item { Slug = "iron_helm", Type = ItemType.Helmet });

        var result = await handler.Handle(
            new EquipItemHubCommand { CharacterId = character.Id, ItemRef = "iron_helm" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Slot.Should().Be(EquipmentSlots.Head);
    }

    [Fact]
    public async Task Handle_ClearsTheOffHand_WhenATwoHandedWeaponIsEquipped()
    {
        var character = Character(equipment: """{"OffHand":"buckler"}""");
        var (handler, _, _) = Arrange(character, new Item
        {
            Slug = "greatsword",
            Type = ItemType.Weapon,
            IsTwoHanded = true,
        });

        var result = await handler.Handle(
            new EquipItemHubCommand { CharacterId = character.Id, Slot = "MainHand", ItemRef = "greatsword" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ClearedSlots.Should().Contain(EquipmentSlots.OffHand);
        result.AllEquippedItems.Should().NotContainKey(EquipmentSlots.OffHand);
    }
}
