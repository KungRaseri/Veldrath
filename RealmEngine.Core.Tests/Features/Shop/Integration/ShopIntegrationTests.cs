using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Shop.Integration;

/// <summary>
/// Integration tests for the complete shop system workflow.
/// Tests: Browse shop → Buy items → Sell items → Price calculations.
/// Demonstrates proper service registration for consuming applications.
/// </summary>
[Trait("Category", "Integration")]
public class ShopIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;
    private readonly ShopEconomyService _shopService;
    private readonly SaveGame _testSaveGame;
    private readonly NPC _testMerchant;

    public ShopIntegrationTests()
    {
        var services = new ServiceCollection();

        // Mock IApocalypseTimer
        var mockTimer = new Mock<IApocalypseTimer>();
        mockTimer.Setup(t => t.GetBonusMinutes()).Returns(0);
        services.AddSingleton(mockTimer.Object);

        // Mock repository for testing
        var mockRepository = new Mock<ISaveGameRepository>();
        mockRepository.Setup(r => r.SaveGame(It.IsAny<SaveGame>()))
            .Callback<SaveGame>(s => { /* No-op for tests */ });

        // Register logging
        services.AddLogging();

        // Register MediatR with shop command handlers
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(BrowseShopCommand).Assembly));

        // Register ItemCatalogLoader with mocked DbContextFactory
        var dbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        services.AddSingleton(dbFactory.Object);
        services.AddSingleton<ItemCatalogLoader>();

        // Register core services
        services.AddSingleton(mockRepository.Object);
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<ISaveGameService>(sp => sp.GetRequiredService<SaveGameService>());
        services.AddSingleton<ShopEconomyService>();

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _saveGameService = _serviceProvider.GetRequiredService<SaveGameService>();
        _shopService = _serviceProvider.GetRequiredService<ShopEconomyService>();

        // Create test save game with merchant
        var character = new Character
        {
            Name = "TestHero",
            ClassName = "Warrior",
            Level = 5,
            Gold = 1000
        };
        _testSaveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(_testSaveGame);

        // Create test merchant
        _testMerchant = CreateTestMerchant();
        _testSaveGame.KnownNPCs.Add(_testMerchant);
    }

    [Fact]
    public async Task Should_Browse_Shop_Successfully()
    {
        // Arrange
        var command = new BrowseShopCommand(_testMerchant.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MerchantName.Should().Be(_testMerchant.Name);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Should_Fail_To_Browse_Non_Merchant()
    {
        // Arrange - Create non-merchant NPC
        var npc = new NPC
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Farmer",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(false, TraitType.Boolean)
            }
        };
        _testSaveGame.KnownNPCs.Add(npc);

        var command = new BrowseShopCommand(npc.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }

    [Fact]
    public async Task Should_Buy_Item_From_Shop_Successfully()
    {
        // Arrange - Add item to merchant inventory
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        var potion = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Health Potion",
            Price = 50,
            TotalRarityWeight = 80,
            Type = ItemType.Consumable
        };
        inventory.DynamicItems.Add(potion);

        var initialGold = _testSaveGame.Character.Gold;
        var initialInventoryCount = _testSaveGame.Character.Inventory.Count;

        var command = new BuyFromShopCommand(_testMerchant.Id, potion.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemPurchased.Should().NotBeNull();
        result.ItemPurchased!.Name.Should().Be("Health Potion");
        result.PriceCharged.Should().BeGreaterThan(0);
        result.PlayerGoldRemaining.Should().Be(initialGold - result.PriceCharged);
        _testSaveGame.Character.Inventory.Should().HaveCount(initialInventoryCount + 1);
    }

    [Fact]
    public async Task Should_Fail_To_Buy_When_Insufficient_Gold()
    {
        // Arrange - Set player gold to 0
        _testSaveGame.Character.Gold = 0;

        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        var expensiveItem = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Legendary Sword",
            Price = 10000,
            TotalRarityWeight = 10,
            Type = ItemType.Weapon
        };
        inventory.DynamicItems.Add(expensiveItem);

        var command = new BuyFromShopCommand(_testMerchant.Id, expensiveItem.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough gold");
    }

    [Fact]
    public async Task Should_Sell_Item_To_Shop_Successfully()
    {
        // Arrange - Give player an item
        var sword = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50,
            Type = ItemType.Weapon
        };
        _testSaveGame.Character.Inventory.Add(sword);

        var initialGold = _testSaveGame.Character.Gold;
        var initialInventoryCount = _testSaveGame.Character.Inventory.Count;

        var command = new SellToShopCommand(_testMerchant.Id, sword.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemSold.Should().NotBeNull();
        result.ItemSold!.Name.Should().Be("Iron Sword");
        result.PriceReceived.Should().BeGreaterThan(0);
        result.PlayerGoldRemaining.Should().Be(initialGold + result.PriceReceived);
        _testSaveGame.Character.Inventory.Should().HaveCount(initialInventoryCount - 1);
    }

    [Fact]
    public async Task Should_Fail_To_Sell_Equipped_Item()
    {
        // Arrange - Give player an equipped item
        var sword = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50,
            Type = ItemType.Weapon
        };
        _testSaveGame.Character.Inventory.Add(sword);
        _testSaveGame.Character.EquippedMainHand = sword;

        var command = new SellToShopCommand(_testMerchant.Id, sword.Id);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cannot sell equipped items");
    }

    [Fact]
    public async Task Should_Fail_To_Sell_Item_Not_In_Inventory()
    {
        // Arrange - Try to sell non-existent item
        var fakeItemId = Guid.NewGuid().ToString();
        var command = new SellToShopCommand(_testMerchant.Id, fakeItemId);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found in your inventory");
    }

    [Fact]
    public async Task Should_Update_Merchant_Gold_After_Transactions()
    {
        // Arrange - Sell item to merchant
        var sword = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50,
            Type = ItemType.Weapon
        };
        _testSaveGame.Character.Inventory.Add(sword);

        var initialMerchantGold = _testMerchant.Gold;

        // Act - Sell to merchant
        var sellResult = await _mediator.Send(new SellToShopCommand(_testMerchant.Id, sword.Id));

        // Assert
        sellResult.Success.Should().BeTrue();
        _testMerchant.Gold.Should().BeLessThan(initialMerchantGold,
            "merchant should lose gold when buying from player");
    }

    [Fact]
    public async Task Should_Show_Player_Sold_Items_In_Shop_Inventory()
    {
        // Arrange - Sell item to merchant
        var sword = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Iron Sword",
            Price = 100,
            TotalRarityWeight = 50,
            Type = ItemType.Weapon
        };
        _testSaveGame.Character.Inventory.Add(sword);

        await _mediator.Send(new SellToShopCommand(_testMerchant.Id, sword.Id));

        // Act - Browse shop
        var browseResult = await _mediator.Send(new BrowseShopCommand(_testMerchant.Id));

        // Assert
        browseResult.Success.Should().BeTrue();
        browseResult.PlayerSoldItems.Should().ContainSingle(
            item => item.Item.Name == "Iron Sword");
    }

    [Fact]
    public async Task Should_Calculate_Different_Buy_And_Sell_Prices()
    {
        // Arrange
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        var item = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Item",
            Price = 100,
            TotalRarityWeight = 50,
            Type = ItemType.Weapon
        };
        inventory.DynamicItems.Add(item);

        // Act - Browse to see prices
        var browseResult = await _mediator.Send(new BrowseShopCommand(_testMerchant.Id));

        // Assert
        var shopItem = browseResult.DynamicItems.First();
        shopItem.BuyPrice.Should().BeGreaterThan(shopItem.SellPrice,
            "player should buy at higher price than they sell");
    }

    private NPC CreateTestMerchant()
    {
        return new NPC
        {
            Id = $"merchant-{Guid.NewGuid()}",
            Name = "Test Merchant",
            Occupation = "GeneralMerchant",
            Gold = 5000,
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(true, TraitType.Boolean),
                ["shopType"] = new TraitValue("general", TraitType.String),
                ["baseInventorySize"] = new TraitValue(20, TraitType.Number),
                ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String)
            }
        };
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
