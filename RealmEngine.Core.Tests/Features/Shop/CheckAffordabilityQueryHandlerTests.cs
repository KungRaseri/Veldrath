using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class CheckAffordabilityQueryHandlerTests
{
    private static ShopEconomyService FakeShopService() =>
        new(new ItemDataService(null!, NullLogger<ItemDataService>.Instance),
            NullLogger<ShopEconomyService>.Instance);

    private static NPC CreateMerchant(string id = "merchant-1") => new()
    {
        Id = id,
        Name = "Test Merchant",
        Traits = new Dictionary<string, TraitValue>
        {
            ["isMerchant"] = new TraitValue(true, TraitType.Boolean)
        }
    };

    private static CheckAffordabilityQueryHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ShopEconomyService?     shopSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            shopSvc ?? FakeShopService(),
            NullLogger<CheckAffordabilityQueryHandler>.Instance);

    // ── Failure paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotFound()
    {
        var save = new SaveGame { KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new CheckAffordabilityQuery("unknown", "item-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemNotInMerchantInventory()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        shopSvc.GetOrCreateInventory(merchant); // empty inventory

        var save = new SaveGame { KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "missing-item"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // ── Affordability logic ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsCanAfford_WhenPlayerHasEnoughGold()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        var item = new Item { Id = "sword-1", Name = "Iron Sword", Price = 100, Rarity = ItemRarity.Common };
        shopSvc.GetOrCreateInventory(merchant).CoreItems.Add(item);

        // ItemRarity.Common quality multiplier = 1.0, so price = 100. Player has 500.
        var save = new SaveGame
        {
            KnownNPCs = [merchant],
            Character = new Character { Gold = 500 }
        };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "sword-1"), default);

        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeTrue();
        result.GoldShortfall.Should().Be(0);
        result.ItemName.Should().Be("Iron Sword");
    }

    [Fact]
    public async Task Handle_ReturnsCannotAfford_WhenPlayerHasInsufficientGold()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        var item = new Item { Id = "sword-1", Name = "Iron Sword", Price = 100, Rarity = ItemRarity.Common };
        shopSvc.GetOrCreateInventory(merchant).CoreItems.Add(item);

        // Player has 0 gold — can not possibly afford.
        var save = new SaveGame
        {
            KnownNPCs = [merchant],
            Character = new Character { Gold = 0 }
        };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "sword-1"), default);

        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeFalse();
        result.GoldShortfall.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_FindsItemInDynamicInventory()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        var item = new Item { Id = "potion-1", Name = "Health Potion", Price = 30, Rarity = ItemRarity.Common };
        shopSvc.GetOrCreateInventory(merchant).DynamicItems.Add(item);

        var save = new SaveGame
        {
            KnownNPCs = [merchant],
            Character = new Character { Gold = 1000 }
        };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "potion-1"), default);

        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeTrue();
        result.ItemName.Should().Be("Health Potion");
    }

    [Fact]
    public async Task Handle_GoldShortfall_EqualsDeficit_WhenPlayerCannotAfford()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();
        // Common item, price=100, quality multiplier=1.0 → sell price = 100
        var item = new Item { Id = "sword-1", Name = "Sword", Price = 100, Rarity = ItemRarity.Common };
        shopSvc.GetOrCreateInventory(merchant).CoreItems.Add(item);

        var save = new SaveGame
        {
            KnownNPCs = [merchant],
            Character = new Character { Gold = 60 }
        };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new CheckAffordabilityQuery("merchant-1", "sword-1"), default);

        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeFalse();
        // shortfall = price − player gold
        result.GoldShortfall.Should().Be(result.ItemPrice - 60);
    }
}
