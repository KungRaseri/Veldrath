using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Shop.Integration;

/// <summary>
/// Integration tests for shop query API.
/// Tests: Query merchant info, check affordability, get NPCs at location.
/// </summary>
[Trait("Category", "Integration")]
public class ShopQueryAPITests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;
    private readonly SaveGame _testSaveGame;
    private readonly NPC _testMerchant;

    public ShopQueryAPITests()
    {
        var services = new ServiceCollection();

        // Mock IApocalypseTimer
        var mockTimer = new Mock<IApocalypseTimer>();
        mockTimer.Setup(t => t.GetBonusMinutes()).Returns(0);
        services.AddSingleton(mockTimer.Object);

        // Mock IGameUI
        var mockConsole = new Mock<IGameUI>();
        services.AddSingleton(mockConsole.Object);

        // Mock repository for testing
        var mockRepository = new Mock<ISaveGameRepository>();
        mockRepository.Setup(r => r.SaveGame(It.IsAny<SaveGame>()))
            .Callback<SaveGame>(s => { /* No-op for tests */ });

        // Register logging
        services.AddLogging();

        // Register MediatR with all handlers
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetMerchantInfoQuery).Assembly));

        // Register core services
        services.AddSingleton(mockRepository.Object);
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<ISaveGameService>(sp => sp.GetRequiredService<SaveGameService>());
        
        // Register ItemCatalogLoader with mocked DbContextFactory
        var dbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        services.AddSingleton(dbFactory.Object);
        services.AddSingleton<ItemDataService>();
        services.AddSingleton<ShopEconomyService>();

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _saveGameService = _serviceProvider.GetRequiredService<SaveGameService>();

        // Create test data
        _testMerchant = CreateTestMerchant();
        _testSaveGame = new SaveGame
        {
            Character = new Character
            {
                Name = "TestHero",
                Level = 5,
                Gold = 1000
            }
        };
        _testSaveGame.KnownNPCs.Add(_testMerchant);
        _saveGameService.SetCurrentSave(_testSaveGame);
    }

    [Fact]
    public async Task Should_Get_Merchant_Info_Successfully()
    {
        // Act
        var result = await _mediator.Send(new GetMerchantInfoQuery(_testMerchant.Id));

        // Assert
        result.Success.Should().BeTrue();
        result.Merchant.Should().NotBeNull();
        result.Merchant!.Name.Should().Be("Test Merchant");
        result.Merchant.ShopType.Should().Be("general");
        result.Merchant.Gold.Should().Be(5000);
    }

    [Fact]
    public async Task Should_Get_NPCs_At_Location()
    {
        // Act
        var result = await _mediator.Send(new GetNPCsAtLocationQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.NPCs.Should().NotBeNull();
        result.NPCs!.Should().HaveCount(1);
        result.NPCs![0].Name.Should().Be("Test Merchant");
        result.NPCs[0].IsMerchant.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Check_Affordability_When_Player_Has_Enough_Gold()
    {
        // Arrange
        var shopService = _serviceProvider.GetRequiredService<ShopEconomyService>();
        var inventory = shopService.GetOrCreateInventory(_testMerchant);
        
        var testItem = new Item
        {
            Id = "test-item-1",
            Name = "Test Sword",
            Price = 100
        };
        inventory.CoreItems.Add(testItem);

        // Act
        var result = await _mediator.Send(new CheckAffordabilityQuery(_testMerchant.Id, "test-item-1"));

        // Assert
        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeTrue();
        result.PlayerGold.Should().Be(1000);
        result.GoldShortfall.Should().Be(0);
        result.ItemName.Should().Be("Test Sword");
    }

    [Fact]
    public async Task Should_Check_Affordability_When_Player_Cannot_Afford()
    {
        // Arrange
        var shopService = _serviceProvider.GetRequiredService<ShopEconomyService>();
        var inventory = shopService.GetOrCreateInventory(_testMerchant);
        
        var expensiveItem = new Item
        {
            Id = "expensive-item",
            Name = "Legendary Sword",
            Price = 2000 // Will cost ~3000 with markup
        };
        inventory.CoreItems.Add(expensiveItem);

        // Act
        var result = await _mediator.Send(new CheckAffordabilityQuery(_testMerchant.Id, "expensive-item"));

        // Assert
        result.Success.Should().BeTrue();
        result.CanAfford.Should().BeFalse();
        result.GoldShortfall.Should().BeGreaterThan(0);
        result.ItemPrice.Should().BeGreaterThan(result.PlayerGold);
    }

    [Fact]
    public async Task Should_Return_Merchant_Inventory_Counts()
    {
        // Arrange
        var shopService = _serviceProvider.GetRequiredService<ShopEconomyService>();
        var inventory = shopService.GetOrCreateInventory(_testMerchant);
        
        inventory.CoreItems.Add(new Item { Id = "item1", Name = "Item 1", Price = 50 });
        inventory.CoreItems.Add(new Item { Id = "item2", Name = "Item 2", Price = 75 });
        inventory.DynamicItems.Add(new Item { Id = "item3", Name = "Item 3", Price = 100 });

        // Act
        var result = await _mediator.Send(new GetMerchantInfoQuery(_testMerchant.Id));

        // Assert
        result.Success.Should().BeTrue();
        result.Merchant!.CoreItemsCount.Should().Be(2);
        result.Merchant.DynamicItemsCount.Should().Be(1);
        result.Merchant.TotalItemsForSale.Should().Be(3);
    }

    [Fact]
    public async Task Should_Include_NPC_Relationship_Value()
    {
        // Arrange
        _testSaveGame.NPCRelationships[_testMerchant.Id] = 75;

        // Act
        var result = await _mediator.Send(new GetNPCsAtLocationQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.NPCs![0].RelationshipValue.Should().Be(75);
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
                ["shopInventoryType"] = new TraitValue("hybrid", TraitType.String)
            }
        };
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>Tests for the <see cref="GetShopItemsQuery"/> pure-read query.</summary>
[Trait("Category", "Feature")]
public class GetShopItemsQueryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;
    private readonly ShopEconomyService _shopService;
    private readonly SaveGame _testSaveGame;
    private readonly NPC _testMerchant;

    public GetShopItemsQueryTests()
    {
        var services = new ServiceCollection();

        var mockTimer = new Mock<IApocalypseTimer>();
        mockTimer.Setup(t => t.GetBonusMinutes()).Returns(0);
        services.AddSingleton(mockTimer.Object);

        var mockRepo = new Mock<ISaveGameRepository>();
        mockRepo.Setup(r => r.SaveGame(It.IsAny<SaveGame>()));
        services.AddSingleton(mockRepo.Object);

        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetShopItemsQuery).Assembly));

        var dbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        services.AddSingleton(dbFactory.Object);
        services.AddSingleton<ItemDataService>();
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<ISaveGameService>(sp => sp.GetRequiredService<SaveGameService>());
        services.AddSingleton<ShopEconomyService>();

        _serviceProvider = services.BuildServiceProvider();
        _mediator        = _serviceProvider.GetRequiredService<IMediator>();
        _saveGameService = _serviceProvider.GetRequiredService<SaveGameService>();
        _shopService     = _serviceProvider.GetRequiredService<ShopEconomyService>();

        _testMerchant = CreateTestMerchant();
        _testSaveGame = _saveGameService.CreateNewGame(
            new Character { Name = "TestHero", ClassName = "Warrior", Level = 5, Gold = 500 },
            DifficultySettings.Normal);
        _testSaveGame.KnownNPCs.Add(_testMerchant);
        _saveGameService.SetCurrentSave(_testSaveGame);
    }

    [Fact]
    public async Task GetShopItems_Returns_Success_With_Items()
    {
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        inventory.CoreItems.Add(new Item { Id = "sword-1", Name = "Iron Sword", Price = 80, Type = ItemType.Weapon });
        inventory.DynamicItems.Add(new Item { Id = "pot-1", Name = "Health Potion", Price = 30, Type = ItemType.Consumable });

        var result = await _mediator.Send(new GetShopItemsQuery(_testMerchant.Id));

        result.Success.Should().BeTrue();
        result.MerchantName.Should().Be(_testMerchant.Name);
        result.CoreItems.Should().ContainSingle(i => i.Item.Name == "Iron Sword");
        result.DynamicItems.Should().ContainSingle(i => i.Item.Name == "Health Potion");
        result.TotalItemCount.Should().Be(2);
    }

    [Fact]
    public async Task GetShopItems_ReturnsFailure_ForNonMerchant()
    {
        var npc = new NPC
        {
            Id = "farmer-1",
            Name = "Old Farmer",
            Traits = new Dictionary<string, TraitValue>
            {
                ["isMerchant"] = new TraitValue(false, TraitType.Boolean)
            }
        };
        _testSaveGame.KnownNPCs.Add(npc);

        var result = await _mediator.Send(new GetShopItemsQuery("farmer-1"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }

    [Fact]
    public async Task GetShopItems_ReturnsFailure_ForUnknownMerchantId()
    {
        var result = await _mediator.Send(new GetShopItemsQuery("unknown-id-xyz"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task GetShopItems_BuyPrice_GreaterThan_SellPrice()
    {
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        inventory.CoreItems.Add(new Item { Id = "axe-1", Name = "Battle Axe", Price = 150, Type = ItemType.Weapon });

        var result = await _mediator.Send(new GetShopItemsQuery(_testMerchant.Id));

        result.Success.Should().BeTrue();
        var item = result.CoreItems.Single(i => i.Item.Name == "Battle Axe");
        item.BuyPrice.Should().BeGreaterThan(item.SellPrice,
            "players buy at markup and sell at discount");
    }

    [Fact]
    public async Task GetShopItems_MatchesBrowseShopCommand_ItemLists()
    {
        // Both query and command should return identical item lists
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        inventory.CoreItems.Add(new Item { Id = "shield-1", Name = "Wooden Shield", Price = 60, Type = ItemType.Shield });

        var queryResult   = await _mediator.Send(new GetShopItemsQuery(_testMerchant.Id));
        var commandResult = await _mediator.Send(new BrowseShopCommand(_testMerchant.Id));

        queryResult.Success.Should().BeTrue();
        commandResult.Success.Should().BeTrue();
        queryResult.CoreItems.Select(i => i.Item.Id)
            .Should().BeEquivalentTo(commandResult.CoreItems.Select(i => i.Item.Id));
    }

    [Fact]
    public async Task GetShopItems_IsUnlimited_TrueForCoreItems_FalseForDynamic()
    {
        var inventory = _shopService.GetOrCreateInventory(_testMerchant);
        inventory.CoreItems.Add(new Item   { Id = "core-1",    Name = "Core Item",    Price = 50 });
        inventory.DynamicItems.Add(new Item { Id = "dynamic-1", Name = "Dynamic Item", Price = 50 });

        var result = await _mediator.Send(new GetShopItemsQuery(_testMerchant.Id));

        result.CoreItems.Should().OnlyContain(i => i.IsUnlimited);
        result.DynamicItems.Should().OnlyContain(i => !i.IsUnlimited);
    }

    private static NPC CreateTestMerchant() => new()
    {
        Id   = $"merchant-{Guid.NewGuid()}",
        Name = "Item Shop Owner",
        Occupation = "GeneralMerchant",
        Gold = 3000,
        Traits = new Dictionary<string, TraitValue>
        {
            ["isMerchant"]        = new TraitValue(true,      TraitType.Boolean),
            ["shopType"]          = new TraitValue("general", TraitType.String),
            ["shopInventoryType"] = new TraitValue("hybrid",  TraitType.String)
        }
    };

    public void Dispose() => _serviceProvider?.Dispose();
}
