using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Services;
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
        
        // Register GameDataCache with required dataRootPath
        var dataPath = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\..\\RealmEngine.Data\\Data\\Json");
        services.AddSingleton(new RealmEngine.Data.Services.GameDataCache(dataPath));
        
        services.AddSingleton<ItemCatalogLoader>();
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
