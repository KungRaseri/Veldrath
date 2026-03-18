using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Inventory.Queries.CheckItemEquipped;
using RealmEngine.Core.Features.Inventory.Queries.GetEquippedItems;
using RealmEngine.Core.Features.Inventory.Queries.GetInventoryValue;
using RealmEngine.Core.Features.Inventory.Queries.GetPlayerInventory;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Inventory.Queries;

public class GetEquippedItemsQueryHandlerTests
{
    private static GetEquippedItemsQueryHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>(), NullLogger<GetEquippedItemsQueryHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetEquippedItemsQuery(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsLoadout_WhenCharacterHasNoItems()
    {
        var saveGame = new SaveGame { Character = new Character { Name = "Hero" } };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetEquippedItemsQuery(), default);

        result.Success.Should().BeTrue();
        result.Equipment.Should().NotBeNull();
        result.Equipment!.MainHand.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsEquippedItems_WhenCharacterHasGear()
    {
        var sword = new Item { Name = "Long Sword", Type = ItemType.Weapon, Price = 100 };
        var character = new Character { Name = "Warrior", EquippedMainHand = sword };
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetEquippedItemsQuery(), default);

        result.Success.Should().BeTrue();
        result.Equipment!.MainHand.Should().Be(sword);
        result.Stats!.TotalEquippedItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CalculatesStats_WhenMultipleItemsEquipped()
    {
        var sword = new Item { Name = "Sword", Type = ItemType.Weapon, Price = 200 };
        var helm = new Item { Name = "Helm", Type = ItemType.Helmet, Price = 150 };
        var character = new Character
        {
            Name = "Fighter",
            EquippedMainHand = sword,
            EquippedHelmet = helm
        };
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetEquippedItemsQuery(), default);

        result.Stats!.TotalEquippedItems.Should().Be(2);
        result.Stats.TotalValue.Should().Be(350);
    }
}

public class CheckItemEquippedQueryHandlerTests
{
    private static CheckItemEquippedQueryHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>(), NullLogger<CheckItemEquippedQueryHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new CheckItemEquippedQuery { ItemId = "sword-01" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenItemNotEquipped()
    {
        var saveGame = new SaveGame { Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new CheckItemEquippedQuery { ItemId = "some-item" }, default);

        result.Success.Should().BeTrue();
        result.IsEquipped.Should().BeFalse();
        result.EquipSlot.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenItemEquippedInMainHand()
    {
        var sword = new Item { Id = "sword-01", Name = "Sword", Type = ItemType.Weapon };
        var saveGame = new SaveGame { Character = new Character { EquippedMainHand = sword } };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new CheckItemEquippedQuery { ItemId = "sword-01" }, default);

        result.Success.Should().BeTrue();
        result.IsEquipped.Should().BeTrue();
        result.EquipSlot.Should().Be("MainHand");
    }

    [Theory]
    [InlineData("OffHand")]
    [InlineData("Helmet")]
    [InlineData("Chest")]
    [InlineData("Boots")]
    [InlineData("Ring1")]
    public async Task Handle_ReturnsCorrectSlot_ForDifferentEquipmentSlots(string slotName)
    {
        var item = new Item { Id = "item-01", Name = "Gear Piece" };
        var character = new Character();
        switch (slotName)
        {
            case "OffHand": character.EquippedOffHand = item; break;
            case "Helmet": character.EquippedHelmet = item; break;
            case "Chest": character.EquippedChest = item; break;
            case "Boots": character.EquippedBoots = item; break;
            case "Ring1": character.EquippedRing1 = item; break;
        }
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new CheckItemEquippedQuery { ItemId = "item-01" }, default);

        result.IsEquipped.Should().BeTrue();
        result.EquipSlot.Should().Be(slotName);
    }
}

public class GetInventoryValueQueryHandlerTests
{
    private static GetInventoryValueQueryHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>(), NullLogger<GetInventoryValueQueryHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetInventoryValueQuery(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsZeroValue_WhenInventoryEmpty()
    {
        var saveGame = new SaveGame { Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetInventoryValueQuery(), default);

        result.Success.Should().BeTrue();
        result.TotalValue.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsSumOfItemPrices_FromInventory()
    {
        var character = new Character();
        character.Inventory.Add(new Item { Name = "Potion", Price = 50 });
        character.Inventory.Add(new Item { Name = "Scroll", Price = 75 });
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetInventoryValueQuery { IncludeEquipped = false }, default);

        result.Success.Should().BeTrue();
        result.UnequippedValue.Should().Be(125);
    }
}

public class GetPlayerInventoryQueryHandlerTests
{
    private static GetPlayerInventoryQueryHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>(), NullLogger<GetPlayerInventoryQueryHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPlayerInventoryQuery(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenInventoryEmpty()
    {
        var saveGame = new SaveGame { Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPlayerInventoryQuery(), default);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllItems_WhenNoFilterApplied()
    {
        var character = new Character();
        character.Inventory.Add(new Item { Name = "Sword", Type = ItemType.Weapon });
        character.Inventory.Add(new Item { Name = "Potion", Type = ItemType.Consumable });
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPlayerInventoryQuery(), default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersItemsByType_WhenTypeFilterApplied()
    {
        var character = new Character();
        character.Inventory.Add(new Item { Name = "Sword", Type = ItemType.Weapon });
        character.Inventory.Add(new Item { Name = "Potion", Type = ItemType.Consumable });
        var saveGame = new SaveGame { Character = character };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetPlayerInventoryQuery { ItemTypeFilter = "Consumable" }, default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Potion");
    }
}
