using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;

namespace RealmUnbound.Server.Services;

/// <summary>
/// Periodically evicts idle character creation sessions from the in-memory store.
/// Runs every 5 minutes; idle threshold is controlled by <c>CharacterCreation:SessionIdleMinutes</c>
/// (default 30).
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly ICharacterCreationSessionStore _store;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _maxIdle;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    /// <summary>Initializes a new instance of <see cref="SessionCleanupService"/>.</summary>
    /// <param name="store">The session store to evict idle sessions from.</param>
    /// <param name="config">Application configuration for reading the idle timeout.</param>
    /// <param name="logger">Logger for eviction events.</param>
    public SessionCleanupService(
        ICharacterCreationSessionStore store,
        IConfiguration config,
        ILogger<SessionCleanupService> logger)
    {
        _store = store;
        _logger = logger;
        var idleMinutes = config.GetValue<int>("CharacterCreation:SessionIdleMinutes", 30);
        _maxIdle = TimeSpan.FromMinutes(idleMinutes);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Only the in-memory store has an eviction method; skip if using a different store.
            if (_store is not InMemoryCharacterCreationSessionStore inMemory)
                continue;

            var evicted = inMemory.EvictExpiredSessions(_maxIdle);
            if (evicted > 0)
                _logger.LogInformation("Session cleanup: evicted {Count} idle creation session(s) (idle > {MaxIdle})", evicted, _maxIdle);
        }
    }
}
