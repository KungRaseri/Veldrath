using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Background sweeper that brings dead enemies back and prunes abandoned combat sessions.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the old fire-and-forget <c>Task.Run</c> + <c>Task.Delay</c> respawn timer. The pending respawn lives
/// in the <c>EnemyInstances.RespawnAt</c> column, so a restart mid-timer simply continues the countdown instead of
/// losing it, and the interval is configuration rather than a hardcoded delay.
/// </para>
/// <para>
/// The sweeper is safe to run on several instances: <see cref="IEnemyInstanceStore.SweepExpiredRespawnsAsync"/>
/// claims each batch with <c>FOR UPDATE SKIP LOCKED</c> on PostgreSQL. The broadcast happens after the commit, so a
/// missed broadcast is a cosmetic delay — a client that reconnects re-reads the roster.
/// </para>
/// </remarks>
public sealed class EnemyRespawnService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly EnemyWorldOptions _options;
    private readonly ILogger<EnemyRespawnService> _logger;

    /// <summary>Initializes a new instance of <see cref="EnemyRespawnService"/>.</summary>
    /// <param name="scopeFactory">Factory for the per-sweep service scope (the stores are scoped).</param>
    /// <param name="hubContext">Hub context used to broadcast <c>EnemySpawned</c>.</param>
    /// <param name="options">World tuning (interval, batch size, session grace).</param>
    /// <param name="logger">Logger instance.</param>
    public EnemyRespawnService(
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hubContext,
        IOptions<EnemyWorldOptions> options,
        ILogger<EnemyRespawnService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.SweepIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never let one bad sweep kill the service; log it and try again on the next tick.
                    _logger.LogError(ex, "Enemy respawn sweep failed; retrying on the next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
    }

    /// <summary>Runs one sweep: revives due enemies, broadcasts them, then prunes expired sessions.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the sweep is done.</returns>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var enemies = scope.ServiceProvider.GetRequiredService<IEnemyInstanceStore>();
        var sessions = scope.ServiceProvider.GetRequiredService<ICombatSessionStore>();

        var respawned = await enemies.SweepExpiredRespawnsAsync(_options.RespawnBatchSize, cancellationToken);

        foreach (var enemy in respawned)
        {
            await _hubContext.Clients.Group(enemy.ZoneGroup).SendAsync(
                "EnemySpawned",
                new
                {
                    enemy.Id,
                    enemy.Name,
                    enemy.Level,
                    enemy.CurrentHealth,
                    enemy.MaxHealth,
                },
                cancellationToken);
        }

        if (respawned.Count > 0)
        {
            _logger.LogInformation(
                "Respawn sweep revived {Count} enemy instance(s) across {Zones} zone group(s)",
                respawned.Count,
                respawned.Select(enemy => enemy.ZoneGroup).Distinct().Count());
        }

        var pruned = await sessions.PruneExpiredAsync(_options.SessionGrace, cancellationToken);
        if (pruned.Count > 0)
        {
            _logger.LogInformation(
                "Pruned {Count} combat session(s) whose owner disconnected beyond the {Grace}s grace window",
                pruned.Count,
                _options.SessionGraceSeconds);
        }
    }
}
