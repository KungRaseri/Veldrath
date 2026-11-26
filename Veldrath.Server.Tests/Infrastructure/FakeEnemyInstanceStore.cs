using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IEnemyInstanceStore"/> for handler and service tests.
/// </summary>
/// <remarks>
/// The sync helpers exist so tests can arrange and assert state without awaiting; the interface methods simply
/// delegate to them. Replaces direct use of the deleted static <c>ZoneLocationEnemyStore</c>.
/// </remarks>
internal sealed class FakeEnemyInstanceStore : IEnemyInstanceStore
{
    private readonly Dictionary<Guid, (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy)> _rows = new();
    private readonly HashSet<Guid> _dead = new();

    /// <summary>Gets how many times health or participation has been persisted.</summary>
    public int SaveCount { get; private set; }

    /// <summary>Gets the respawns that the next sweep should report, which the sweep drains.</summary>
    public Queue<EnemyRespawnedEvent> DueRespawns { get; } = new();

    /// <summary>Gets how many sweeps have been performed.</summary>
    public int SweepCount { get; private set; }

    /// <summary>Arranges an enemy at a location.</summary>
    /// <param name="zoneGroup">SignalR zone group.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="enemy">The enemy instance.</param>
    public void Add(string zoneGroup, string locationSlug, SpawnedEnemy enemy) =>
        _rows[enemy.Id] = (zoneGroup, locationSlug, enemy);

    /// <summary>Removes an enemy synchronously.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    public void Remove(Guid enemyId) => _rows.Remove(enemyId);

    /// <summary>Returns the stored enemy, if any.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    public SpawnedEnemy? Find(Guid enemyId) =>
        _rows.TryGetValue(enemyId, out var row) ? row.Enemy : null;

    /// <summary>Returns whether the death transition has been applied to an enemy.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    public bool IsMarkedDead(Guid enemyId) => _dead.Contains(enemyId);

    /// <summary>Gets how many times a location's cleared-room respawn window was applied.</summary>
    public Dictionary<string, int> ClearedWindowApplications { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the (group, location) pairs a roster was requested under, in call order.</summary>
    public List<(string ZoneGroup, string LocationSlug)> RosterRequests { get; } = [];

    /// <inheritdoc />
    public Task<int> ExtendClearedRespawnsAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default)
    {
        var key = $"{zoneGroup}/{locationSlug}";
        ClearedWindowApplications[key] = ClearedWindowApplications.GetValueOrDefault(key) + 1;
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public Task<int> CountAliveAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Values.Count(row =>
            row.ZoneGroup == zoneGroup && row.LocationSlug == locationSlug && !_dead.Contains(row.Enemy.Id)));

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpawnedEnemy>> GetAliveOrSpawnAsync(
        string zoneGroup,
        string locationSlug,
        Func<CancellationToken, Task<List<SpawnedEnemy>>> spawnRoster,
        Guid? zoneInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        RosterRequests.Add((zoneGroup, locationSlug));

        var existing = _rows.Values
            .Where(row => row.ZoneGroup == zoneGroup && row.LocationSlug == locationSlug)
            .Select(row => row.Enemy)
            .ToList();

        if (existing.Count == 0)
        {
            var spawned = await spawnRoster(cancellationToken);
            foreach (var enemy in spawned)
            {
                Add(zoneGroup, locationSlug, enemy);
            }

            existing = spawned;
        }

        return existing.Where(enemy => enemy.IsAlive).ToList();
    }

    /// <summary>
    /// Gets snapshots <see cref="TryGetAsync"/> hands out before it falls back to the stored row. The EF store
    /// materialises a fresh <see cref="SpawnedEnemy"/> per read, so two callers racing on one kill each hold their own
    /// copy loaded while the enemy was alive; a queue lets a single-threaded test reproduce that.
    /// </summary>
    public Queue<SpawnedEnemy> PendingReads { get; } = new();

    /// <inheritdoc />
    public Task<SpawnedEnemy?> TryGetAsync(Guid enemyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PendingReads.Count > 0 ? PendingReads.Dequeue() : Find(enemyId));

    /// <inheritdoc />
    public Task SaveAsync(
        SpawnedEnemy enemy,
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default)
    {
        Add(zoneGroup, locationSlug, enemy);
        SaveCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Like the EF store, the credited participants are read from the stored row at the transition, not from the
    /// caller's own copy — a caller that brings a stale snapshot still gets the durable set.
    /// </remarks>
    public Task<IReadOnlyList<Guid>?> TryMarkDeadAsync(Guid enemyId, CancellationToken cancellationToken = default)
    {
        if (!_rows.TryGetValue(enemyId, out var row) || !_dead.Add(enemyId))
        {
            return Task.FromResult<IReadOnlyList<Guid>?>(null);
        }

        lock (row.Enemy.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<Guid>?>(row.Enemy.Participants.ToList());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EnemyRespawnedEvent>> SweepExpiredRespawnsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        SweepCount++;

        var revived = new List<EnemyRespawnedEvent>();
        while (revived.Count < batchSize && DueRespawns.Count > 0)
        {
            var next = DueRespawns.Dequeue();
            _dead.Remove(next.Id);
            revived.Add(next);
        }

        return Task.FromResult<IReadOnlyList<EnemyRespawnedEvent>>(revived);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid enemyId, CancellationToken cancellationToken = default)
    {
        Remove(enemyId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> RemoveForeignZoneGroupRowsAsync(
        IReadOnlyDictionary<string, IReadOnlySet<string>> zonesByLocationSlug,
        CancellationToken cancellationToken = default)
    {
        var stale = _rows
            .Where(row =>
            {
                if (!zonesByLocationSlug.TryGetValue(row.Value.LocationSlug, out var zones))
                    return false;

                return !Veldrath.Server.Features.Zones.ZoneGroupName.TryParse(row.Value.ZoneGroup, out var group)
                       || !zones.Any(group.BelongsToZone);
            })
            .Select(row => row.Key)
            .ToList();

        foreach (var id in stale)
        {
            Remove(id);
        }

        return Task.FromResult(stale.Count);
    }
}
