using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class RefreshMerchantInventoryHandlerTests
{
    // ShopEconomyService has no null-checks in constructor — safe to pass null for infra dependencies
    // in tests that exercise early-exit validation paths.
    private static ShopEconomyService FakeShopService() =>
        new(null!, NullLogger<ShopEconomyService>.Instance);

    private static RefreshMerchantInventoryCommandHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            FakeShopService(),
            NullLogger<RefreshMerchantInventoryCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("merchant-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotInSave()
    {
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("nonexistent-merchant"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Merchant not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNpcIsNotAMerchant()
    {
        var guard = new NPC
        {
            Id = "guard-1",
            Name = "Gate Guard",
            Occupation = "Guard",
            Traits = new Dictionary<string, TraitValue>() // No "isMerchant" trait
        };
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [guard] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("guard-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }

    // Lightweight test double for ShopEconomyService — avoids Castle proxy issues
    // with Moq<ShopEconomyService> (requires constructor args Castle can't resolve).
    private sealed class TestShopService(
        ShopInventory returnedInventory,
        Action<NPC, ShopInventory>? onRefresh = null)
        : ShopEconomyService(null!, NullLogger<ShopEconomyService>.Instance)
    {
        public override ShopInventory GetOrCreateInventory(NPC npc) => returnedInventory;
        public override void RefreshDynamicInventory(NPC merchant, ShopInventory inventory)
            => onRefresh?.Invoke(merchant, inventory);
    }

    // Helper: build a valid merchant NPC with isMerchant=true
    private static NPC MakeMerchant(string id = "merchant-1", string shopType = "hybrid")
        => new()
        {
            Id = id,
            Name = "Elara the Trader",
            Occupation = "Merchant",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"]         = new TraitValue(true,        TraitType.Boolean),
                ["shopInventoryType"]  = new TraitValue(shopType,    TraitType.String),
                ["shopDynamicCount"]   = new TraitValue(3,           TraitType.Number)
            }
        };

    private static RefreshMerchantInventoryCommandHandler CreateHandlerWithShop(
        Mock<ISaveGameService> saveSvc,
        ShopEconomyService shopSvc)
        => new(
            saveSvc.Object,
            shopSvc,
            NullLogger<RefreshMerchantInventoryCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsSuccess_AndReportsCorrectCounts_ForHybridShop()
    {
        var merchant  = MakeMerchant();
        var inventory = new ShopInventory
        {
            MerchantId   = merchant.Id,
            DynamicItems = [ new Item { Id = "existing-1", Name = "Old Sword" } ]
        };

        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave())
               .Returns(new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] });

        var shopSvc = new TestShopService(inventory, (_, inv) =>
        {
            inv.DynamicItems.Clear();
            inv.DynamicItems.Add(new Item { Id = "new-1", Name = "Iron Dagger" });
            inv.DynamicItems.Add(new Item { Id = "new-2", Name = "Leather Gloves" });
        });

        var result = await CreateHandlerWithShop(saveSvc, shopSvc).Handle(
            new RefreshMerchantInventoryCommand(merchant.Id), default);

        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(2);   // 2 new items generated
        result.ItemsRemoved.Should().Be(1); // 1 old item was cleared
        result.ItemsExpired.Should().Be(0);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsZeroCounts_ForStaticShopType()
    {
        var merchant  = MakeMerchant(shopType: "static-only");
        var inventory = new ShopInventory
        {
            MerchantId   = merchant.Id,
            DynamicItems = [ new Item { Id = "existing-1", Name = "Sword" } ]
        };

        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave())
               .Returns(new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] });

        bool refreshCalled = false;
        var shopSvc = new TestShopService(inventory, (_, _) => refreshCalled = true);

        var result = await CreateHandlerWithShop(saveSvc, shopSvc).Handle(
            new RefreshMerchantInventoryCommand(merchant.Id), default);

        result.Success.Should().BeTrue();
        result.ItemsAdded.Should().Be(0);
        result.ItemsRemoved.Should().Be(0);
        refreshCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RemovesExpiredPlayerItems_AndReportsCorrectExpiredCount()
    {
        var merchant = MakeMerchant();
        var expired  = new PlayerSoldItem { Item = new Item { Id = "old-1", Name = "Rusty Sword" }, DaysRemaining = 0  };
        var valid    = new PlayerSoldItem { Item = new Item { Id = "ok-1",  Name = "Fine Bow"    }, DaysRemaining = 3  };
        var inventory = new ShopInventory
        {
            MerchantId      = merchant.Id,
            DynamicItems    = [],
            PlayerSoldItems = [expired, valid]
        };

        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave())
               .Returns(new SaveGame { PlayerName = "Hero", KnownNPCs = [merchant] });

        var shopSvc = new TestShopService(inventory); // no refresh callback — no-op

        var result = await CreateHandlerWithShop(saveSvc, shopSvc).Handle(
            new RefreshMerchantInventoryCommand(merchant.Id), default);

        result.Success.Should().BeTrue();
        result.ItemsExpired.Should().Be(1);
        inventory.PlayerSoldItems.Should().ContainSingle(p => p.Item.Id == "ok-1");
        inventory.PlayerSoldItems.Should().NotContain(p => p.Item.Id == "old-1");
    }
}
