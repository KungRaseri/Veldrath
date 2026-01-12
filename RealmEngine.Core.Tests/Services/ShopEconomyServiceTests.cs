using FluentAssertions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
/// <summary>
/// Tests for ShopEconomyService.
/// </summary>
public class ShopEconomyServiceTests
{
    private readonly ShopEconomyService _service;

    public ShopEconomyServiceTests()
    {
        _service = new ShopEconomyService();
    }

    [Fact]
    public void GetOrCreateInventory_Should_Create_Inventory_For_Merchant()
    {
        // Arrange
        var merchant = CreateTestMerchant("Blacksmith");

        // Act
        var inventory = _service.GetOrCreateInventory(merchant);

        // Assert
        inventory.Should().NotBeNull();
        inventory.MerchantId.Should().Be(merchant.Id);
    }

    [Fact]
    public void GetOrCreateInventory_Should_Return_Existing_Inventory()
    {
        // Arrange
        var merchant = CreateTestMerchant("Blacksmith");

        // Act
        var inventory1 = _service.GetOrCreateInventory(merchant);
        var inventory2 = _service.GetOrCreateInventory(merchant);

        // Assert
        inventory1.Should().BeSameAs(inventory2, "should return the same inventory instance");
    }

    [Fact]
    public void GetOrCreateInventory_Should_Throw_For_Non_Merchant()
    {
        // Arrange
        var nonMerchant = new NPC
        {
            Id = "npc-1",
            Name = "Villager",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(false, TraitType.Boolean)
            }
        };

        // Act & Assert
        var act = () => _service.GetOrCreateInventory(nonMerchant);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*is not a merchant*");
    }

    [Fact]
    public void CalculateSellPrice_Should_Return_Positive_Price()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var item = new Item
        {
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50
        };

        // Act
        var price = _service.CalculateSellPrice(item, merchant);

        // Assert
        price.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateSellPrice_Should_Use_Rarity_Based_Quality_Multiplier()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var commonItem = new Item { Name = "Common Sword", Price = 100, Rarity = ItemRarity.Common };
        var rareItem = new Item { Name = "Rare Sword", Price = 100, Rarity = ItemRarity.Rare };
        var legendaryItem = new Item { Name = "Legendary Sword", Price = 100, Rarity = ItemRarity.Legendary };

        // Act
        var commonPrice = _service.CalculateSellPrice(commonItem, merchant);
        var rarePrice = _service.CalculateSellPrice(rareItem, merchant);
        var legendaryPrice = _service.CalculateSellPrice(legendaryItem, merchant);

        // Assert - Quality multiplier based on rarity: 100 / rarityWeight
        // Common (weight=100): 100 * (100/100) = 100
        // Rare (weight=40): 100 * (100/40) = 250
        // Legendary (weight=10): 100 * (100/10) = 1000
        commonPrice.Should().Be(100, "Common rarity has weight 100, multiplier 1.0");
        rarePrice.Should().Be(250, "Rare rarity has weight 40, multiplier 2.5");
        legendaryPrice.Should().Be(1000, "Legendary rarity has weight 10, multiplier 10.0");
    }

    [Fact]
    public void CalculateBuyPrice_Should_Be_Less_Than_Sell_Price()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var item = new Item
        {
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50
        };

        // Act
        var sellPrice = _service.CalculateSellPrice(item, merchant);
        var buyPrice = _service.CalculateBuyPrice(item, merchant);

        // Assert
        buyPrice.Should().BeLessThan(sellPrice,
            "merchants should buy items for less than they sell them");
    }

    [Fact]
    public void CalculateBuyPrice_Should_Be_Around_40_Percent_Of_Sell_Price()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var item = new Item
        {
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 100 // Common item
        };

        // Act
        var sellPrice = _service.CalculateSellPrice(item, merchant);
        var buyPrice = _service.CalculateBuyPrice(item, merchant);

        // Assert - Should be approximately 40% (allowing for rounding)
        var expectedBuyPrice = sellPrice * 0.40;
        buyPrice.Should().BeCloseTo((int)expectedBuyPrice, 10,
            "buy price should be around 40% of sell price");
    }

    [Fact]
    public void CalculateResellPrice_Should_Be_80_Percent_Of_Original_Price()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var item = new Item
        {
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 100
        };

        // Act
        var originalPrice = _service.CalculateSellPrice(item, merchant);
        var resellPrice = _service.CalculateResellPrice(item, merchant);

        // Assert
        var expectedResellPrice = originalPrice * 0.80;
        resellPrice.Should().BeCloseTo((int)expectedResellPrice, 1,
            "resell price should be 80% of original price");
    }

    [Fact]
    public void BuyFromPlayer_Should_Add_Item_To_Inventory()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var inventory = _service.GetOrCreateInventory(merchant);
        var item = new Item
        {
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50
        };

        // Act
        var success = _service.BuyFromPlayer(merchant, item, out var pricePaid);

        // Assert
        success.Should().BeTrue();
        pricePaid.Should().BeGreaterThan(0);
        inventory.PlayerSoldItems.Should().Contain(i => i.Item.Name == "Iron Sword");
    }

    [Fact]
    public void SellToPlayer_Should_Remove_Item_From_Inventory()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var inventory = _service.GetOrCreateInventory(merchant);
        var item = new Item
        {
            Name = "Health Potion",
            Price = 50,
            TotalRarityWeight = 80
        };

        // Add item to merchant inventory first
        inventory.DynamicItems.Add(item);
        var initialCount = inventory.DynamicItems.Count;

        // Act
        var success = _service.SellToPlayer(merchant, item, out var priceCharged);

        // Assert
        success.Should().BeTrue();
        priceCharged.Should().BeGreaterThan(0);
        inventory.DynamicItems.Should().HaveCount(initialCount - 1);
    }

    [Fact]
    public void CalculateSellPrice_Should_Have_Minimum_Price_Of_One()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var cheapItem = new Item
        {
            Name = "Stick",
            Price = 0, // Free item
            TotalRarityWeight = 100
        };

        // Act
        var price = _service.CalculateSellPrice(cheapItem, merchant);

        // Assert
        price.Should().BeGreaterThanOrEqualTo(1, "items should always have a minimum price of 1 gold");
    }

    [Fact]
    public void CalculateBuyPrice_Should_Have_Minimum_Price_Of_One()
    {
        // Arrange
        var merchant = CreateTestMerchant("Merchant");
        var cheapItem = new Item
        {
            Name = "Stick",
            Price = 1,
            TotalRarityWeight = 100
        };

        // Act
        var buyPrice = _service.CalculateBuyPrice(cheapItem, merchant);

        // Assert
        buyPrice.Should().BeGreaterThanOrEqualTo(1, "buy price should always be at least 1 gold");
    }

    [Fact]
    public void Merchant_Background_Should_Affect_Prices()
    {
        // Arrange
        var regularMerchant = CreateTestMerchant("Regular Merchant");
        var specialMerchant = CreateTestMerchant("Special Merchant");
        specialMerchant.Traits["backgroundPriceMultiplier"] = new TraitValue(1.2, TraitType.Number); // 20% more expensive

        var item = new Item { Name = "Sword", Price = 100, TotalRarityWeight = 50 };

        // Act
        var regularPrice = _service.CalculateSellPrice(item, regularMerchant);
        var specialPrice = _service.CalculateSellPrice(item, specialMerchant);

        // Assert
        specialPrice.Should().BeGreaterThan(regularPrice,
            "merchants with price multipliers should charge different prices");
    }

    private NPC CreateTestMerchant(string name)
    {
        return new NPC
        {
            Id = $"merchant-{Guid.NewGuid()}",
            Name = name,
            Gold = 1000, // Give merchant starting gold
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
                ["shopType"] = new TraitValue("general", TraitType.String),
                ["baseInventorySize"] = new TraitValue(20, TraitType.Number),
                ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String) // Required for BuyFromPlayer
            }
        };
    }
}
