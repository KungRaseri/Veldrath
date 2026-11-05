using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

using DataItem = RealmEngine.Data.Entities.Item;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Covers the inventory side of <see cref="ShopEconomyService"/>: merchant buy-price modifiers,
/// the player-sale guards, the three sell paths (core, dynamic, player-sold), daily refresh with
/// player-item decay, and the weighted catalog selection per shop type.
/// </summary>
/// <remarks>
/// The service owns a private <see cref="Random"/>, so the tests drive it through traits
/// (shop type, counts) that remove randomness where possible; where the roll stays (weighted item
/// pick) the tests assert counts, types and ranges rather than exact identities.
/// </remarks>
[Trait("Category", "Service")]
public class ShopEconomyServiceInventoryTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static ContentDbContext CreateDb(string databaseName) =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static IDbContextFactory<ContentDbContext> CreateFactory(string databaseName)
    {
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(databaseName));
        return factory.Object;
    }

    private static DataItem MakeEntity(string slug, string typeKey, string itemType, int rarityWeight = 90) => new()
    {
        Slug = slug,
        DisplayName = slug,
        TypeKey = typeKey,
        ItemType = itemType,
        WeaponType = itemType == "weapon" ? "sword" : null,
        ArmorType = itemType == "armor" ? "chest" : null,
        IsActive = true,
        RarityWeight = rarityWeight
    };

    private static string SeedCatalog()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        db.Items.AddRange(
            MakeEntity("iron-sword", "weapons", "weapon", rarityWeight: 90),
            MakeEntity("fine-blade", "weapons", "weapon", rarityWeight: 60),
            MakeEntity("iron-chest", "armor", "armor", rarityWeight: 90),
            MakeEntity("healing-draught", "consumables", "consumable", rarityWeight: 90),
            MakeEntity("rare-tonic", "consumables", "consumable", rarityWeight: 30));
        db.SaveChanges();
        return databaseName;
    }

    private static ShopEconomyService CreateService(string databaseName) =>
        new(
            new ItemDataService(CreateFactory(databaseName), NullLogger<ItemDataService>.Instance),
            NullLogger<ShopEconomyService>.Instance);

    private static NPC MakeMerchant(
        Dictionary<string, TraitValue>? overrides = null,
        int gold = 1000)
    {
        var traits = new Dictionary<string, TraitValue>
        {
            ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
            ["shopType"] = new TraitValue("general", TraitType.String),
            ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String),
            ["shopCoreCount"] = new TraitValue(3, TraitType.Number),
            ["shopDynamicCount"] = new TraitValue(2, TraitType.Number)
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                traits[key] = value;
            }
        }

        return new NPC
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Merchant",
            Gold = gold,
            Traits = traits
        };
    }

    private static Item MakeItem(int price = 100, ItemRarity rarity = ItemRarity.Common, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Item",
        Price = price,
        Rarity = rarity
    };

    private static void SetLastRefreshDate(ShopEconomyService service, DateTime date) =>
        typeof(ShopEconomyService)
            .GetField("_lastRefreshDate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, date);

    // ── Buy / sell prices ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateBuyPrice_AppliesBackgroundAndTraitBuyMultipliers()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new()
        {
            ["backgroundBuyPriceMultiplier"] = new TraitValue(1.5, TraitType.Number),
            ["traitBuyPriceMultiplier"] = new TraitValue(1.5, TraitType.Number)
        });
        var item = MakeItem(price: 100);

        var buyPrice = service.CalculateBuyPrice(item, merchant);

        service.CalculateSellPrice(item, merchant).Should().Be(100);
        buyPrice.Should().Be(90, "40% base x 1.5 background x 1.5 trait = 90% of the sell price");
    }

    [Fact]
    public void CalculateSellPrice_AppliesTraitPriceMultiplierAndQualityBonus()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new()
        {
            ["traitPriceMultiplier"] = new TraitValue(0.5, TraitType.Number),
            ["traitQualityBonus"] = new TraitValue(20, TraitType.Number)
        });

        var price = service.CalculateSellPrice(MakeItem(price: 100), merchant);

        price.Should().Be(60, "100 base x 0.5 trait, then +20% quality bonus");
    }

    [Fact]
    public void CalculateSellPrice_UnknownRarityFallsBackToWeightFifty()
    {
        var service = CreateService(SeedCatalog());
        var item = MakeItem(price: 100, rarity: (ItemRarity)42);

        service.CalculateSellPrice(item, MakeMerchant()).Should().Be(200, "100 / 50 weight = 2x for unknown rarities");
    }

    // ── BuyFromPlayer guards ──────────────────────────────────────────────────

    [Fact]
    public void BuyFromPlayer_RejectsMerchantThatDoesNotAcceptPlayerItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        merchant.Traits.Remove("shopInventoryType");

        var accepted = service.BuyFromPlayer(merchant, MakeItem(), out var pricePaid);

        accepted.Should().BeFalse();
        pricePaid.Should().Be(0);
        merchant.Gold.Should().Be(1000, "a rejected sale must not move gold");
    }

    [Fact]
    public void BuyFromPlayer_RejectsStaticShopInventoryType()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new() { ["shopInventoryType"] = new TraitValue("static", TraitType.String) });

        var accepted = service.BuyFromPlayer(merchant, MakeItem(), out _);

        accepted.Should().BeFalse();
    }

    [Fact]
    public void BuyFromPlayer_Rejects_WhenMerchantCannotAffordTheItem()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(gold: 1);

        var accepted = service.BuyFromPlayer(merchant, MakeItem(price: 100), out var pricePaid);

        accepted.Should().BeFalse();
        pricePaid.Should().Be(40);
        merchant.Gold.Should().Be(1);
        service.GetOrCreateInventory(merchant).PlayerSoldItems.Should().BeEmpty();
    }

    [Fact]
    public void BuyFromPlayer_AddsDecayingPlayerItemAndDeductsGold()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(gold: 500);
        var item = MakeItem(price: 100);
        var inventory = service.GetOrCreateInventory(merchant);

        var accepted = service.BuyFromPlayer(merchant, item, out var pricePaid);

        accepted.Should().BeTrue();
        pricePaid.Should().Be(40);
        merchant.Gold.Should().Be(460);
        var sold = inventory.PlayerSoldItems.Should().ContainSingle().Subject;
        sold.Item.Should().BeSameAs(item);
        sold.DaysRemaining.Should().Be(7);
        sold.PurchaseDate.Should().Be(DateTime.UtcNow.Date);
        sold.ResellPrice.Should().Be(80, "resell price is 80% of the merchant sell price");
    }

    // ── SellToPlayer paths ────────────────────────────────────────────────────

    [Fact]
    public void SellToPlayer_LeavesCoreItemsInStock()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        var inventory = service.GetOrCreateInventory(merchant);
        var coreItem = MakeItem(id: "core-item");
        inventory.CoreItems.Add(coreItem);

        var sold = service.SellToPlayer(merchant, coreItem, out var priceCharged);

        sold.Should().BeTrue();
        priceCharged.Should().BeGreaterThan(0);
        inventory.CoreItems.Should().Contain(coreItem, "core items are unlimited stock");
    }

    [Fact]
    public void SellToPlayer_ChargesResellPrice_ForPlayerSoldItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        var inventory = service.GetOrCreateInventory(merchant);
        var item = MakeItem(price: 100);
        inventory.PlayerSoldItems.Add(new PlayerSoldItem
        {
            Item = item,
            PurchaseDate = DateTime.UtcNow.Date,
            DaysRemaining = 7,
            ResellPrice = 42
        });

        var sold = service.SellToPlayer(merchant, item, out var priceCharged);

        sold.Should().BeTrue();
        priceCharged.Should().Be(42, "the decaying resell price is charged instead of the catalog price");
        inventory.PlayerSoldItems.Should().BeEmpty();
    }

    [Fact]
    public void SellToPlayer_ReturnsFalse_WhenTheItemIsNotStocked()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        service.GetOrCreateInventory(merchant);

        var sold = service.SellToPlayer(merchant, MakeItem(), out _);

        sold.Should().BeFalse();
    }

    [Fact]
    public void SellToPlayer_RemovesDynamicItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        var inventory = service.GetOrCreateInventory(merchant);
        var dynamicItem = MakeItem(id: "dynamic-item");
        inventory.DynamicItems.Add(dynamicItem);

        service.SellToPlayer(merchant, dynamicItem, out _).Should().BeTrue();

        inventory.DynamicItems.Should().NotContain(dynamicItem);
    }

    // ── Refresh / decay ───────────────────────────────────────────────────────

    [Fact]
    public void RefreshDynamicInventory_RegeneratesFromCatalog_AndStampsRefreshDate()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new()
        {
            ["shopType"] = new TraitValue("weaponsmith", TraitType.String),
            ["shopDynamicCount"] = new TraitValue(2, TraitType.Number)
        });
        var inventory = service.GetOrCreateInventory(merchant);
        inventory.DynamicItems.Add(MakeItem(id: "stale"));

        service.RefreshDynamicInventory(merchant, inventory);

        inventory.DynamicItems.Should().HaveCount(2);
        inventory.DynamicItems.Should().NotContain(i => i.Id == "stale");
        inventory.DynamicItems.Should().OnlyContain(i => i.Type == ItemType.Weapon);
        inventory.LastRefresh.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public void RefreshDynamicInventory_FallsBackToAllItems_WhenNoUncommonPlusItemsExist()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new()
        {
            ["shopType"] = new TraitValue("armorer", TraitType.String),
            ["shopDynamicCount"] = new TraitValue(1, TraitType.Number)
        });
        var inventory = service.GetOrCreateInventory(merchant);

        service.RefreshDynamicInventory(merchant, inventory);

        inventory.DynamicItems.Should().HaveCount(1);
        inventory.DynamicItems[0].Rarity.Should().Be(ItemRarity.Common, "the only armor item is common, so the filter falls back");
    }

    [Fact]
    public void RefreshDynamicInventory_WithoutCountTrait_UsesFiveToTenItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        merchant.Traits.Remove("shopDynamicCount");
        var inventory = service.GetOrCreateInventory(merchant);

        service.RefreshDynamicInventory(merchant, inventory);

        inventory.DynamicItems.Count.Should().BeInRange(5, 10);
    }

    [Fact]
    public void GetOrCreateInventory_OnANewDay_RefreshesDynamicItems_AndDecaysPlayerItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();
        var inventory = service.GetOrCreateInventory(merchant);

        var expired = new PlayerSoldItem
        {
            Item = MakeItem(id: "expired"),
            PurchaseDate = DateTime.UtcNow.Date.AddDays(-10),
            DaysRemaining = 7,
            ResellPrice = 100
        };
        var recent = new PlayerSoldItem
        {
            Item = MakeItem(id: "recent"),
            PurchaseDate = DateTime.UtcNow.Date.AddDays(-2),
            DaysRemaining = 7,
            ResellPrice = 100
        };
        inventory.PlayerSoldItems.AddRange([expired, recent]);
        inventory.DynamicItems.Add(MakeItem(id: "yesterdays-stock"));

        SetLastRefreshDate(service, DateTime.UtcNow.Date.AddDays(-1));
        var refreshed = service.GetOrCreateInventory(merchant);

        refreshed.Should().BeSameAs(inventory);
        refreshed.PlayerSoldItems.Should().ContainSingle()
            .Which.Item.Id.Should().Be("recent");
        refreshed.PlayerSoldItems[0].DaysRemaining.Should().Be(5);
        refreshed.PlayerSoldItems[0].ResellPrice.Should().Be(81, "10% decay per day over two days");
        refreshed.DynamicItems.Should().NotContain(i => i.Id == "yesterdays-stock");
        refreshed.DynamicItems.Should().HaveCount(2);
    }

    // ── Core inventory per shop type ─────────────────────────────────────────

    [Theory]
    [InlineData("weaponsmith", 10, ItemType.Weapon)]
    [InlineData("armorer", 10, ItemType.Chest)]
    [InlineData("apothecary", 15, ItemType.Consumable)]
    [InlineData("general", 20, null)]
    [InlineData("oddity-shop", 15, ItemType.Consumable)]
    public void GetOrCreateInventory_UsesShopTypeDefaultsForCoreItemCounts(string shopType, int expectedCount, ItemType? expectedType)
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new() { ["shopType"] = new TraitValue(shopType, TraitType.String) });
        merchant.Traits.Remove("shopCoreCount");

        var inventory = service.GetOrCreateInventory(merchant);

        inventory.CoreItems.Should().HaveCount(expectedCount);
        if (expectedType.HasValue)
        {
            inventory.CoreItems.Should().OnlyContain(i => i.Type == expectedType.Value);
        }
    }

    [Theory]
    [InlineData("weaponsmith", ItemType.Weapon)]
    [InlineData("armorer", ItemType.Chest)]
    [InlineData("alchemist", ItemType.Consumable)]
    [InlineData("unknown-shop", ItemType.Consumable)]
    public void GetOrCreateInventory_MapsCoreItemsToTheShopTypeCategory(string shopType, ItemType expectedType)
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new() { ["shopType"] = new TraitValue(shopType, TraitType.String) });
        merchant.Traits["shopCoreCount"] = new TraitValue(1, TraitType.Number);

        var inventory = service.GetOrCreateInventory(merchant);

        inventory.CoreItems.Should().ContainSingle().Which.Type.Should().Be(expectedType);
    }

    [Fact]
    public void GetOrCreateInventory_BlacksmithShop_SellsWeaponsAndArmorOnly()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant(new() { ["shopType"] = new TraitValue("blacksmith", TraitType.String) });
        merchant.Traits["shopCoreCount"] = new TraitValue(2, TraitType.Number);

        var inventory = service.GetOrCreateInventory(merchant);

        inventory.CoreItems.Should().HaveCount(2);
        inventory.CoreItems.Should().OnlyContain(i => i.Type == ItemType.Weapon || i.Type == ItemType.Chest);
    }

    [Fact]
    public void GetOrCreateInventory_UsesExplicitCoreCountTrait_AndGeneratesCatalogItems()
    {
        var service = CreateService(SeedCatalog());
        var merchant = MakeMerchant();

        var inventory = service.GetOrCreateInventory(merchant);

        inventory.CoreItems.Should().HaveCount(3);
        inventory.CoreItems.Should().OnlyContain(i => i.Price >= 0);
        inventory.MerchantId.Should().Be(merchant.Id);
        inventory.MerchantName.Should().Be("Merchant");
        inventory.LastRefresh.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public void GetOrCreateInventory_WithEmptyCatalog_ProducesNoCoreItems()
    {
        var service = CreateService(Guid.NewGuid().ToString());
        var merchant = MakeMerchant();

        var inventory = service.GetOrCreateInventory(merchant);

        inventory.CoreItems.Should().BeEmpty();
    }
}
