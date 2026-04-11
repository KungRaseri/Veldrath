using Microsoft.Extensions.DependencyInjection;

namespace Veldrath.Assets;

/// <summary>Provides DI registration extensions for <c>Veldrath.Assets</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAssetStore"/> and its dependencies with the service container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// Optional delegate for overriding <see cref="AssetStoreOptions"/>.
    /// When omitted, <see cref="AssetStoreOptions.BasePath"/> defaults to
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVeldrathAssets(
        this IServiceCollection services,
        Action<AssetStoreOptions>? configure = null)
    {
        services.AddMemoryCache();
        services.Configure<AssetStoreOptions>(opts => configure?.Invoke(opts));
        services.AddSingleton<IAssetStore, AssetStore>();
        return services;
    }
}
