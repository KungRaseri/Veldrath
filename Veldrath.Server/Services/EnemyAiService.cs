using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Contracts.Tilemap;
using Veldrath.Server.Hubs;

namespace Veldrath.Server.Services;

/// <summary>
/// Background service that drives enemy AI on a 1-second tick.
/// For each zone that contains at least one player, each living enemy either steps
/// toward the nearest player (within 6 tiles) or performs a random wander move.
/// Broadcasts <c>EnemyMoved</c> to the zone's SignalR group after each successful move.
/// </summary>
public class EnemyAiService : IHostedService, IDisposable
{
    private readonly IZoneEntityTracker _entityTracker;
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<EnemyAiService> _logger;

    // Per-zone map cache; populated lazily and never invalidated (maps are static assets)
    private readonly ConcurrentDictionary<string, TiledMap> _mapCache = new(StringComparer.OrdinalIgnoreCase);

    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    /// <summary>Initializes a new instance of <see cref="EnemyAiService"/>.</summary>
    public EnemyAiService(
        IZoneEntityTracker entityTracker,
        ITileMapRepository tilemapRepo,
        IHubContext<GameHub> hubContext,
        ILogger<EnemyAiService> logger)
    {
        _entityTracker = entityTracker;
        _tilemapRepo   = tilemapRepo;
        _hubContext    = hubContext;
        _logger        = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts      = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer    = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _loopTask = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_loopTask is not null)
            await _loopTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_timer is not null && await _timer.WaitForNextTickAsync(ct))
                await TickAsync(ct);
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnemyAiService loop terminated unexpectedly");
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var zones = _entityTracker.GetZonesWithPlayers();
        foreach (var zoneId in zones)
        {
            try
            {
                await TickZoneAsync(zoneId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI tick failed for zone {ZoneId}", zoneId);
            }
        }
    }

    private async Task TickZoneAsync(string zoneId, CancellationToken ct)
    {
        var enemies = _entityTracker.GetEntities(zoneId);
        if (enemies.Count == 0) return;

        var players   = _entityTracker.GetPlayerPositions(zoneId);
        var groupName = _entityTracker.GetZoneGroupName(zoneId);
        if (groupName is null) return;

        var map = await GetMapAsync(zoneId);
        if (map is null) return;

        var rng = new Random();

        foreach (var enemy in enemies)
        {
            if (enemy.CurrentHealth <= 0) continue;

            var nearest  = FindNearest(enemy.TileX, enemy.TileY, players, maxRange: 6);
            var (nx, ny, dir) = nearest.HasValue
                ? StepToward(enemy.TileX, enemy.TileY, nearest.Value.X, nearest.Value.Y, map)
                : RandomWander(enemy.TileX, enemy.TileY, map, rng);

            if (nx == enemy.TileX && ny == enemy.TileY) continue;

            _entityTracker.UpdatePosition(zoneId, enemy.EntityId, nx, ny, dir);

            var payload = new EnemyMovedPayload(enemy.EntityId, enemy.SpriteKey, nx, ny, dir);
            await _hubContext.Clients.Group(groupName).SendAsync("EnemyMoved", payload, ct);
        }
    }

    private async Task<TiledMap?> GetMapAsync(string zoneId)
    {
        if (_mapCache.TryGetValue(zoneId, out var cached)) return cached;

        var loaded = await _tilemapRepo.GetByZoneIdAsync(zoneId);
        if (loaded is not null)
            _mapCache[zoneId] = loaded;
        return loaded;
    }

    // ── Movement helpers ──────────────────────────────────────────────────────

    private static (Guid CharacterId, int X, int Y)? FindNearest(
        int ex, int ey,
        IReadOnlyList<(Guid CharacterId, int X, int Y)> players,
        int maxRange)
    {
        (Guid CharacterId, int X, int Y)? best = null;
        var bestDist = int.MaxValue;

        foreach (var p in players)
        {
            var dist = Math.Abs(p.X - ex) + Math.Abs(p.Y - ey);
            if (dist <= maxRange && dist < bestDist)
            {
                bestDist = dist;
                best     = p;
            }
        }

        return best;
    }

    private static (int x, int y, string dir) StepToward(
        int ex, int ey, int px, int py, TiledMap map)
    {
        // Greedy step: pick whichever adjacent tile minimises remaining Manhattan distance
        Span<(int dx, int dy, string dir)> candidates =
        [
            (0, -1, "N"),
            (0,  1, "S"),
            (1,  0, "E"),
            (-1, 0, "W"),
        ];

        var bestDist = Math.Abs(px - ex) + Math.Abs(py - ey);
        var bestX    = ex;
        var bestY    = ey;
        var bestDir  = "S";

        foreach (var (dx, dy, d) in candidates)
        {
            var nx = ex + dx;
            var ny = ey + dy;
            if (map.IsBlocked(nx, ny)) continue;
            var dist = Math.Abs(px - nx) + Math.Abs(py - ny);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestX    = nx;
                bestY    = ny;
                bestDir  = d;
            }
        }

        return (bestX, bestY, bestDir);
    }

    private static (int x, int y, string dir) RandomWander(int ex, int ey, TiledMap map, Random rng)
    {
        // 40 % chance to stay still
        if (rng.Next(10) < 4) return (ex, ey, "S");

        Span<(int dx, int dy, string dir)> candidates =
        [
            (0, -1, "N"),
            (0,  1, "S"),
            (1,  0, "E"),
            (-1, 0, "W"),
        ];

        // Shuffle and pick first unblocked
        for (var i = candidates.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var (dx, dy, d) in candidates)
        {
            var nx = ex + dx;
            var ny = ey + dy;
            if (!map.IsBlocked(nx, ny))
                return (nx, ny, d);
        }

        return (ex, ey, "S");
    }
}
