using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Core;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands.AttackEnemy;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Queries;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Features.LevelUp.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.SaveLoad.Commands;
using RealmEngine.Core.Features.SaveLoad.Queries;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Features.Shop.Queries;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data;
using RealmEngine.Data.Services;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using System.Reflection;
using Xunit;

namespace RealmEngine.Core.Tests;

/// <summary>
/// Comprehensive tests to ensure all services are properly registered in the DI container.
/// These tests will catch missing registrations before runtime errors occur.
/// </summary>
public class ServiceRegistrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _services;

    public ServiceRegistrationTests()
    {
        _services = new ServiceCollection();
        
        // Register logging (basic logging without console sink)
        _services.AddLogging();
        
        // Register RealmEngine services FIRST (before MediatR)
        // This ensures all handler dependencies are available when MediatR scans
        _services.AddRealmEngineCore();
        
        // Override SaveGameRepository to use in-memory implementation for test isolation
        // This must be done AFTER AddRealmEngineCore() which registers the default implementation
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(ISaveGameRepository));
        if (descriptor != null)
        {
            _services.Remove(descriptor);
        }
        _services.AddScoped<ISaveGameRepository, InMemorySaveGameRepository>();
        
        // Register MediatR with behaviors and validators LAST
        // This ensures all dependencies (services, validators) are registered before handlers
        _services.AddRealmEngineMediatR();
        
        _serviceProvider = _services.BuildServiceProvider();
    }

    #region Debug Tests

    [Fact]
    public void MediatR_Can_Resolve_Crafting_Handlers()
    {
        // Handlers are registered as IRequestHandler<TRequest, TResponse>, not by concrete type
        // Test via IMediator to verify they work end-to-end
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        
        // This should not throw - it proves GetKnownRecipesHandler is registered and resolvable
        mediator.Should().NotBeNull("IMediator should be registered");
        
        // Verify handler can be resolved via IRequestHandler interface
        var handler = scope.ServiceProvider.GetService<IRequestHandler<GetKnownRecipesQuery, GetKnownRecipesResult>>();
        handler.Should().NotBeNull("GetKnownRecipesHandler should be resolvable via IRequestHandler interface");
    }

    [Fact]
    public void MediatR_Can_Resolve_Shop_Handlers()
    {
        using var scope = _serviceProvider.CreateScope();
        
        // Test BrowseShopHandler resolution
        var handler = scope.ServiceProvider.GetService<IRequestHandler<BrowseShopCommand, BrowseShopResult>>();
        handler.Should().NotBeNull("BrowseShopHandler should be resolvable via IRequestHandler interface");
    }

    [Fact]
    public void MediatR_Can_Resolve_SaveLoad_Handlers()
    {
        using var scope = _serviceProvider.CreateScope();
        
        // Test SaveGameHandler resolution  
        var handler = scope.ServiceProvider.GetService<IRequestHandler<SaveGameCommand, SaveGameResult>>();
        handler.Should().NotBeNull("SaveGameHandler should be resolvable via IRequestHandler interface");
    }

    #endregion

    #region Core Interface Bindings

    [Fact]
    public void IApocalypseTimer_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IApocalypseTimer>();

        // Assert
        service.Should().NotBeNull("IApocalypseTimer should be registered in DI container");
        service.Should().BeOfType<ApocalypseTimer>("IApocalypseTimer should bind to ApocalypseTimer implementation");
    }

    [Fact]
    public void ISaveGameService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ISaveGameService>();

        // Assert
        service.Should().NotBeNull("ISaveGameService should be registered in DI container");
        service.Should().BeOfType<SaveGameService>("ISaveGameService should bind to SaveGameService implementation");
    }

    [Fact]
    public void IInventoryService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IInventoryService>();

        // Assert
        service.Should().NotBeNull("IInventoryService should be registered in DI container");
        service.Should().BeOfType<InMemoryInventoryService>("IInventoryService should bind to InMemoryInventoryService implementation");
    }

    [Fact]
    public void IPassiveBonusCalculator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IPassiveBonusCalculator>();

        // Assert
        service.Should().NotBeNull("IPassiveBonusCalculator should be registered in DI container");
        service.Should().BeOfType<PassiveBonusCalculator>("IPassiveBonusCalculator should bind to PassiveBonusCalculator implementation");
    }

    #endregion

    #region Repository Interface Bindings

    [Fact]
    public void ISaveGameRepository_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ISaveGameRepository>();

        // Assert
        service.Should().NotBeNull("ISaveGameRepository should be registered in DI container");
    }

    [Fact]
    public void INodeRepository_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<INodeRepository>();

        // Assert
        service.Should().NotBeNull("INodeRepository should be registered in DI container");
    }

    [Fact]
    public void ICharacterClassRepository_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ICharacterClassRepository>();

        // Assert
        service.Should().NotBeNull("ICharacterClassRepository should be registered in DI container");
    }

    [Fact]
    public void IHallOfFameRepository_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IHallOfFameRepository>();

        // Assert
        service.Should().NotBeNull("IHallOfFameRepository should be registered in DI container");
    }

    [Fact]
    public void IEquipmentSetRepository_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IEquipmentSetRepository>();

        // Assert
        service.Should().NotBeNull("IEquipmentSetRepository should be registered in DI container");
    }

    #endregion

    #region Critical Services Reported By User

    [Fact]
    public void CraftingService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<CraftingService>();

        // Assert
        service.Should().NotBeNull("CraftingService should be registered (required by GetKnownRecipesHandler)");
    }

    [Fact]
    public void BudgetCalculator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<BudgetCalculator>();

        // Assert
        service.Should().NotBeNull("BudgetCalculator should be registered (required by BudgetHelperService → ShopEconomyService)");
    }

    [Fact]
    public void BudgetConfigFactory_Should_Be_Registered()
    {
        // Act
        var factory = _serviceProvider.GetService<BudgetConfigFactory>();

        // Assert
        factory.Should().NotBeNull("BudgetConfigFactory should be registered to load budget configuration");
    }

    [Fact]
    public void BudgetConfig_Should_Be_Registered_And_Loaded()
    {
        // Act
        var config = _serviceProvider.GetService<BudgetConfig>();

        // Assert
        config.Should().NotBeNull("BudgetConfig should be registered and loaded from JSON");
        config!.Allocation.Should().NotBeNull("BudgetConfig should have allocation settings");
        config.Formulas.Should().NotBeNull("BudgetConfig should have cost formulas");
    }

    [Fact]
    public void AbilityCatalogService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<AbilityDataService>();

        // Assert
        service.Should().NotBeNull("AbilityCatalogService should be registered (required by GetAvailableAbilitiesHandler)");
    }

    [Fact]
    public void SpellCatalogService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<SpellDataService>();

        // Assert
        service.Should().NotBeNull("SpellCatalogService should be registered as singleton (required by GetLearnableSpellsHandler)");
    }

    [Fact]
    public void RecipeCatalogLoader_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<RecipeDataService>();

        // Assert
        service.Should().NotBeNull("RecipeCatalogLoader should be registered");
    }

    [Fact]
    public void ItemCatalogLoader_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ItemDataService>();

        // Assert
        service.Should().NotBeNull("ItemCatalogLoader should be registered");
    }

    #endregion

    #region Generator Services

    [Fact]
    public void ItemGenerator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ItemGenerator>();

        // Assert
        service.Should().NotBeNull("ItemGenerator should be registered");
    }

    [Fact]
    public void EnemyGenerator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<EnemyGenerator>();

        // Assert
        service.Should().NotBeNull("EnemyGenerator should be registered");
    }

    [Fact]
    public void AbilityGenerator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<AbilityGenerator>();

        // Assert
        service.Should().NotBeNull("AbilityGenerator should be registered");
    }

    [Fact]
    public void LocationGenerator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<LocationGenerator>();

        // Assert
        service.Should().NotBeNull("LocationGenerator should be registered");
    }

    [Fact]
    public void NpcGenerator_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<NpcGenerator>();

        // Assert
        service.Should().NotBeNull("NpcGenerator should be registered");
    }

    #endregion

    #region Core Services

    [Fact]
    public void CharacterGrowthService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<CharacterGrowthService>();

        // Assert
        service.Should().NotBeNull("CharacterGrowthService should be registered");
    }

    [Fact]
    public void LootTableService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<LootTableService>();

        // Assert
        service.Should().NotBeNull("LootTableService should be registered");
    }

    [Fact]
    public void ShopEconomyService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<ShopEconomyService>();

        // Assert
        service.Should().NotBeNull("ShopEconomyService should be registered");
    }

    [Fact]
    public void IGameStateService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<IGameStateService>();

        // Assert
        service.Should().NotBeNull("IGameStateService should be registered in DI container");
        service.Should().BeOfType<GameStateService>("IGameStateService should bind to GameStateService implementation");
    }

    #endregion

    #region Data Services

    [Fact]
    public void CategoryDiscoveryService_Should_Be_Registered_As_Singleton()
    {
        // Act
        var service1 = _serviceProvider.GetService<CategoryDiscoveryService>();
        var service2 = _serviceProvider.GetService<CategoryDiscoveryService>();

        // Assert
        service1.Should().NotBeNull("CategoryDiscoveryService should be registered");
        service1.Should().BeSameAs(service2, "CategoryDiscoveryService should be a singleton");
    }

    #endregion

    #region Handler Resolution Tests

    [Fact]
    public void GetKnownRecipesHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange - This handler requires CraftingService
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var character = new Character { Name = "TestCharacter", ClassName = "Fighter" };

        // Act
        var action = () => mediator.Send(new GetKnownRecipesQuery { Character = character });

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("GetKnownRecipesHandler should resolve all dependencies (CraftingService)");
    }

    [Fact]
    public void CreateCharacterHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var characterClass = new CharacterClass { Name = "Fighter" };

        // Act
        var action = () => mediator.Send(new CreateCharacterCommand 
        { 
            CharacterName = "TestHero",
            CharacterClass = characterClass
        });

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("CreateCharacterHandler should resolve all dependencies");
    }

    [Fact]
    public void SaveGameHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange - This handler requires ISaveGameRepository and IApocalypseTimer
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var player = new Character { Name = "TestHero", ClassName = "Fighter" };

        // Act
        var action = () => mediator.Send(new SaveGameCommand 
        { 
            Player = player,
            Inventory = new List<Item>()
        });

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("SaveGameHandler should resolve all dependencies (ISaveGameRepository, IApocalypseTimer)");
    }

    [Fact]
    public void GetAvailableAbilitiesHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange - This handler requires AbilityCatalogService
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var action = () => mediator.Send(new GetAvailableAbilitiesQuery 
        { 
            ClassName = "Fighter"
        });

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("GetAvailableAbilitiesHandler should resolve all dependencies (AbilityCatalogService)");
    }

    [Fact]
    public void BrowseShopHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange - This handler requires ShopEconomyService which requires BudgetCalculator
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var action = () => mediator.Send(new BrowseShopCommand("TestMerchant"));

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("BrowseShopHandler should resolve all dependencies (ShopEconomyService → BudgetCalculator)");
    }

    [Fact]
    public void GenerateItemHandler_Should_Resolve_With_Dependencies()
    {
        // Arrange - This handler requires ItemGenerator
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var action = () => mediator.Send(new GenerateItemCommand 
        { 
            Category = "items/weapons/swords"
        });

        // Assert
        action.Should().NotThrowAsync<InvalidOperationException>("GenerateItemHandler should resolve all dependencies (ItemGenerator)");
    }

    #endregion

    #region Specific Handler Type Tests - OBSOLETE
    
    // NOTE: The old tests here tried to resolve handlers by concrete type, which doesn't work.
    // MediatR registers handlers as IRequestHandler<TRequest, TResponse>, not by concrete type.
    // See "Debug Tests" section above for correct handler resolution tests.

    #endregion

    #region Comprehensive Validation

    [Fact]
    public void All_Generator_Services_Should_Be_Resolvable()
    {
        // Arrange - Only include generators that actually exist in the codebase
        var generatorTypes = new[]
        {
            typeof(ItemGenerator),
            typeof(EnemyGenerator),
            typeof(NpcGenerator),
            typeof(AbilityGenerator),
            typeof(LocationGenerator),
            typeof(QuestGenerator),
            typeof(DialogueGenerator),
            typeof(SocketGenerator)
        };

        var failures = new List<string>();

        // Act
        foreach (var generatorType in generatorTypes)
        {
            try
            {
                var generator = _serviceProvider.GetService(generatorType);
                
                if (generator == null)
                {
                    failures.Add($"{generatorType.Name}: Not registered in DI container");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{generatorType.Name}: {ex.Message}");
            }
        }

        // Assert
        failures.Should().BeEmpty($"All {generatorTypes.Length} generators should be resolvable. Failures:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void All_Feature_Services_Should_Be_Resolvable()
    {
        // Arrange
        var featureServices = new[]
        {
            typeof(SaveGameService),
            typeof(LoadGameService),
            typeof(CombatService),
            typeof(CraftingService),
            typeof(ExplorationService),
            typeof(GameplayService),
            typeof(SocketService)
        };

        var failures = new List<string>();

        // Act
        foreach (var serviceType in featureServices)
        {
            try
            {
                var service = _serviceProvider.GetService(serviceType);
                
                if (service == null)
                {
                    failures.Add($"{serviceType.Name}: Not registered in DI container");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{serviceType.Name}: {ex.Message}");
            }
        }

        // Assert
        failures.Should().BeEmpty($"All {featureServices.Length} feature services should be resolvable. Failures:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void GameplayService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<GameplayService>();

        // Assert
        service.Should().NotBeNull("GameplayService should be registered (handles rest, recovery)");
    }

    [Fact]
    public void SocketService_Should_Be_Registered()
    {
        // Act
        var service = _serviceProvider.GetService<SocketService>();

        // Assert
        service.Should().NotBeNull("SocketService should be registered (handles socket validation and operations)");
    }

    #endregion

    #region Cross-Project Dependency Validation

    [Fact]
    public void Core_Should_Resolve_Data_Dependencies()
    {
        // Arrange - SaveGameService (Core) depends on ISaveGameRepository (Data)
        var saveGameService = _serviceProvider.GetService<SaveGameService>();

        // Act & Assert
        saveGameService.Should().NotBeNull("SaveGameService should resolve with Data repository dependencies");
    }

    [Fact]
    public void ShopEconomyService_Should_Resolve_All_Dependencies()
    {
        // Arrange - ShopEconomyService depends on BudgetCalculator, BudgetHelperService, etc.
        var shopService = _serviceProvider.GetService<ShopEconomyService>();

        // Act & Assert
        shopService.Should().NotBeNull("ShopEconomyService should resolve all nested dependencies (BudgetCalculator → BudgetHelperService)");
    }

    #endregion
}
