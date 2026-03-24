using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class GetMerchantInfoQueryHandlerTests
{
    private static ShopEconomyService FakeShopService() =>
        new(new ItemDataService(null!, NullLogger<ItemDataService>.Instance),
            NullLogger<ShopEconomyService>.Instance);

    private static NPC CreateMerchant(
        string id = "merchant-1",
        string shopInventoryType = "hybrid") => new()
        {
            Id = id,
            Name = "Tharin",
            Occupation = "Blacksmith",
            Gold = 500,
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
                ["shopType"] = new TraitValue("weapons", TraitType.String),
                ["shopInventoryType"] = new TraitValue(shopInventoryType, TraitType.String),
            }
        };

    private static GetMerchantInfoQueryHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ShopEconomyService? shopSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            shopSvc ?? FakeShopService(),
            NullLogger<GetMerchantInfoQueryHandler>.Instance);

    // Failure paths
    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("merchant-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotFound()
    {
        var save = new SaveGame { KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("unknown"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNpcIsNotAMerchant()
    {
        var npc = new NPC { Id = "villager-1", Name = "Villager", Traits = [] };
        var save = new SaveGame { KnownNPCs = [npc] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("villager-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }

    // Success paths
    [Fact]
    public async Task Handle_ReturnsSuccess_WithMerchantDetails()
    {
        var merchant = CreateMerchant();
        var save = new SaveGame { KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("merchant-1"), default);

        result.Success.Should().BeTrue();
        result.Merchant.Should().NotBeNull();
        result.Merchant!.Id.Should().Be("merchant-1");
        result.Merchant.Name.Should().Be("Tharin");
        result.Merchant.ShopType.Should().Be("weapons");
    }

    [Theory]
    [InlineData("hybrid", true)]
    [InlineData("dynamic-only", true)]
    [InlineData("core-only", false)]
    [InlineData("static", false)]
    public async Task Handle_AcceptsPlayerItems_MatchesExpectedInventoryType(
        string inventoryType, bool expectedAccepts)
    {
        var merchant = CreateMerchant(shopInventoryType: inventoryType);
        var save = new SaveGame { KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("merchant-1"), default);

        result.Success.Should().BeTrue();
        result.Merchant!.AcceptsPlayerItems.Should().Be(expectedAccepts);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultShopType_WhenShopTypeTraitMissing()
    {
        var merchant = new NPC
        {
            Id = "merchant-2",
            Name = "Generic Merchant",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(true, TraitType.Boolean)
                // shopType omitted intentionally
            }
        };
        var save = new SaveGame { KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc)
            .Handle(new GetMerchantInfoQuery("merchant-2"), default);

        result.Success.Should().BeTrue();
        result.Merchant!.ShopType.Should().Be("general");
    }

    [Fact]
    public async Task Handle_InventoryItemCounts_ReflectAddedItems()
    {
        var shopSvc = FakeShopService();
        var merchant = CreateMerchant();

        // Pre-populate the inventory so we can verify counts.
        var inventory = shopSvc.GetOrCreateInventory(merchant);
        inventory.CoreItems.Add(new Item { Id = "item-1", Name = "Sword", Price = 50, Rarity = ItemRarity.Common });
        inventory.CoreItems.Add(new Item { Id = "item-2", Name = "Axe", Price = 75, Rarity = ItemRarity.Uncommon });

        var save = new SaveGame { KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc, shopSvc)
            .Handle(new GetMerchantInfoQuery("merchant-1"), default);

        result.Success.Should().BeTrue();
        result.Merchant!.CoreItemsCount.Should().Be(2);
        result.Merchant.TotalItemsForSale.Should().Be(2);
    }
}
