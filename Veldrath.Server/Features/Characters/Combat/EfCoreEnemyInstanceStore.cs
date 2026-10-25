using Microsoft.EntityFrameworkCore;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

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
    private readonly ILogger<EfCoreEnemyInstanceStore> _logger;

    /// <summary>Initializes a new instance of <see cref="EfCoreEnemyInstanceStore"/>.</summary>
    /// <param name="db">Server database context.</param>
    /// <param name="archetypes">Resolver used to rebuild an enemy's archetype template from content.</param>
    /// <param name="logger">Logger instance.</param>
    public EfCoreEnemyInstanceStore(
        ApplicationDbContext db,
        ActorPoolResolver archetypes,
        ILogger<EfCoreEnemyInstanceStore> logger)
    {
        _db = db;
        _archetypes = archetypes;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpawnedEnemy>> GetAliveOrSpawnAsync(
        string zoneGroup,
        string locationSlug,
        Func<CancellationToken, Task<List<SpawnedEnemy>>> spawnRoster,
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
                _db.EnemyInstances.Add(ToEntity(enemy, zoneGroup, locationSlug));
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
    public async Task<bool> TryMarkDeadAsync(
        Guid enemyId,
        TimeSpan respawnDelay,
        CancellationToken cancellationToken = default)
    {
        var respawnAt = DateTimeOffset.UtcNow + respawnDelay;

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

    /// <summary>
    /// Brings a location's rows back in line with the world: zero-health enemies that are still flagged alive are
    /// killed (a crash between saving health and the death transition), and enemies whose respawn time has passed
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
        var respawnDelay = TimeSpan.FromSeconds(60);

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

            if (!templates.TryGetValue(row.ArchetypeSlug, out var template))
            {
                template = await _archetypes.ResolveArchetypeAsync(row.ArchetypeSlug);
                if (template is null)
                {
                    _logger.LogError(
                        "Enemy {EnemyId} references missing actor archetype '{ArchetypeSlug}'; it stays fightable but has no abilities",
                        row.Id,
                        row.ArchetypeSlug);
                }

                templates[row.ArchetypeSlug] = template;
            }

            var enemy = new SpawnedEnemy
            {
                Id = row.Id,
                ArchetypeSlug = row.ArchetypeSlug,
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

    private static EnemyInstance ToEntity(SpawnedEnemy enemy, string zoneGroup, string locationSlug) =>
        new()
        {
            Id = enemy.Id,
            ZoneGroup = zoneGroup,
            LocationSlug = locationSlug,
            ArchetypeSlug = enemy.ArchetypeSlug,
            Name = enemy.Name,
            Level = enemy.Level,
            CurrentHealth = enemy.CurrentHealth,
            MaxHealth = enemy.MaxHealth,
            BaseXp = enemy.BaseXp,
            GoldReward = enemy.GoldReward,
            IsAlive = enemy.IsAlive,
            SpawnedAt = DateTimeOffset.UtcNow,
            Version = 1,
        };
}
