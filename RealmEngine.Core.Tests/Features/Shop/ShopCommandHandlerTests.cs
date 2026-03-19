using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class BuyFromShopHandlerTests
{
    // Creates a ShopEconomyService whose catalog loader will safely return
    // empty item lists (null DB factory → LoadCatalog catches the NPE and returns []).
    private static ShopEconomyService FakeShopService() =>
        new(new ItemDataService(null!, NullLogger<ItemDataService>.Instance),
            NullLogger<ShopEconomyService>.Instance);

    private static NPC CreateMerchant(string id = "merchant-1") => new()
    {
        Id = id,
        Name = "Test Merchant",
        Traits = new Dictionary<string, TraitValue>
        {
            ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
            ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String)
        }
    };

    private static Item CreateItem(string id = "item-1", int price = 50) => new()
    {
        Id = id,
        Name = "Test Sword",
        Price = price,
        Rarity = ItemRarity.Common
    };

    private static BuyFromShopHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ShopEconomyService? shopSvc = null) =>
        new(
            shopSvc ?? FakeShopService(),
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            NullLogger<BuyFromShopHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new BuyFromShopCommand("merchant-1", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotFound()
    {
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new BuyFromShopCommand("nonexistent", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Merchant not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemNotInShop()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        // Pre-cache an empty inventory so GetOrCreateInventory won't call the DB again.
        shopSvc.GetOrCreateInventory(merchant);

        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc).Handle(
            new BuyFromShopCommand(merchant.Id, "missing-item"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Item not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenInsufficientGold()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        var item = CreateItem(price: 100);

        var inventory = shopSvc.GetOrCreateInventory(merchant);
        inventory.CoreItems.Add(item);

        var character = new Character { Name = "Hero", Gold = 10 };
        var save = new SaveGame { PlayerName = "Hero", Character = character, KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc).Handle(
            new BuyFromShopCommand(merchant.Id, item.Id), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough gold");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenPurchaseIsValid()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        var item = CreateItem(price: 50);

        var inventory = shopSvc.GetOrCreateInventory(merchant);
        inventory.CoreItems.Add(item);

        var character = new Character { Name = "Hero", Gold = 200 };
        var save = new SaveGame { PlayerName = "Hero", Character = character, KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc).Handle(
            new BuyFromShopCommand(merchant.Id, item.Id), default);

        result.Success.Should().BeTrue();
        result.ItemPurchased.Should().NotBeNull();
        result.PriceCharged.Should().BeGreaterThan(0);
        result.PlayerGoldRemaining.Should().BeLessThan(200);
        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }
}

[Trait("Category", "Feature")]
public class SellToShopHandlerTests
{
    private static ShopEconomyService FakeShopService() =>
        new(new ItemDataService(null!, NullLogger<ItemDataService>.Instance),
            NullLogger<ShopEconomyService>.Instance);

    private static NPC CreateMerchant(string id = "merchant-1") => new()
    {
        Id = id,
        Name = "Test Merchant",
        Gold = 1000,
        Traits = new Dictionary<string, TraitValue>
        {
            ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
            ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String)
        }
    };

    private static Item CreateItem(string id = "item-1") => new()
    {
        Id = id,
        Name = "Old Shield",
        Price = 80,
        Rarity = ItemRarity.Common
    };

    private static SellToShopHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ShopEconomyService? shopSvc = null) =>
        new(
            shopSvc ?? FakeShopService(),
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            NullLogger<SellToShopHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new SellToShopCommand("merchant-1", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotFound()
    {
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new SellToShopCommand("nonexistent", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Merchant not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemNotInInventory()
    {
        var merchant = CreateMerchant();
        var character = new Character { Name = "Hero" };
        var save = new SaveGame { PlayerName = "Hero", Character = character, KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new SellToShopCommand(merchant.Id, "missing-item"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found in your inventory");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemIsEquippedMainHand()
    {
        var merchant = CreateMerchant();
        var item = CreateItem();
        var character = new Character
        {
            Name = "Hero",
            Inventory = [item],
            EquippedMainHand = item
        };
        var save = new SaveGame { PlayerName = "Hero", Character = character, KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new SellToShopCommand(merchant.Id, item.Id), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("equipped");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenSaleIsValid()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        // Pre-cache the merchant's inventory so BuyFromPlayer won't trigger catalog loading.
        shopSvc.GetOrCreateInventory(merchant);

        var item = CreateItem();
        var character = new Character { Name = "Hero", Gold = 0, Inventory = [item] };
        var save = new SaveGame { PlayerName = "Hero", Character = character, KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc).Handle(
            new SellToShopCommand(merchant.Id, item.Id), default);

        result.Success.Should().BeTrue();
        result.ItemSold.Should().NotBeNull();
        result.PriceReceived.Should().BeGreaterThan(0);
        result.PlayerGoldRemaining.Should().BeGreaterThan(0);
        character.Inventory.Should().NotContain(item);
        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }
}
