using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Features.Party.Commands;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Services;

using HarvestingConfigData = RealmEngine.Core.Services.Harvesting.HarvestingConfig;

namespace RealmEngine.Core.Tests;

/// <summary>
/// Exercises the four registration paths of <see cref="ServiceCollectionExtensions"/> end to end:
/// the default in-memory container (every descriptor resolved and its lifetime asserted), the
/// Npgsql container (EF repositories), the external-repository mode, and the granular
/// MediatR/handler composition helpers.
/// </summary>
[Trait("Category", "Composition")]
public class ServiceCollectionExtensionsCoverageTests
{
    private static readonly Type[] NonConstructibleServices =
    [
        typeof(IServiceProvider),
        typeof(IServiceScopeFactory),
        typeof(IServiceProviderIsService),
        typeof(IServiceProviderIsKeyedService),
        typeof(IKeyedServiceProvider)
    ];

    private static ServiceCollection CreateCoreCollection(Action<PersistenceOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRealmEngineCore(configure);
        return services;
    }

    private static bool IsLifetimeFailure(IServiceProvider provider, ServiceDescriptor descriptor, out string failure)
    {
        failure = string.Empty;
        var type = descriptor.ServiceType;

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        object? first;
        try
        {
            first = scopeA.ServiceProvider.GetService(type);
        }
        catch (Exception ex)
        {
            failure = $"{type.Name}: failed to resolve ({ex.GetType().Name}: {ex.Message})";
            return true;
        }

        if (first is null)
        {
            failure = $"{type.Name}: resolved to null";
            return true;
        }

        if (descriptor.Lifetime == ServiceLifetime.Transient)
        {
            var second = scopeA.ServiceProvider.GetService(type);
            if (ReferenceEquals(first, second))
            {
                failure = $"{type.Name}: transient returned the same instance twice";
                return true;
            }
            return false;
        }

        if (descriptor.Lifetime == ServiceLifetime.Singleton)
        {
            var acrossScopes = scopeB.ServiceProvider.GetService(type);
            if (!ReferenceEquals(first, acrossScopes))
            {
                failure = $"{type.Name}: singleton returned different instances across scopes";
                return true;
            }
            return false;
        }

        var sameScope = scopeA.ServiceProvider.GetService(type);
        var otherScope = scopeB.ServiceProvider.GetService(type);
        if (!ReferenceEquals(first, sameScope))
        {
            failure = $"{type.Name}: scoped returned different instances within one scope";
            return true;
        }
        if (ReferenceEquals(first, otherScope))
        {
            failure = $"{type.Name}: scoped returned the same instance across scopes";
            return true;
        }
        return false;
    }

    [Fact]
    public void AddRealmEngineCore_RegistersEveryService_AndEachResolvesWithItsDeclaredLifetime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRealmEngineCore();

        var descriptors = services
            .Where(d => !d.ServiceType.IsGenericTypeDefinition)
            .Where(d => !NonConstructibleServices.Contains(d.ServiceType))
            .ToList();

        descriptors.Should().HaveCountGreaterThan(90, "the core container surface is large");

        services.AddRealmEngineMediatR();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var failures = new List<string>();
        foreach (var descriptor in descriptors)
        {
            if (IsLifetimeFailure(provider, descriptor, out var failure))
            {
                failures.Add(failure);
            }
        }

        failures.Should().BeEmpty("every registration must resolve and honour its lifetime:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void AddRealmEngineCore_DefaultMode_RegistersInMemoryRepositoriesAndDefaultConfig()
    {
        var services = CreateCoreCollection();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        descriptor(services, typeof(ISaveGameRepository)).ImplementationType.Should().Be(typeof(InMemorySaveGameRepository));
        descriptor(services, typeof(IItemRepository)).ImplementationType.Should().Be(typeof(InMemoryItemRepository));
        descriptor(services, typeof(IEnemyRepository)).ImplementationType.Should().Be(typeof(InMemoryEnemyRepository));
        descriptor(services, typeof(ISpeciesRepository)).ImplementationType.Should().Be(typeof(InMemorySpeciesRepository));
        descriptor(services, typeof(IInventoryService)).ImplementationType.Should().Be(typeof(InMemoryInventoryService));
        descriptor(services, typeof(GameConfigService)).ImplementationType.Should().Be(typeof(NullGameConfigService));

        scope.ServiceProvider.GetRequiredService<GameConfigService>().GetData("growth-stats")
            .Should().BeNull("the in-memory config service is a no-op");

        scope.ServiceProvider.GetRequiredService<ContentDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<ContentDbContext>>().Should().NotBeNull();
    }

    [Fact]
    public void AddRealmEngineCore_DefaultMode_UsesSingletonsForCatalogServices()
    {
        using var provider = CreateCoreCollection().BuildServiceProvider();

        var random = provider.GetRequiredService<IRandomSource>();
        random.Should().BeSameAs(SystemRandomSource.Shared, "the shared random source is stateless and registered as a singleton");
        provider.GetRequiredService<IRandomSource>().Should().BeSameAs(random);

        provider.GetRequiredService<PowerDataService>().Should().BeSameAs(provider.GetRequiredService<PowerDataService>());
        provider.GetRequiredService<SkillDataService>().Should().BeSameAs(provider.GetRequiredService<SkillDataService>());
        provider.GetRequiredService<NamePatternService>().Should().BeSameAs(provider.GetRequiredService<NamePatternService>());
        provider.GetRequiredService<CategoryDiscoveryService>().Should().BeSameAs(provider.GetRequiredService<CategoryDiscoveryService>());

        var harvestingConfig = provider.GetRequiredService<HarvestingConfigData>();
        harvestingConfig.Should().BeSameAs(provider.GetRequiredService<HarvestingConfigData>());
        provider.GetRequiredService<BudgetConfig>().Should().BeSameAs(provider.GetRequiredService<BudgetConfig>());
        provider.GetRequiredService<MaterialPools>().Should().NotBeNull();
        provider.GetRequiredService<EnemyTypes>().Should().NotBeNull();
        provider.GetRequiredService<MaterialFilterConfig>().Should().NotBeNull();
        provider.GetRequiredService<SocketConfig>().Should().NotBeNull();
        provider.GetRequiredService<BudgetCalculator>().Should().NotBeNull();
    }

    [Fact]
    public void AddRealmEngineCore_DefaultMode_RegistersFeatureServicesAsScopedImplementations()
    {
        var services = CreateCoreCollection();
        services.AddRealmEngineMediatR();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        descriptor(services, typeof(IGameStateService)).ImplementationType.Should().Be(typeof(GameStateService));
        descriptor(services, typeof(ISaveGameService)).ImplementationType.Should().Be(typeof(SaveGameService));
        descriptor(services, typeof(IApocalypseTimer)).ImplementationType.Should().Be(typeof(ApocalypseTimer));
        descriptor(services, typeof(IPassiveBonusCalculator)).ImplementationType.Should().Be(typeof(PassiveBonusCalculator));
        descriptor(services, typeof(CraftingService)).ImplementationType.Should().Be(typeof(CraftingService));
        descriptor(services, typeof(ExplorationService)).ImplementationType.Should().Be(typeof(ExplorationService));
        descriptor(services, typeof(PartyAIService)).ImplementationType.Should().Be(typeof(PartyAIService));
        descriptor(services, typeof(FactionDataService)).ImplementationType.Should().Be(typeof(FactionDataService));
        descriptor(services, typeof(SpellCastingService)).ImplementationType.Should().Be(typeof(SpellCastingService));

        scope.ServiceProvider.GetRequiredService<CombatService>().Should().NotBeNull("CombatService is built through an explicit factory");
        scope.ServiceProvider.GetRequiredService<ICombatSettings>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ItemGenerator>().Should().NotBeNull();
    }

    [Fact]
    public void AddRealmEngineCore_NpgsqlMode_RegistersEfCoreRepositoriesAndDbServices()
    {
        const string connectionString = "Host=localhost;Database=realmengine_test;Username=test;Password=test";

        var services = CreateCoreCollection(options => options.UseNpgsql(connectionString));

        descriptor(services, typeof(ISaveGameRepository)).ImplementationType.Should().Be(typeof(EfCoreSaveGameRepository));
        descriptor(services, typeof(IItemRepository)).ImplementationType.Should().Be(typeof(EfCoreItemRepository));
        descriptor(services, typeof(IPowerRepository)).ImplementationType.Should().Be(typeof(EfCorePowerRepository));
        descriptor(services, typeof(ITraitDefinitionRepository)).ImplementationType.Should().Be(typeof(EfCoreTraitDefinitionRepository));
        descriptor(services, typeof(IInventoryService)).ImplementationType.Should().Be(typeof(EfCoreInventoryService));
        descriptor(services, typeof(GameConfigService)).ImplementationType.Should().Be(typeof(DbGameConfigService));
        descriptor(services, typeof(GameConfigService)).Lifetime.Should().Be(ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(GameDbContext));
        services.Should().Contain(d => d.ServiceType == typeof(IDbContextFactory<ContentDbContext>));

        var efCoreRepositoryCount = services.Count(d =>
            d.ImplementationType is { } implementationType &&
            implementationType.Namespace == typeof(EfCoreSaveGameRepository).Namespace &&
            implementationType.Name.StartsWith("EfCore", StringComparison.Ordinal));
        efCoreRepositoryCount.Should().BeGreaterThanOrEqualTo(20, "Npgsql mode binds the whole repository surface to EF");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<GameDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ContentDbContext>().Should().NotBeNull();
    }

    [Fact]
    public void AddRealmEngineCore_ExternalMode_SkipsRepositoryRegistrations_ButKeepsCoreServices()
    {
        var services = CreateCoreCollection(options => options.UseExternal());

        services.Should().NotContain(d => d.ServiceType == typeof(ISaveGameRepository),
            "external mode leaves repositories to the host");
        services.Should().NotContain(d => d.ServiceType == typeof(IItemRepository));
        services.Should().Contain(d => d.ServiceType == typeof(ISaveGameContext));
        services.Should().Contain(d => d.ServiceType == typeof(INodeRepository));
        services.Should().Contain(d => d.ServiceType == typeof(CategoryDiscoveryService));

        services.Count(d => d.ServiceType == typeof(CategoryDiscoveryService)).Should().Be(1);
    }

    [Fact]
    public void AddRealmEngineMediatRPipeline_RegistersMediatorAndValidators_WithoutAnyHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRealmEngineMediatRPipeline();

        services.Should().Contain(d => d.ServiceType == typeof(IMediator));
        services.Should().NotContain(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>),
            "the pipeline helper deliberately scans zero handlers");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMediator>().Should().NotBeNull();

        var validatorServiceTypes = services
            .Where(d => d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(FluentValidation.IValidator<>))
            .Select(d => d.ServiceType)
            .ToList();
        validatorServiceTypes.Should().NotBeEmpty("the assembly's FluentValidation validators are registered");
        foreach (var validatorType in validatorServiceTypes)
        {
            scope.ServiceProvider.GetService(validatorType).Should().NotBeNull();
        }
    }

    public static TheoryData<string> HandlerGroups => new()
    {
        nameof(ServiceCollectionExtensions.AddGenerationHandlers),
        nameof(ServiceCollectionExtensions.AddCharacterCreationHandlers),
        nameof(ServiceCollectionExtensions.AddCatalogHandlers),
        nameof(ServiceCollectionExtensions.AddGameplayHandlers),
        nameof(ServiceCollectionExtensions.AddSaveLoadHandlers)
    };

    [Theory]
    [MemberData(nameof(HandlerGroups))]
    public void HandlerGroupMethods_RegisterOnlyTransientRequestHandlersFromTheirFeatures(string methodName)
    {
        var services = CreateCoreCollection();
        var method = typeof(ServiceCollectionExtensions).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        method.Invoke(null, [services]);

        var handlerDescriptors = services
            .Where(d => d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .GroupBy(d => d.ServiceType)
            .Select(g => g.First())
            .ToList();

        handlerDescriptors.Should().NotBeEmpty($"{methodName} registers at least one handler");
        handlerDescriptors.Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Transient);
        handlerDescriptors.Should().OnlyContain(d => d.ImplementationType!.Namespace!.StartsWith("RealmEngine.Core.Features.", StringComparison.Ordinal));

        var expectedHandlerNames = methodName switch
        {
            nameof(ServiceCollectionExtensions.AddGenerationHandlers) => new[] { "GenerateItemCommandHandler" },
            nameof(ServiceCollectionExtensions.AddCharacterCreationHandlers) => new[] { "CreateCharacterHandler" },
            nameof(ServiceCollectionExtensions.AddCatalogHandlers) => new[] { "GetEnemyCatalogQueryHandler" },
            nameof(ServiceCollectionExtensions.AddGameplayHandlers) => new[] { "CraftRecipeHandler", "PartyCombatTurnHandler", "EquipItemHandler" },
            _ => new[] { "SaveGameHandler" }
        };
        handlerDescriptors.Select(d => d.ImplementationType!.Name).Should().Contain(expectedHandlerNames);

        services.AddRealmEngineMediatR();
        if (methodName == nameof(ServiceCollectionExtensions.AddCharacterCreationHandlers))
        {
            // Documented host responsibility: creation-session storage lives outside the engine.
            services.AddScoped(_ => new Mock<ICharacterCreationSessionStore>().Object);
        }
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        foreach (var handler in handlerDescriptors)
        {
            scope.ServiceProvider.GetService(handler.ServiceType).Should().NotBeNull(
                $"the handler registered by {methodName} must be constructible");
        }
    }

    [Fact]
    public void AddRealmEngineGenerationMediatR_RegistersPipelineAndGenerationHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRealmEngineGenerationMediatR();

        services.Should().Contain(d => d.ServiceType == typeof(IMediator));
        var handlerTypes = services
            .Where(d => d.ImplementationType is not null)
            .Select(d => d.ImplementationType!.Name)
            .ToList();
        handlerTypes.Should().Contain("GenerateItemCommandHandler");
        handlerTypes.Should().Contain("GenerateEnemyCommandHandler");
        handlerTypes.Should().NotContain("CreateCharacterHandler", "only generation handlers are composed here");
    }

    [Fact]
    public async Task InitializeCatalogsAsync_LoadsBothCatalogSingletons()
    {
        using var provider = CreateCoreCollection().BuildServiceProvider();

        await provider.InitializeCatalogsAsync();

        var powers = provider.GetRequiredService<PowerDataService>();
        powers.GetAllPowers().Should().BeEmpty("the in-memory content database starts empty");

        var skills = provider.GetRequiredService<SkillDataService>();
        skills.GetAllSkills().Should().BeEmpty();

        var act = () => provider.InitializeCatalogsAsync();
        await act.Should().NotThrowAsync("initialisation is idempotent");
    }

    [Fact]
    public void AddRealmEngineCore_ReturnsTheSameCollection_ForChaining()
    {
        var services = new ServiceCollection();

        services.AddRealmEngineCore().Should().BeSameAs(services);
        services.AddRealmEngineMediatR().Should().BeSameAs(services);
        services.AddRealmEngineMediatRPipeline().Should().BeSameAs(services);
    }

    private static ServiceDescriptor descriptor(IServiceCollection services, Type serviceType) =>
        services.Last(d => d.ServiceType == serviceType);
}
