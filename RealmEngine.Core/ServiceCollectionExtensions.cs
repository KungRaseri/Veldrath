using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
using RealmEngine.Core.Features.Enchanting;
using RealmEngine.Core.Features.Upgrading;
using RealmEngine.Core.Features.Achievements.Services;
using RealmEngine.Core.Repositories;
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
    /// <c>p =&gt; p.UseExternal()</c> when the host (e.g. Veldrath.Server) registers its own
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
        services.AddScoped<PowerGenerator>();
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
        services.AddScoped<ShopEconomyService>();
        services.AddScoped<LevelUpService>();
        services.AddScoped<IGameStateService, GameStateService>();
        
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
        services.AddSingleton<SocketConfig>(sp =>
            sp.GetRequiredService<BudgetConfigFactory>().GetSocketConfig());
        services.AddSingleton<BudgetCalculator>();
        services.AddScoped<BudgetItemGenerationService>();
        services.AddScoped<MaterialPoolService>();
        services.AddScoped<BudgetHelperService>();
        
        // Register catalog loaders
        services.AddScoped<RecipeDataService>();
        services.AddScoped<ItemDataService>();
        
        // Register config/utility services
        services.AddScoped<RarityConfigService>();
        services.AddScoped<DescriptiveTextService>();
        services.AddScoped<NodeSpawnerService>();
        services.AddScoped<ReactivePowerService>();
        services.AddScoped<PassiveBonusCalculator>();
        
        // Register catalog services (singletons for shared catalogs)
        // NOTE: These catalog services require InitializeAsync() to be called after DI container is built
        // Example: await serviceProvider.GetRequiredService<PowerDataService>().InitializeAsync();
        // Factory lambdas are used to explicitly select the IServiceScopeFactory constructor,
        // avoiding ambiguity with the secondary test constructor that takes IXxxRepository.
        services.AddSingleton(sp => new PowerDataService(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetService<ILogger<PowerDataService>>()));
        services.AddSingleton(sp => new SkillDataService(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetService<ILogger<SkillDataService>>()));
        services.AddSingleton(sp => new NamePatternService(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetService<ILogger<NamePatternService>>()));
        
        // Register interfaces to implementations
        services.AddScoped<IApocalypseTimer, ApocalypseTimer>();
        services.AddScoped<ISaveGameService, SaveGameService>();
        services.AddScoped<ICombatSettings>(sp => sp.GetRequiredService<ISaveGameService>().GetDifficultySettings());
        services.AddScoped<IPassiveBonusCalculator, PassiveBonusCalculator>();
        
        // Register repositories (interfaces defined in Shared, implementations in Data)
        // Mode is controlled by the persistence options passed to AddRealmEngineCore().
        if (persistenceOptions.IsNpgsql)
        {
            services.AddDbContext<GameDbContext>(o =>
                o.UseNpgsql(persistenceOptions.ConnectionString));
            services.AddDbContextFactory<ContentDbContext>(o =>
                o.UseNpgsql(persistenceOptions.ConnectionString));
            services.AddScoped<ContentDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<ContentDbContext>>().CreateDbContext());
            services.AddSingleton<GameConfigService, DbGameConfigService>();
            services.AddScoped<ISaveGameRepository, EfCoreSaveGameRepository>();
            services.AddScoped<IHallOfFameRepository, EfCoreHallOfFameRepository>();
            services.AddScoped<ICharacterClassRepository, EfCoreCharacterClassRepository>();
            services.AddScoped<IBackgroundRepository, EfCoreBackgroundRepository>();
            services.AddScoped<IPowerRepository, EfCorePowerRepository>();
            services.AddScoped<IEnemyRepository, EfCoreEnemyRepository>();
            services.AddScoped<INpcRepository, EfCoreNpcRepository>();
            services.AddScoped<IQuestRepository, EfCoreQuestRepository>();
            services.AddScoped<IRecipeRepository, EfCoreRecipeRepository>();
            services.AddScoped<ILootTableRepository, EfCoreLootTableRepository>();
            services.AddScoped<ISkillRepository, EfCoreSkillRepository>();
            services.AddScoped<INamePatternRepository, EfCoreNamePatternRepository>();
            services.AddScoped<IMaterialRepository, EfCoreMaterialRepository>();
            services.AddScoped<IEquipmentSetRepository, EfCoreEquipmentSetRepository>();
            services.AddScoped<IInventoryService, EfCoreInventoryService>();
            services.AddScoped<IItemRepository, EfCoreItemRepository>();
            services.AddScoped<IEnchantmentRepository, EfCoreEnchantmentRepository>();
            services.AddScoped<IOrganizationRepository, EfCoreOrganizationRepository>();
            services.AddScoped<IZoneLocationRepository, EfCoreZoneLocationRepository>();
            services.AddSingleton<ITileMapRepository>(sp => new CompositeITileMapRepository(
                Path.Combine(AppContext.BaseDirectory, "GameAssets", "tilemaps", "maps"),
                sp.GetRequiredService<ILogger<CompositeITileMapRepository>>()));
            services.AddScoped<IDialogueRepository, EfCoreDialogueRepository>();
            services.AddScoped<IActorInstanceRepository, EfCoreActorInstanceRepository>();
            services.AddScoped<IMaterialPropertyRepository, EfCoreMaterialPropertyRepository>();
            services.AddScoped<ITraitDefinitionRepository, EfCoreTraitDefinitionRepository>();
            services.AddScoped<ISpeciesRepository, EfCoreSpeciesRepository>();
        }
        else if (!persistenceOptions.IsExternal)
        {
            // Default: in-memory (no file I/O, ideal for tests)
            services.AddDbContextFactory<ContentDbContext>(o =>
                o.UseInMemoryDatabase("RealmEngineContent"));
            services.AddScoped<ContentDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<ContentDbContext>>().CreateDbContext());
            services.AddSingleton<GameConfigService, NullGameConfigService>();
            services.AddScoped<ISaveGameRepository, InMemorySaveGameRepository>();
            services.AddScoped<IHallOfFameRepository, InMemoryHallOfFameRepository>();
            services.AddScoped<ICharacterClassRepository, InMemoryCharacterClassRepository>();
            services.AddScoped<IBackgroundRepository, InMemoryBackgroundRepository>();
            services.AddScoped<IPowerRepository, InMemoryPowerRepository>();
            services.AddScoped<IEnemyRepository, InMemoryEnemyRepository>();
            services.AddScoped<INpcRepository, InMemoryNpcRepository>();
            services.AddScoped<IQuestRepository, InMemoryQuestRepository>();
            services.AddScoped<IRecipeRepository, InMemoryRecipeRepository>();
            services.AddScoped<ILootTableRepository, InMemoryLootTableRepository>();
            services.AddScoped<ISkillRepository, InMemorySkillRepository>();
            services.AddScoped<INamePatternRepository, InMemoryNamePatternRepository>();
            services.AddScoped<IMaterialRepository, InMemoryMaterialRepository>();
            services.AddScoped<IEquipmentSetRepository, InMemoryEquipmentSetRepository>();
            services.AddScoped<IInventoryService, InMemoryInventoryService>();
            services.AddScoped<IItemRepository, InMemoryItemRepository>();
            services.AddScoped<IEnchantmentRepository, InMemoryEnchantmentRepository>();
            services.AddScoped<IOrganizationRepository, InMemoryOrganizationRepository>();
            services.AddScoped<IZoneLocationRepository, InMemoryZoneLocationRepository>();
            services.AddSingleton<ITileMapRepository, InMemoryTileMapRepository>();
            services.AddScoped<IDialogueRepository, InMemoryDialogueRepository>();
            services.AddScoped<IActorInstanceRepository, InMemoryActorInstanceRepository>();
            services.AddScoped<IMaterialPropertyRepository, InMemoryMaterialPropertyRepository>();
            services.AddScoped<ITraitDefinitionRepository, InMemoryTraitDefinitionRepository>();
            services.AddScoped<ISpeciesRepository, InMemorySpeciesRepository>();
        }
        // External: host is responsible for registering ALL repo interfaces.
        services.AddSingleton<ISaveGameContext, SaveGameContext>();
        services.AddScoped<INodeRepository, InMemoryNodeRepository>();
        
        // Register feature services (concrete implementations)
        services.AddScoped<SaveGameService>();
        services.AddScoped<LoadGameService>();
        // CombatService has two public constructors (ICombatSettings for server, ISaveGameService for single-player).
        // Use an explicit factory to select the single-player constructor and avoid DI ambiguity.
        // The server registers its own binding after calling AddRealmEngineCore().
        services.AddScoped<CombatService>(sp => new CombatService(
            sp.GetRequiredService<ISaveGameService>(),
            sp.GetRequiredService<IMediator>(),
            sp.GetRequiredService<PowerDataService>(),
            sp.GetRequiredService<ILogger<CombatService>>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetService<ItemGenerator>()));
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
        services.AddScoped<EnchantingService>();
        services.AddScoped<UpgradeService>();
        
        // Register combat AI services
        services.AddScoped<EnemySpellCastingService>();
        services.AddScoped<EnemyPowerAIService>();
        
        // Register additional feature services
        services.AddScoped<DungeonGeneratorService>();
        services.AddScoped<AchievementService>();
        
        // NOTE: Catalog initialization is NOT registered automatically because the startup
        // order varies by host type:
        //   • Generic Host consumers (ASP.NET Core, Blazor, Avalonia with IHost) —
        //     add services.AddHostedService<CatalogInitializationService>() AFTER
        //     AddRealmEngineCore(), so the host starts it after EF schema is ready.
        //   • Non-hosted consumers (console apps, test fixtures) —
        //     call await services.InitializeCatalogsAsync() on the built IServiceProvider.
        
        return services;
    }

    /// <summary>
    /// Initializes the power and skill catalog singletons.
    /// Call this on the built <see cref="IServiceProvider"/> when not using the .NET Generic Host
    /// (e.g. standalone console apps or test hosts that bypass <see cref="IHostedService"/> startup).
    /// Generic Host consumers (ASP.NET Core, Blazor, Avalonia with IHost) do not need to call this —
    /// <see cref="CatalogInitializationService"/> handles it automatically.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task InitializeCatalogsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<PowerDataService>().InitializeAsync();
        await services.GetRequiredService<SkillDataService>().InitializeAsync();
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
    /// Registers only the MediatR pipeline behaviors (Validation → Logging → Performance) and
    /// all FluentValidation validators, without scanning for any request handlers.
    /// Use this as the foundation when composing a custom handler set via the granular
    /// <c>Add*Handlers()</c> methods rather than registering the full assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineMediatRPipeline(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            // MediatR 12 requires at least one assembly scan call to register its infrastructure
            // (IMediator, ISender, IPublisher). TypeEvaluator is set to false so the scan
            // registers zero handlers — callers compose their own handler sets separately via
            // the Add*Handlers() methods.
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            cfg.TypeEvaluator = _ => false;

            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Registers all item and content generation request handlers
    /// (<c>RealmEngine.Core.Features.ItemGeneration</c>).
    /// These cover generating items, enemies, NPCs, powers, and ability queries.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGenerationHandlers(this IServiceCollection services)
        => services.RegisterHandlersInFeatures("ItemGeneration");

    /// <summary>
    /// Registers all character creation and background query request handlers
    /// (<c>RealmEngine.Core.Features.CharacterCreation</c> and <c>Characters</c>).
    /// Requires <see cref="RealmEngine.Shared.Abstractions.ICharacterCreationSessionStore"/> to be
    /// registered in the host container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCharacterCreationHandlers(this IServiceCollection services)
        => services.RegisterHandlersInFeatures("CharacterCreation", "Characters");

    /// <summary>
    /// Registers all read-only catalog query handlers — actor instances, dialogues,
    /// enchantments, enemies, items, loot tables, materials, material properties, NPCs,
    /// organizations, powers, quests, recipes, skills, species, tilemaps, traits, and zones.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCatalogHandlers(this IServiceCollection services)
        => services.RegisterHandlersInFeatures(
            "ActorInstanceCatalog",
            "DialogueCatalog",
            "EnchantmentCatalog",
            "EnemyCatalog",
            "ItemCatalog",
            "LootTableCatalog",
            "MaterialCatalog",
            "MaterialPropertyCatalog",
            "NpcCatalog",
            "OrganizationCatalog",
            "PowerCatalog",
            "QuestCatalog",
            "RecipeCatalog",
            "SkillCatalog",
            "Species",
            "Tilemap",
            "TraitCatalog",
            "ZoneLocationCatalog");

    /// <summary>
    /// Registers all live gameplay request handlers — combat, crafting, death, difficulty,
    /// enchanting, equipment, exploration, harvesting, inventory, level-up, party, progression,
    /// quests, reputation, salvaging, shop, socketing, upgrading, victory, and achievements.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGameplayHandlers(this IServiceCollection services)
        => services.RegisterHandlersInFeatures(
            "Combat",
            "Crafting",
            "Death",
            "Difficulty",
            "Enchanting",
            "Equipment",
            "Exploration",
            "Harvesting",
            "Inventory",
            "LevelUp",
            "Party",
            "Progression",
            "Quests",
            "Reputation",
            "Salvaging",
            "Shop",
            "Socketing",
            "Upgrading",
            "Victory",
            "Achievements");

    /// <summary>
    /// Registers all save and load game request handlers
    /// (<c>RealmEngine.Core.Features.SaveLoad</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSaveLoadHandlers(this IServiceCollection services)
        => services.RegisterHandlersInFeatures("SaveLoad");

    /// <summary>
    /// Registers the MediatR pipeline and the generation feature handlers only.
    /// Intended for lightweight hosts such as the Discord bot that only dispatch
    /// <c>Generate*</c> commands and do not require any other gameplay handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineGenerationMediatR(this IServiceCollection services)
    {
        services.AddRealmEngineMediatRPipeline();
        services.AddGenerationHandlers();
        return services;
    }

    // Scans the Core assembly and registers every concrete IRequestHandler<,> implementation
    // whose namespace falls under RealmEngine.Core.Features.<featureName> (exact segment match,
    // so "Quest" does not accidentally include "QuestCatalog").
    private static IServiceCollection RegisterHandlersInFeatures(
        this IServiceCollection services, params string[] featureNames)
    {
        const string prefix = "RealmEngine.Core.Features";
        var requestHandlerOpenType = typeof(IRequestHandler<,>);

        foreach (var type in typeof(ServiceCollectionExtensions).Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.Namespace is null) continue;

            var ns = type.Namespace;
            var matchesFeature = featureNames.Any(f =>
                ns == $"{prefix}.{f}" || ns.StartsWith($"{prefix}.{f}."));

            if (!matchesFeature) continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                if (iface.GetGenericTypeDefinition() != requestHandlerOpenType) continue;
                services.AddTransient(iface, type);
            }
        }

        return services;
    }
}
