using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// EF Core implementation of <see cref="IEnemyInstanceStore"/>, backed by the <c>EnemyInstances</c> and
/// <c>EnemyParticipants</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// Enemies are materialised into <see cref="SpawnedEnemy"/> views per call. There is deliberately no cache yet:
/// a stale health value is a correctness risk and these are single indexed lookups. If measurements later justify
/// a cache it belongs <em>inside</em> this class (invalidate on write, check <c>Version</c> on read) so no caller
/// can bypass it.
/// </para>
/// <para>
/// Death is owned entirely by <see cref="TryMarkDeadAsync"/>: <see cref="SaveAsync"/> only syncs health and
/// participants, so two players landing the killing blow cannot both award rewards.
/// </para>
/// <para>
/// Respawns are applied lazily when a location is read (<see cref="SettleLocationAsync"/>), which keeps the world
/// correct without a background timer. D3 (#34) adds the proactive sweeper and the <c>EnemySpawned</c> broadcast.
/// </para>
/// </remarks>
public class EfCoreEnemyInstanceStore : IEnemyInstanceStore
{
    private readonly ApplicationDbContext _db;
    private readonly ActorPoolResolver _archetypes;
    private readonly EnemyWorldOptions _options;
    private readonly ILogger<EfCoreEnemyInstanceStore> _logger;

    /// <summary>Initializes a new instance of <see cref="EfCoreEnemyInstanceStore"/>.</summary>
    /// <param name="db">Server database context.</param>
    /// <param name="archetypes">Resolver used to rebuild an enemy's archetype template from content.</param>
    /// <param name="options">World tuning (respawn delay and sweep sizing).</param>
    /// <param name="logger">Logger instance.</param>
    public EfCoreEnemyInstanceStore(
        ApplicationDbContext db,
        ActorPoolResolver archetypes,
        IOptions<EnemyWorldOptions> options,
        ILogger<EfCoreEnemyInstanceStore> logger)
    {
        _db = db;
        _archetypes = archetypes;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpawnedEnemy>> GetAliveOrSpawnAsync(
        string zoneGroup,
        string locationSlug,
        Func<CancellationToken, Task<List<SpawnedEnemy>>> spawnRoster,
        Guid? zoneInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        await SettleLocationAsync(zoneGroup, locationSlug, cancellationToken);

        var rows = await LoadLocationAsync(zoneGroup, locationSlug, cancellationToken);

        if (rows.Count == 0)
        {
            var spawned = await spawnRoster(cancellationToken);
            if (spawned.Count == 0)
            {
                // Nothing to spawn and nothing stored: the caller decides what to tell the player.
                return [];
            }

            foreach (var enemy in spawned)
            {
                _db.EnemyInstances.Add(ToEntity(enemy, zoneGroup, locationSlug, zoneInstanceId));
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another instance spawned this location first. Keep the stored roster rather than ours.
                _db.ChangeTracker.Clear();
                _logger.LogWarning(
                    ex,
                    "Lost the spawn race at {ZoneGroup}/{LocationSlug}; using the roster already stored",
                    zoneGroup,
                    locationSlug);
            }

            rows = await LoadLocationAsync(zoneGroup, locationSlug, cancellationToken);
        }

        return await ToSpawnedAsync(rows.Where(row => row.IsAlive), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ExtendClearedRespawnsAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default)
    {
        var dead = await _db.EnemyInstances
            .Where(enemy =>
                enemy.ZoneGroup == zoneGroup &&
                enemy.LocationSlug == locationSlug &&
                !enemy.IsAlive)
            .ToListAsync(cancellationToken);

        if (dead.Count == 0)
            return 0;

        var quietUntil = DateTimeOffset.UtcNow + _options.ClearedRoomRespawnDelay;

        foreach (var enemy in dead)
        {
            // A floor, not an addition: never pull a later timer backwards, and never leave the room quieter
            // for less than the cleared-room window.
            if (enemy.RespawnAt is null || enemy.RespawnAt < quietUntil)
                enemy.RespawnAt = quietUntil;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return dead.Count;
    }

    /// <inheritdoc />
    public Task<int> CountAliveAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default) =>
        _db.EnemyInstances.CountAsync(
            enemy =>
                enemy.ZoneGroup == zoneGroup &&
                enemy.LocationSlug == locationSlug &&
                enemy.IsAlive,
            cancellationToken);

    /// <inheritdoc />
    public async Task<SpawnedEnemy?> TryGetAsync(Guid enemyId, CancellationToken cancellationToken = default)
    {
        var row = await _db.EnemyInstances
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == enemyId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        var materialised = await ToSpawnedAsync([row], cancellationToken);
        return materialised.Count == 0 ? null : materialised[0];
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        SpawnedEnemy enemy,
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.EnemyInstances
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == enemy.Id, cancellationToken);

        if (row is null)
        {
            _logger.LogError(
                "Cannot persist enemy {EnemyId} at {ZoneGroup}/{LocationSlug}: the row no longer exists",
                enemy.Id,
                zoneGroup,
                locationSlug);
            return;
        }

        // Health only. Whether the enemy is alive is decided by TryMarkDeadAsync, so the death transition stays
        // atomic and cannot be lost by a concurrent save.
        row.CurrentHealth = enemy.CurrentHealth;
        row.Version++;

        var now = DateTimeOffset.UtcNow;
        var original = row.Participants.ToList();

        foreach (var characterId in enemy.Participants.Distinct())
        {
            if (original.Any(p => p.CharacterId == characterId))
            {
                continue;
            }

            row.Participants.Add(new EnemyParticipant
            {
                EnemyInstanceId = row.Id,
                CharacterId = characterId,
                LastAttackedAt = now,
            });
        }

        foreach (var (characterId, damage) in enemy.DamageContributions)
        {
            var participant = row.Participants.FirstOrDefault(p => p.CharacterId == characterId);
            if (participant is null)
            {
                participant = new EnemyParticipant
                {
                    EnemyInstanceId = row.Id,
                    CharacterId = characterId,
                    LastAttackedAt = now,
                };
                row.Participants.Add(participant);
            }

            participant.Damage = damage;
            participant.LastAttackedAt = now;
        }

        // A character who fled or was released is no longer a participant.
        foreach (var participant in original.Where(p => !enemy.Participants.Contains(p.CharacterId)))
        {
            row.Participants.Remove(participant);
            _db.EnemyParticipants.Remove(participant);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Someone else wrote this enemy between our read and our write. Drop our tracked copy so the next
            // request reads the winner's state; D4 (#35) adds the coordination that removes this window.
            _db.ChangeTracker.Clear();
            _logger.LogWarning(
                ex,
                "Concurrent write to enemy {EnemyId}; discarded this update and will reload on the next turn",
                enemy.Id);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogError(ex, "Failed to persist enemy {EnemyId}", enemy.Id);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryMarkDeadAsync(Guid enemyId, CancellationToken cancellationToken = default)
    {
        var respawnAt = DateTimeOffset.UtcNow + _options.RespawnDelay;

        // Single conditional UPDATE: exactly one caller flips IsAlive, so rewards are distributed once.
        var affected = await _db.EnemyInstances
            .Where(e => e.Id == enemyId && e.IsAlive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.IsAlive, false)
                    .SetProperty(e => e.RespawnAt, respawnAt)
                    .SetProperty(e => e.Version, e => e.Version + 1),
                cancellationToken);

        // ExecuteUpdate bypasses the change tracker; drop any stale copy so the next read in this scope sees the
        // row as it now is.
        DetachTracked([enemyId]);

        return affected == 1;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid enemyId, CancellationToken cancellationToken = default) =>
        _db.EnemyInstances
            .Where(e => e.Id == enemyId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnemyRespawnedEvent>> SweepExpiredRespawnsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var isPostgres = _db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        // On PostgreSQL the batch is claimed with SKIP LOCKED inside a transaction, so several sweeper instances
        // never revive the same row twice. SQLite (tests) has neither the clause nor the concurrency to guard.
        IDbContextTransaction? transaction = null;
        if (isPostgres)
        {
            transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var due = isPostgres
                ? await _db.EnemyInstances
                    .FromSql($"""
                        SELECT * FROM "EnemyInstances"
                        WHERE "IsAlive" = false AND "RespawnAt" IS NOT NULL AND "RespawnAt" <= {now}
                        ORDER BY "RespawnAt"
                        LIMIT {batchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                : await _db.EnemyInstances
                    .AsNoTracking()
                    .Where(e => !e.IsAlive && e.RespawnAt != null && e.RespawnAt <= now)
                    .OrderBy(e => e.RespawnAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

            if (due.Count == 0)
            {
                return [];
            }

            var ids = due.Select(e => e.Id).ToList();

            // The sweeper can beat a player to a legacy row, so it reconciles the batch before reviving it: a row
            // that came back on the flat template stats would live one more whole life on them.
            var reconciled = await ReconcileStaleStatsAsync(due, cancellationToken);

            await _db.EnemyInstances
                .Where(e => ids.Contains(e.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.IsAlive, true)
                        .SetProperty(e => e.CurrentHealth, e => e.MaxHealth)
                        .SetProperty(e => e.RespawnAt, (DateTimeOffset?)null)
                        .SetProperty(e => e.Version, e => e.Version + 1),
                    cancellationToken);

            await _db.EnemyParticipants
                .Where(p => ids.Contains(p.EnemyInstanceId))
                .ExecuteDeleteAsync(cancellationToken);

            DetachTracked(ids);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogDebug("Respawn sweep revived {Count} enemy row(s)", due.Count);

            return due
                .Select(e =>
                {
                    // The row was revived from the column, so report what the column now holds rather than the
                    // pre-reconciliation snapshot this batch was read with.
                    var maxHealth = reconciled.TryGetValue(e.Id, out var template)
                        ? Math.Max(1, template.MaxHealth)
                        : e.MaxHealth;

                    return new EnemyRespawnedEvent(
                        e.Id,
                        e.ZoneGroup,
                        e.LocationSlug,
                        e.Name,
                        e.Level,
                        maxHealth,
                        maxHealth);
                })
                .ToList();
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Brings a location's rows back in line with the world: zero-health enemies that are still flagged alive are
    /// killed (a crash between saving health and the death transition), enemy rows written before the archetype
    /// stats were projected onto spawns are rebuilt from their archetype, and enemies whose respawn time has passed
    /// come back at full health with their participants cleared.
    /// </summary>
    /// <param name="zoneGroup">SignalR zone group.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SettleLocationAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var respawnDelay = _options.RespawnDelay;

        var brokenRows = await _db.EnemyInstances
            .Where(e => e.ZoneGroup == zoneGroup && e.LocationSlug == locationSlug && e.IsAlive && e.CurrentHealth <= 0)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (brokenRows.Count > 0)
        {
            _logger.LogError(
                "{Count} enemy row(s) at {ZoneGroup}/{LocationSlug} had zero health but were still flagged alive; marking them dead",
                brokenRows.Count,
                zoneGroup,
                locationSlug);

            await _db.EnemyInstances
                .Where(e => brokenRows.Contains(e.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.IsAlive, false)
                        .SetProperty(e => e.RespawnAt, now + respawnDelay)
                        .SetProperty(e => e.Version, e => e.Version + 1),
                    cancellationToken);

            DetachTracked(brokenRows);
        }

        // Rows written before the archetype stats were projected onto spawns would keep the flat template stats for
        // the rest of their life — every read, save and respawn takes the stored numbers. Reconcile them before
        // anything is revived, so a respawn comes back at the archetype's maximum rather than the stored one.
        var staleRows = await _db.EnemyInstances
            .AsNoTracking()
            .Where(e => e.ZoneGroup == zoneGroup && e.LocationSlug == locationSlug &&
                        e.StatsVersion < EnemyInstance.CurrentStatsVersion)
            .ToListAsync(cancellationToken);

        if (staleRows.Count > 0)
        {
            await ReconcileStaleStatsAsync(staleRows, cancellationToken);
        }

        var expired = await _db.EnemyInstances
            .Where(e => e.ZoneGroup == zoneGroup && e.LocationSlug == locationSlug &&
                        !e.IsAlive && e.RespawnAt != null && e.RespawnAt <= now)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        await _db.EnemyInstances
            .Where(e => expired.Contains(e.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.IsAlive, true)
                    .SetProperty(e => e.CurrentHealth, e => e.MaxHealth)
                    .SetProperty(e => e.RespawnAt, (DateTimeOffset?)null)
                    .SetProperty(e => e.Version, e => e.Version + 1),
                cancellationToken);

        DetachTracked(expired);

        // A respawned enemy starts with a clean slate.
        await _db.EnemyParticipants
            .Where(p => expired.Contains(p.EnemyInstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug(
            "Respawned {Count} expired enemy row(s) at {ZoneGroup}/{LocationSlug}",
            expired.Count,
            zoneGroup,
            locationSlug);
    }

    /// <summary>
    /// Rebuilds the stored combat stats of rows written before the archetype stats were projected onto spawns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is stale when its <see cref="EnemyInstance.StatsVersion"/> is below
    /// <see cref="EnemyInstance.CurrentStatsVersion"/>. That is how "created before the deploy" is known without
    /// guessing from timestamps or values: the column did not exist when those rows were written, so the migration
    /// that adds it backfills zero, while every spawn from now on stamps the current version.
    /// </para>
    /// <para>
    /// Max health and base XP are deterministic, so they are refreshed for stale rows whether the enemy is alive or
    /// dead, with <see cref="EnemyInstance.CurrentHealth"/> clamped to the new maximum. Gold is not: the column is
    /// the amount the kill path awards, so a living row keeps the value it was born with and only a dead row — which
    /// is between fights, its previous award already paid — is re-rolled from the archetype's range. Dead rows are
    /// also the only rows stamped, because only they are fully reconciled; a living row stays stale, so the reward
    /// is refreshed at its first death, and the deterministic update matches nothing once the row has converged.
    /// </para>
    /// </remarks>
    /// <param name="rows">Rows that may be stale; rows already at the current version are ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The archetype each reconciled row was rebuilt from, keyed by enemy id.</returns>
    private async Task<Dictionary<Guid, Enemy>> ReconcileStaleStatsAsync(
        IReadOnlyCollection<EnemyInstance> rows,
        CancellationToken cancellationToken)
    {
        var reconciled = new Dictionary<Guid, Enemy>();
        var stale = rows.Where(row => row.StatsVersion < EnemyInstance.CurrentStatsVersion).ToList();

        if (stale.Count == 0)
        {
            return reconciled;
        }

        foreach (var group in stale.GroupBy(row => row.ArchetypeSlug, StringComparer.OrdinalIgnoreCase))
        {
            var template = await _archetypes.ResolveArchetypeAsync(group.Key);
            if (template is null)
            {
                // Same rule as a read: missing content is reported, never substituted, and the row keeps what it has.
                _logger.LogError(
                    "Cannot reconcile the stored stats of {Count} enemy row(s): archetype '{ArchetypeSlug}' is no longer in the catalogue",
                    group.Count(),
                    group.Key);
                continue;
            }

            var maxHealth = Math.Max(1, template.MaxHealth);
            var baseXp = Math.Max(1, template.XP);
            var ids = group.Select(row => row.Id).ToList();

            foreach (var row in group)
            {
                reconciled[row.Id] = template;
            }

            // Only the rows that actually differ: a living row that has already converged stays un-stamped, and
            // rewriting it on every read of the location would be a write per read for no change.
            await _db.EnemyInstances
                .Where(e => ids.Contains(e.Id) &&
                            e.StatsVersion < EnemyInstance.CurrentStatsVersion &&
                            (e.MaxHealth != maxHealth || e.BaseXp != baseXp || e.CurrentHealth > maxHealth))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.MaxHealth, maxHealth)
                        .SetProperty(e => e.BaseXp, baseXp)
                        .SetProperty(
                            e => e.CurrentHealth,
                            e => e.CurrentHealth > maxHealth ? maxHealth : e.CurrentHealth)
                        .SetProperty(e => e.Version, e => e.Version + 1),
                    cancellationToken);

            var deadIds = group.Where(row => !row.IsAlive).Select(row => row.Id).ToList();
            if (deadIds.Count == 0)
            {
                continue;
            }

            await _db.EnemyInstances
                .Where(e => deadIds.Contains(e.Id) &&
                            !e.IsAlive &&
                            e.StatsVersion < EnemyInstance.CurrentStatsVersion)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.GoldReward, Math.Max(0, template.GoldReward))
                        .SetProperty(e => e.StatsVersion, EnemyInstance.CurrentStatsVersion)
                        .SetProperty(e => e.Version, e => e.Version + 1),
                    cancellationToken);

            _logger.LogInformation(
                "Reconciled {Count} pre-projection enemy row(s) of archetype '{ArchetypeSlug}' to its newest stats",
                deadIds.Count,
                group.Key);
        }

        // ExecuteUpdate bypasses the change tracker, so drop any tracked copy and let the next read see the row.
        DetachTracked(stale.Select(row => row.Id));

        return reconciled;
    }

    /// <summary>
    /// Detaches tracked enemy rows that were changed behind the change tracker's back by
    /// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, so later reads in the same scope cannot serve stale values.
    /// </summary>
    /// <param name="enemyIds">Enemy ids whose tracked copies must be dropped.</param>
    private void DetachTracked(IEnumerable<Guid> enemyIds)
    {
        var ids = enemyIds.ToHashSet();
        if (ids.Count == 0)
        {
            return;
        }

        foreach (var entry in _db.ChangeTracker.Entries<EnemyInstance>()
                     .Where(entry => ids.Contains(entry.Entity.Id))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    // Reads are projected into SpawnedEnemy views, so they never need tracking — and must not be, or an
    // ExecuteUpdate earlier in the same scope would be masked by the stale tracked copy.
    private Task<List<EnemyInstance>> LoadLocationAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken) =>
        _db.EnemyInstances
            .AsNoTracking()
            .Include(e => e.Participants)
            .Where(e => e.ZoneGroup == zoneGroup && e.LocationSlug == locationSlug)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<SpawnedEnemy>> ToSpawnedAsync(
        IEnumerable<EnemyInstance> rows,
        CancellationToken cancellationToken)
    {
        var templates = new Dictionary<string, Enemy?>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SpawnedEnemy>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A placed actor resolves through its instance, which is what carries the overrides; a pool spawn resolves
            // through its archetype. Keying the cache by the instance slug keeps the two from colliding when the same
            // archetype stands at one location as both.
            var templateKey = row.InstanceSlug ?? row.ArchetypeSlug;

            if (!templates.TryGetValue(templateKey, out var template))
            {
                template = row.InstanceSlug is { } instanceSlug
                    ? await _archetypes.ResolveInstanceTemplateAsync(instanceSlug)
                    : await _archetypes.ResolveArchetypeAsync(row.ArchetypeSlug);

                if (template is null)
                {
                    _logger.LogError(
                        row.InstanceSlug is null
                            ? "Enemy {EnemyId} references missing actor archetype '{ArchetypeSlug}'; it stays fightable but has no abilities"
                            : "Enemy {EnemyId} references missing actor instance '{ArchetypeSlug}'; it stays fightable but has no abilities",
                        row.Id,
                        row.InstanceSlug ?? row.ArchetypeSlug);
                }

                templates[templateKey] = template;
            }

            var enemy = new SpawnedEnemy
            {
                Id = row.Id,
                ZoneGroup = row.ZoneGroup,
                LocationSlug = row.LocationSlug,
                ZoneInstanceId = row.ZoneInstanceId,
                ArchetypeSlug = row.ArchetypeSlug,
                InstanceSlug = row.InstanceSlug,
                Name = row.Name,
                Level = row.Level,
                CurrentHealth = row.CurrentHealth,
                MaxHealth = row.MaxHealth,
                BaseXp = row.BaseXp,
                GoldReward = row.GoldReward,
                Template = template ?? new Enemy(),
            };

            foreach (var participant in row.Participants)
            {
                enemy.Participants.Add(participant.CharacterId);
                if (participant.Damage > 0)
                {
                    enemy.DamageContributions[participant.CharacterId] = participant.Damage;
                }
            }

            result.Add(enemy);
        }

        return result;
    }

    private static EnemyInstance ToEntity(
        SpawnedEnemy enemy,
        string zoneGroup,
        string locationSlug,
        Guid? zoneInstanceId) =>
        new()
        {
            Id = enemy.Id,
            ZoneGroup = zoneGroup,
            ZoneInstanceId = zoneInstanceId,
            LocationSlug = locationSlug,
            ArchetypeSlug = enemy.ArchetypeSlug,
            InstanceSlug = enemy.InstanceSlug,
            Name = enemy.Name,
            Level = enemy.Level,
            CurrentHealth = enemy.CurrentHealth,
            MaxHealth = enemy.MaxHealth,
            BaseXp = enemy.BaseXp,
            GoldReward = enemy.GoldReward,
            // Built from the current spawn projection, so there is nothing to reconcile: the reconciliation exists
            // only for rows written before the archetype stats reached the template.
            StatsVersion = EnemyInstance.CurrentStatsVersion,
            IsAlive = enemy.IsAlive,
            SpawnedAt = DateTimeOffset.UtcNow,
            Version = 1,
        };
}
