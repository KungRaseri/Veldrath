using System.Collections.Concurrent;

namespace RealmUnbound.Server.Features.Characters.Combat;

/// <summary>
/// In-process store of all live enemy instances, keyed by zone location.
/// Thread-safe reads and roster-level locks protect concurrent HP mutations.
/// </summary>
public static class ZoneLocationEnemyStore
{
    private static readonly ConcurrentDictionary<string, List<SpawnedEnemy>> _enemies = new();

    /// <summary>
    /// Computes the composite store key from a SignalR zone group name and a location slug.
    /// </summary>
    /// <param name="zoneGroup">The SignalR group name (e.g. <c>"zone:fenwick-crossing"</c>).</param>
    /// <param name="locationSlug">The zone location slug (e.g. <c>"town-square"</c>).</param>
    /// <returns>A key in the form <c>"zoneGroup/locationSlug"</c>.</returns>
    public static string MakeKey(string zoneGroup, string locationSlug) =>
        $"{zoneGroup}/{locationSlug}";

    /// <summary>Returns <see langword="true"/> when a roster exists for the key (even if all are dead).</summary>
    public static bool HasRoster(string key) => _enemies.ContainsKey(key);

    /// <summary>Returns all enemies for the given key, or an empty list when no roster exists.</summary>
    public static List<SpawnedEnemy> GetOrEmpty(string key) =>
        _enemies.TryGetValue(key, out var list) ? list : [];

    /// <summary>Returns only the living enemies at the given key.</summary>
    public static List<SpawnedEnemy> GetAlive(string key) =>
        GetOrEmpty(key).Where(e => e.IsAlive).ToList();

    /// <summary>Tries to retrieve a specific enemy by instance ID at the given key.</summary>
    public static SpawnedEnemy? TryGetEnemy(string key, Guid enemyId) =>
        GetOrEmpty(key).FirstOrDefault(e => e.Id == enemyId);

    /// <summary>
    /// Atomically adds a roster for the given key only when there is no existing entry.
    /// </summary>
    /// <returns><see langword="true"/> if the roster was added; <see langword="false"/> if already present.</returns>
    public static bool TryAddRoster(string key, List<SpawnedEnemy> roster) =>
        _enemies.TryAdd(key, roster);

    /// <summary>Appends a single enemy to an existing roster (thread-safe).</summary>
    public static void AddEnemy(string key, SpawnedEnemy enemy)
    {
        var list = _enemies.GetOrAdd(key, _ => []);
        lock (list) list.Add(enemy);
    }

    /// <summary>Removes a specific enemy from the roster at the given key.</summary>
    public static void RemoveEnemy(string key, Guid enemyId)
    {
        if (!_enemies.TryGetValue(key, out var list)) return;
        lock (list) list.RemoveAll(e => e.Id == enemyId);
    }
}
