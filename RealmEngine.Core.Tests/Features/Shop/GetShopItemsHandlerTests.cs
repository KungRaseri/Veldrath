using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class GetShopItemsHandlerTests
{
    // Use a mock DbContextFactory + ItemDataService so GetOrCreateInventory can run without a real DB.
    // ItemDataService.LoadCatalog swallows exceptions and returns [] when the factory returns null.
    private static ShopEconomyService RealShopService()
    {
        var dbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        var itemSvc = new ItemDataService(dbFactory.Object, NullLogger<ItemDataService>.Instance);
        return new ShopEconomyService(itemSvc, NullLogger<ShopEconomyService>.Instance);
    }

    private static GetShopItemsHandler CreateHandler(Mock<ISaveGameService>? saveSvc = null) =>
        new(
            RealShopService(),
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            NullLogger<GetShopItemsHandler>.Instance);

    private static NPC MakeMerchant(string id = "merchant-1", string name = "Trader") =>
        new()
        {
            Id = id,
            Name = name,
            Occupation = "Merchant",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(true, TraitType.Boolean)
            }
        };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new GetShopItemsQuery("merchant-1"), default);

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
            new GetShopItemsQuery("nonexistent"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Merchant not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNpcIsNotAMerchant()
    {
        var guard = new NPC
        {
            Id = "guard-1",
            Name = "City Guard",
            Occupation = "Guard",
            Traits = new Dictionary<string, TraitValue>()
        };
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [guard] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GetShopItemsQuery("guard-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithMerchantName()
    {
        var merchant = MakeMerchant("shop-1", "Aldric the Merchant");
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GetShopItemsQuery("shop-1"), default);

        result.Success.Should().BeTrue();
        result.MerchantName.Should().Be("Aldric the Merchant");
    }

    [Fact]
    public async Task Handle_ReturnsResult_WithInventoryCategories()
    {
        var merchant = MakeMerchant();
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GetShopItemsQuery("merchant-1"), default);

        result.Success.Should().BeTrue();
        result.CoreItems.Should().NotBeNull();
        result.DynamicItems.Should().NotBeNull();
        result.PlayerSoldItems.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_TotalItemCount_MatchesSumOfAllCategories()
    {
        var merchant = MakeMerchant();
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GetShopItemsQuery("merchant-1"), default);

        result.TotalItemCount.Should().Be(
            result.CoreItems.Count + result.DynamicItems.Count + result.PlayerSoldItems.Count);
    }
}
