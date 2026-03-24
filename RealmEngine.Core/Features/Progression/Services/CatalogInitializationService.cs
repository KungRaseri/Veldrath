using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Hosted service that initializes the power, and skill catalog singletons
/// on application startup. Registers automatically when <c>AddRealmEngineCore()</c> is called.
/// </summary>
/// <remarks>
/// <para>
/// For Generic Host consumers (ASP.NET Core, Blazor, Avalonia with IHost) this runs
/// automatically before the app starts serving requests. No manual wiring is needed.
/// </para>
/// <para>
/// For non-hosted consumers (console apps, unit tests) call
/// <see cref="ServiceProviderExtensions.InitializeCatalogsAsync"/> on the built
/// <see cref="IServiceProvider"/> instead.
/// </para>
/// </remarks>
public sealed class CatalogInitializationService : IHostedService
{
    private readonly PowerDataService _powers;
    private readonly SkillDataService _skills;
    private readonly ILogger<CatalogInitializationService> _logger;

    /// <summary>Initializes a new instance of <see cref="CatalogInitializationService"/>.</summary>
    public CatalogInitializationService(
        PowerDataService powers,
        SkillDataService skills,
        ILogger<CatalogInitializationService>? logger = null)
    {
        _powers = powers;
        _skills    = skills;
        _logger    = logger ?? NullLogger<CatalogInitializationService>.Instance;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing game catalogs…");
        await _powers.InitializeAsync();
        await _skills.InitializeAsync();
        _logger.LogInformation("Game catalogs initialized.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
