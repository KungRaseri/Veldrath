using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Behaviors;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Core.Services.Budget;
// Alias to disambiguate from RealmEngine.Core.Services.HarvestingConfig (in HarvestingConfigService.cs)
using HarvestingConfigData = RealmEngine.Core.Services.Harvesting.HarvestingConfig;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.CharacterCreation.Services;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Core.Features.Difficulty.Services;
using RealmEngine.Core.Features.Death;
using RealmEngine.Core.Features.Death.Services;
using RealmEngine.Core.Features.Victory.Services;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Core.Features.Achievements.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core;

/// <summary>
/// Extension methods for registering RealmEngine services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all RealmEngine Core services (generators, validators, etc.).
    /// Call this after registering Data services and MediatR.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurePersistence">
    /// Optional persistence configuration. Defaults to in-memory when null.
    /// Use <c>p =&gt; p.UseNpgsql(connStr)</c> for PostgreSQL persistence or
    /// <c>p =&gt; p.UseExternal()</c> when the host (e.g. RealmUnbound.Server) registers its own
    /// repository implementations.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineCore(this IServiceCollection services,
        Action<PersistenceOptions>? configurePersistence = null)
    {
        // Apply persistence options before registering any services.
        var persistenceOptions = new PersistenceOptions();
        configurePersistence?.Invoke(persistenceOptions);

        // Register category discovery service (singleton for caching)
        services.AddSingleton<CategoryDiscoveryService>();
        
        // Register generators (all available)
        services.AddScoped<ItemGenerator>();
        services.AddScoped<EnemyGenerator>();
        services.AddScoped<NpcGenerator>();
        services.AddScoped<AbilityGenerator>();
        services.AddScoped<CharacterClassGenerator>();
        services.AddScoped<LocationGenerator>();
        services.AddScoped<QuestGenerator>();
        services.AddScoped<OrganizationGenerator>();
        services.AddScoped<DialogueGenerator>();
        services.AddScoped<GemGenerator>();
        services.AddScoped<EssenceGenerator>();
        services.AddScoped<EnchantmentGenerator>();
        services.AddScoped<SocketGenerator>();
        services.AddScoped<RuneGenerator>();
        services.AddScoped<OrbGenerator>();
        services.AddScoped<CrystalGenerator>();
        
        // Register game logic services
        services.AddScoped<LootTableService>();
        services.AddScoped<CharacterGrowthService>();
        services.AddScoped<InMemoryInventoryService>();
        services.AddScoped<ShopEconomyService>();
        services.AddScoped<LevelUpService>();
        services.AddScoped<GameStateService>();
        
        // Register harvesting services
        // HarvestingConfigData alias = RealmEngine.Core.Services.Harvesting.HarvestingConfig (all defaults)
        services.AddSingleton(new HarvestingConfigData());
        services.AddScoped<HarvestingConfigService>();
        services.AddScoped<HarvestCalculatorService>();
        services.AddScoped<CriticalHarvestService>();
        services.AddScoped<ToolValidationService>();

        // Register budget configuration factory and services
        services.AddSingleton<BudgetConfigFactory>();
        services.AddSingleton<BudgetConfig>(sp => 
        {
            var factory = sp.GetRequiredService<BudgetConfigFactory>();
            return factory.GetBudgetConfig();
        });
        services.AddSingleton<MaterialPools>(sp =>
            sp.GetRequiredService<BudgetConfigFactory>().GetMaterialPools());
        services.AddSingleton<EnemyTypes>(sp =>
            sp.GetRequiredService<BudgetConfigFactory>().GetEnemyTypes());
        services.AddSingleton<MaterialFilterConfig>(sp =>
            sp.GetRequiredService<BudgetConfigFactory>().GetMaterialFilters());
        services.AddSingleton<BudgetCalculator>();
        services.AddScoped<BudgetItemGenerationService>();
        services.AddScoped<MaterialPoolService>();
        services.AddScoped<BudgetHelperService>();
        
        // Register catalog loaders
        services.AddSingleton<RecipeCatalogLoader>();
        services.AddSingleton<ItemCatalogLoader>();
        
        // Register config/utility services
        services.AddScoped<RarityConfigService>();
        services.AddScoped<DescriptiveTextService>();
        services.AddScoped<NodeSpawnerService>();
        services.AddScoped<ReactiveAbilityService>();
        services.AddScoped<PassiveBonusCalculator>();
        
        // Register catalog services (singletons for shared catalogs)
        // NOTE: These catalog services require InitializeAsync() to be called after DI container is built
        // Example: await serviceProvider.GetRequiredService<AbilityCatalogService>().InitializeAsync();
        services.AddSingleton<AbilityCatalogService>();
        services.AddSingleton<SpellCatalogService>();  // Changed from Scoped to Singleton for caching
        services.AddSingleton<SkillCatalogService>();  // Changed from Scoped to Singleton for caching
        
        // Register interfaces to implementations
        services.AddScoped<IApocalypseTimer, ApocalypseTimer>();
        services.AddScoped<ISaveGameService, SaveGameService>();
        services.AddScoped<IInventoryService, InMemoryInventoryService>();
        services.AddScoped<IPassiveBonusCalculator, PassiveBonusCalculator>();
        
        // Register repositories (interfaces defined in Shared, implementations in Data)
        // Mode is controlled by the persistence options passed to AddRealmEngineCore().
        if (persistenceOptions.IsNpgsql)
        {
            services.AddDbContext<GameDbContext>(o =>
                o.UseNpgsql(persistenceOptions.ConnectionString));
            services.AddDbContext<ContentDbContext>(o =>
                o.UseNpgsql(persistenceOptions.ConnectionString));
            services.AddScoped<ISaveGameRepository, EfCoreSaveGameRepository>();
            services.AddScoped<IHallOfFameRepository, EfCoreHallOfFameRepository>();
            services.AddScoped<ICharacterClassRepository, EfCoreCharacterClassRepository>();
            services.AddScoped<IBackgroundRepository, EfCoreBackgroundRepository>();
        }
        else if (!persistenceOptions.IsExternal)
        {
            // Default: in-memory (no file I/O, ideal for tests)
            services.AddScoped<ISaveGameRepository, InMemorySaveGameRepository>();
            services.AddScoped<IHallOfFameRepository, InMemoryHallOfFameRepository>();
            services.AddScoped<ICharacterClassRepository, InMemoryCharacterClassRepository>();
            services.AddScoped<IBackgroundRepository, InMemoryBackgroundRepository>();
        }
        // External: host is responsible for registering ALL repo interfaces.
        services.AddScoped<INodeRepository, InMemoryNodeRepository>();
        services.AddScoped<IEquipmentSetRepository, EquipmentSetRepository>();
        
        // Register feature services (concrete implementations)
        services.AddScoped<SaveGameService>();
        services.AddScoped<LoadGameService>();
        services.AddScoped<CombatService>();
        services.AddScoped<CraftingService>();
        services.AddScoped<ExplorationService>();
        services.AddScoped<CharacterInitializationService>();
        services.AddScoped<QuestService>();
        services.AddScoped<QuestProgressService>();
        services.AddScoped<QuestRewardService>();
        services.AddScoped<QuestInitializationService>();
        services.AddScoped<MainQuestService>();
        services.AddScoped<ReputationService>();
        services.AddScoped<FactionDataService>();
        services.AddScoped<PartyService>();
        services.AddScoped<PartyAIService>();
        services.AddScoped<DifficultyService>();
        services.AddScoped<DeathService>();
        services.AddScoped<RespawnService>();
        services.AddScoped<VictoryService>();
        services.AddScoped<NewGamePlusService>();
        services.AddScoped<SpellCastingService>();
        services.AddScoped<SkillProgressionService>();
        services.AddScoped<GameplayService>();
        services.AddScoped<SocketService>();
        
        // Register combat AI services
        services.AddScoped<EnemySpellCastingService>();
        services.AddScoped<EnemyAbilityAIService>();
        
        // Register additional feature services
        services.AddScoped<DungeonGeneratorService>();
        services.AddScoped<AchievementService>();
        
        return services;
    }
    
    /// <summary>
    /// Registers MediatR with all handlers, pipeline behaviors, and FluentValidation validators.
    /// Call this after registering Data and Core services so all handler dependencies are available.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineMediatR(this IServiceCollection services)
    {
        // Register MediatR and scan for all handlers in the Core assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            
            // Register pipeline behaviors in order (validation -> logging -> performance)
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });
        
        // Register all FluentValidation validators from the Core assembly
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        
        return services;
    }
    
    /// <summary>
    /// Registers all RealmEngine Data services (cache, repositories, reference resolver).
    /// Call this before registering Core services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="jsonDataPath">The path to the JSON data folder (e.g., "Data/Json").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineData(this IServiceCollection services, string jsonDataPath)
    {
        // Register IMemoryCache if not already registered
        services.AddMemoryCache();
        
        // Register GameDataCache as singleton with path
        services.AddSingleton<GameDataCache>(sp => 
            new GameDataCache(jsonDataPath, sp.GetService<IMemoryCache>()));
        
        // Register data services
        services.AddScoped<ReferenceResolverService>();
        services.AddScoped<ItemGenerationRulesService>();
        services.AddScoped<ExperienceConfigService>();
        services.AddScoped<ResourceNodeLoaderService>();
        
        // Note: Repositories are registered in AddRealmEngineCore since they're used by Core services
        // and the interface/implementation split requires them to be in Core's DI scope
        
        return services;
    }
}
