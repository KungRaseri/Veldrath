using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Services;

/// <summary>Background service that periodically purges expired pending-link tokens.</summary>
public sealed class PendingLinkCleanupService : BackgroundService
{
    private readonly IPendingLinkRepository _repo;
    private readonly ILogger<PendingLinkCleanupService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Initializes a new instance of <see cref="PendingLinkCleanupService"/>.</summary>
    /// <param name="repo">Repository for pending-link token persistence.</param>
    /// <param name="logger">Logger for cleanup events.</param>
    public PendingLinkCleanupService(IPendingLinkRepository repo, ILogger<PendingLinkCleanupService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _repo.PurgeExpiredAsync(stoppingToken);
            _logger.LogInformation("Pending link cleanup: purged expired tokens");
        }
    }
}
