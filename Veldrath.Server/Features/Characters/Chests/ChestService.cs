using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Services;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Chests;

/// <summary>
/// Background sweeper that reopens emptied chests, places roaming ones, and brings depleted resource nodes back.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors <c>EnemyRespawnService</c> rather than inventing a second idiom. Nothing is timed in memory: a
/// chest carries <see cref="LootChest.ReopenAt"/>, a node carries its <c>LastHarvestedAt</c>, and the sweeper
/// only asks the database what is due. A restart mid-window therefore continues the countdown, and the same
/// sweep is safe to run on several server instances because every write is conditional on the row still being
/// in the state the sweep observed.
/// </para>
/// <para>
/// Roaming placement lives here rather than in a read path: a chest nobody is standing next to still has to
/// appear on time, and "only if someone looks" would make the world's rhythm depend on traffic. The candidates
/// are the zone's own locations — not a hardcoded list — so a zone that grows new locations grows new chest
/// sites, and the loot is drawn by the table's existing <c>RarityWeight</c>.
/// </para>
/// </remarks>
public sealed class ChestService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChestOptions _chests;
    private readonly HarvestingOptions _harvesting;
    private readonly ILogger<ChestService> _logger;

    /// <summary>Initializes a new instance of <see cref="ChestService"/>.</summary>
    /// <param name="scopeFactory">Factory for the per-sweep service scope (the repositories are scoped).</param>
    /// <param name="chests">Chest tuning: sweep interval, reopen window, roam cadence, per-zone cap.</param>
    /// <param name="harvesting">Node tuning, reused here for how long an emptied node stays empty.</param>
    /// <param name="logger">Logger instance.</param>
    public ChestService(
        IServiceScopeFactory scopeFactory,
        IOptions<ChestOptions> chests,
        IOptions<HarvestingOptions> harvesting,
        ILogger<ChestService> logger)
    {
        _scopeFactory = scopeFactory;
        _chests = chests.Value;
        _harvesting = harvesting.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_chests.SweepIntervalSeconds));

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
                    // One bad sweep must not kill the service; the state is all durable, so the next tick retries.
                    _logger.LogError(ex, "Chest sweep failed; retrying on the next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
    }

    /// <summary>Runs one sweep: reopens what is due, places a roaming chest where one is due, regrows nodes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the sweep is done.</returns>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var locations = scope.ServiceProvider.GetRequiredService<IZoneLocationRepository>();
        var lootTables = scope.ServiceProvider.GetRequiredService<ILootTableRepository>();
        var nodes = scope.ServiceProvider.GetRequiredService<INodeRepository>();
        var random = scope.ServiceProvider.GetService<IRandomSource>() ?? new SystemRandomSource();
        var now = DateTimeOffset.UtcNow;

        var reopened = await ReopenDueChestsAsync(db, now, cancellationToken);
        var placed = await PlaceRoamingChestsAsync(db, locations, lootTables, random, now, cancellationToken);
        var regrown = await RegrowNodesAsync(nodes, now, cancellationToken);

        if (reopened > 0 || placed > 0 || regrown > 0)
        {
            _logger.LogInformation(
                "Chest sweep: {Reopened} chest(s) reopened, {Placed} roaming chest(s) placed, {Regrown} node(s) regrown",
                reopened, placed, regrown);
        }
    }

    /// <summary>Clears the opened state of every chest whose reopen timer has elapsed.</summary>
    /// <param name="db">Application database.</param>
    /// <param name="now">Sweep instant (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many chests came back.</returns>
    private static async Task<int> ReopenDueChestsAsync(
        ApplicationDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Conditional on IsOpened so two sweeper instances cannot both clear, or double-count, the same row.
        return await db.LootChests
            .Where(chest => chest.IsOpened && chest.ReopenAt != null && chest.ReopenAt <= now)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(chest => chest.IsOpened, false)
                    .SetProperty(chest => chest.OpenedAt, (DateTimeOffset?)null)
                    .SetProperty(chest => chest.ReopenAt, (DateTimeOffset?)null)
                    .SetProperty(chest => chest.SpawnedAt, now),
                cancellationToken);
    }

    /// <summary>Places one roaming chest per zone that is due one, from that zone's own locations.</summary>
    /// <param name="db">Application database.</param>
    /// <param name="locations">Zone location catalog.</param>
    /// <param name="lootTables">Loot table catalog, used to draw which chest it is.</param>
    /// <param name="random">Random source, so the draw is reproducible in a test.</param>
    /// <param name="now">Sweep instant (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many roaming chests were placed.</returns>
    private async Task<int> PlaceRoamingChestsAsync(
        ApplicationDbContext db,
        IZoneLocationRepository locations,
        ILootTableRepository lootTables,
        IRandomSource random,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Towns are left alone: a chest in the market square is a pickup, not a find.
        var zones = await db.Zones
            .Where(zone => zone.Type != ZoneType.Town)
            .Select(zone => zone.Id)
            .ToListAsync(cancellationToken);

        if (zones.Count == 0)
            return 0;

        var live = await db.LootChests
            .Where(chest => !chest.IsOpened && chest.ZoneInstanceId == null)
            .Select(chest => new { chest.ZoneId, chest.LocationSlug, chest.Kind })
            .ToListAsync(cancellationToken);

        var history = await db.LootChests
            .Where(chest => chest.Kind == LootChestKind.Roaming)
            .Select(chest => new { chest.ZoneId, chest.SpawnedAt, chest.OpenedAt })
            .ToListAsync(cancellationToken);

        var tables = (await lootTables.GetByContextAsync("chests"))
            .Where(table => table.RarityWeight > 0)
            .ToList();

        if (tables.Count == 0)
        {
            _logger.LogWarning("No chest loot tables are seeded; roaming chests cannot be placed");
            return 0;
        }

        var placed = 0;

        foreach (var zoneId in zones)
        {
            var inZone = live.Where(chest => chest.ZoneId == zoneId).ToList();
            if (inZone.Count(chest => chest.Kind == LootChestKind.Roaming) >= _chests.MaxRoamingPerZone)
                continue;

            // The clock is the last time anything roamed here appeared or was opened, so a zone that is being
            // farmed does not simply out-pace the cadence.
            var last = history
                .Where(row => row.ZoneId == zoneId)
                .Select(row => row.OpenedAt ?? row.SpawnedAt)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();

            if (last + TimeSpan.FromMinutes(_chests.SpawnEveryMinutes) > now)
                continue;

            var occupied = inZone.Select(chest => chest.LocationSlug).ToHashSet(StringComparer.Ordinal);
            var candidates = (await locations.GetByZoneIdAsync(zoneId))
                .Where(location => !location.IsHidden
                                   && !location.IsSafeRespawn
                                   && !location.IsInstanced
                                   && (location.MinLevel ?? 0) >= _chests.MinSiteDangerLevel
                                   && !occupied.Contains(location.Slug))
                .ToList();

            if (candidates.Count == 0)
                continue;

            var site = PickWeighted(candidates, location => location.RarityWeight, random);
            var table = PickWeighted(tables, candidate => candidate.RarityWeight, random);

            if (site is null || table is null)
                continue;

            db.LootChests.Add(new LootChest
            {
                ChestId = $"roaming:{zoneId}:{site.Slug}:{Guid.NewGuid():N}",
                Kind = LootChestKind.Roaming,
                ZoneId = zoneId,
                LocationSlug = site.Slug,
                DisplayName = "Hidden Cache",
                LootTableRefs = JsonSerializer.Serialize(new[] { $"@loot-tables/chests/forest:{table.Slug}" }),
                MergeStrategy = "addPools",
                SpawnedAt = now,
            });

            await db.SaveChangesAsync(cancellationToken);
            placed++;

            _logger.LogInformation(
                "Placed a roaming chest at {ZoneId}/{LocationSlug} using loot table {TableSlug}",
                zoneId, site.Slug, table.Slug);
        }

        return placed;
    }

    /// <summary>Brings back every resource node that has been empty for longer than the regrow window.</summary>
    /// <param name="nodes">Node repository.</param>
    /// <param name="now">Sweep instant (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many nodes came back.</returns>
    private async Task<int> RegrowNodesAsync(
        INodeRepository nodes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.UtcDateTime.AddMinutes(-_harvesting.RegrowMinutes);
        var due = (await nodes.GetNodesReadyForRegenerationAsync())
            .Where(node => node.LastHarvestedAt <= cutoff)
            .ToList();

        foreach (var node in due)
        {
            node.CurrentHealth = node.MaxHealth;
            node.TimesHarvested = 0;
            node.LastHarvestedAt = now.UtcDateTime;
            await nodes.SaveNodeAsync(node);
        }

        return due.Count;
    }

    /// <summary>Draws one entry by weight, or <see langword="null"/> when nothing has a usable weight.</summary>
    /// <typeparam name="T">Entry type.</typeparam>
    /// <param name="entries">Candidate entries.</param>
    /// <param name="weight">Weight selector.</param>
    /// <param name="random">Random source.</param>
    /// <returns>The chosen entry, or <see langword="null"/>.</returns>
    private static T? PickWeighted<T>(IReadOnlyList<T> entries, Func<T, int> weight, IRandomSource random)
        where T : class
    {
        var total = entries.Sum(entry => Math.Max(1, weight(entry)));
        if (total <= 0)
            return default;

        var roll = random.Next(total);
        var running = 0;

        foreach (var entry in entries)
        {
            running += Math.Max(1, weight(entry));
            if (roll < running)
                return entry;
        }

        return entries[^1];
    }
}
