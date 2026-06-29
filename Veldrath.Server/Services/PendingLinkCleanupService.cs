using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Services;

/// <summary>Background service that periodically purges expired pending-link tokens.</summary>
public sealed class PendingLinkCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingLinkCleanupService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Initializes a new instance of <see cref="PendingLinkCleanupService"/>.</summary>
    /// <param name="scopeFactory">Factory for creating DI scopes to resolve scoped dependencies.</param>
    /// <param name="logger">Logger for cleanup events.</param>
    public PendingLinkCleanupService(IServiceScopeFactory scopeFactory, ILogger<PendingLinkCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPendingLinkRepository>();
            await repo.PurgeExpiredAsync(stoppingToken);
            _logger.LogInformation("Pending link cleanup: purged expired tokens");
        }
    }
}
