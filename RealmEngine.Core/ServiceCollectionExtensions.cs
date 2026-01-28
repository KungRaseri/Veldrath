using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;

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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRealmEngineCore(this IServiceCollection services)
    {
        // Register category discovery service (singleton for caching)
        services.AddSingleton<CategoryDiscoveryService>();
        
        // Register generators
        services.AddScoped<ItemGenerator>();
        services.AddScoped<EnemyGenerator>();
        services.AddScoped<NpcGenerator>();
        services.AddScoped<AbilityGenerator>();
        services.AddScoped<CharacterClassGenerator>();
        
        // Register game logic services
        services.AddScoped<LootTableService>();
        services.AddScoped<CharacterGrowthService>();
        services.AddScoped<InMemoryInventoryService>();
        
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
        
        return services;
    }
}
