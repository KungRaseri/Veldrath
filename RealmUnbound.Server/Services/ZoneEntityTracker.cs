using System.Collections.Concurrent;

namespace RealmUnbound.Server.Services;

/// <summary>
/// In-memory snapshot of all live entities (enemies, NPCs) currently spawned in active zones,
/// and the live player positions needed by <c>EnemyAiService</c> for pathfinding.
/// Populated when the first player enters a zone; cleared when the last player leaves.
/// Not persisted — positions reset on server restart.
/// </summary>
public interface IZoneEntityTracker
{
    // ── Entity (enemy/NPC) tracking ──────────────────────────────────────────

    /// <summary>Returns all entity snapshots for the given zone, or an empty list if no entities are tracked.</summary>
    IReadOnlyList<ZoneEntitySnapshot> GetEntities(string zoneId);

    /// <summary>Sets the full entity list for a zone, replacing any previous snapshot.</summary>
    void SetEntities(string zoneId, IEnumerable<ZoneEntitySnapshot> entities);

    /// <summary>Updates the position and direction of a single entity within its zone.</summary>
    /// <returns><see langword="true"/> if the entity was found and updated; <see langword="false"/> if it was not tracked.</returns>
    bool UpdatePosition(string zoneId, Guid entityId, int tileX, int tileY, string direction);

    /// <summary>Removes all tracked entities for the given zone.</summary>
    void ClearZone(string zoneId);

    /// <summary>Returns all zone IDs that currently have entities tracked.</summary>
    IReadOnlyList<string> GetActiveZoneIds();

    // ── Player position tracking ─────────────────────────────────────────────

    /// <summary>Records or updates the tile position of a player character within a zone.</summary>
    void TrackPlayer(string zoneId, Guid characterId, int tileX, int tileY);

    /// <summary>Removes a player's position record from the zone.</summary>
    void UntrackPlayer(string zoneId, Guid characterId);

    /// <summary>Returns the positions of all players currently tracked in the given zone.</summary>
    IReadOnlyList<(Guid CharacterId, int X, int Y)> GetPlayerPositions(string zoneId);

    /// <summary>Returns the IDs of all zones that currently have at least one tracked player.</summary>
    IReadOnlyList<string> GetZonesWithPlayers();

    // ── Zone group name ───────────────────────────────────────────────────────

    /// <summary>Stores the SignalR group name for a zone so background services can broadcast without hub context.</summary>
    void SetZoneGroupName(string zoneId, string groupName);

    /// <summary>Returns the SignalR group name previously stored for the zone, or <see langword="null"/> if not set.</summary>
    string? GetZoneGroupName(string zoneId);
}

/// <summary>Snapshot of a single live entity on the zone tile grid.</summary>
/// <param name="EntityId">Unique instance identifier for this spawn.</param>
/// <param name="EntityType">Broad category: <c>"enemy"</c> or <c>"npc"</c>.</param>
/// <param name="ArchetypeSlug">Content slug that defines this entity's stats and appearance.</param>
/// <param name="SpriteKey">Sprite sheet key used by the client renderer.</param>
/// <param name="TileX">Current tile column (0-based).</param>
/// <param name="TileY">Current tile row (0-based).</param>
/// <param name="Direction">Facing direction: <c>"N"</c>, <c>"S"</c>, <c>"E"</c>, or <c>"W"</c>.</param>
/// <param name="MaxHealth">Maximum hit points for this spawn instance.</param>
/// <param name="CurrentHealth">Current hit points; 0 means defeated.</param>
public record ZoneEntitySnapshot(
    Guid EntityId,
    string EntityType,
    string ArchetypeSlug,
    string SpriteKey,
    int TileX,
    int TileY,
    string Direction,
    int MaxHealth,
    int CurrentHealth)
{
    /// <summary>Returns a copy with an updated tile position and direction.</summary>
    public ZoneEntitySnapshot WithPosition(int tileX, int tileY, string direction)
        => this with { TileX = tileX, TileY = tileY, Direction = direction };
}

/// <summary>Thread-safe, singleton implementation of <see cref="IZoneEntityTracker"/>.</summary>
public class ZoneEntityTracker : IZoneEntityTracker
{
    // zoneId → mutable list protected by list-level locking
    private readonly ConcurrentDictionary<string, List<ZoneEntitySnapshot>> _zones = new(StringComparer.OrdinalIgnoreCase);

    // zoneId → (characterId → (x, y)) — protected by a dedicated lock per zone entry
    private readonly ConcurrentDictionary<string, Dictionary<Guid, (int X, int Y)>> _players = new(StringComparer.OrdinalIgnoreCase);

    // zoneId → SignalR group name
    private readonly ConcurrentDictionary<string, string> _groupNames = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IReadOnlyList<ZoneEntitySnapshot> GetEntities(string zoneId)
    {
        if (!_zones.TryGetValue(zoneId, out var list))
            return [];

        lock (list)
            return [.. list];
    }

    /// <inheritdoc/>
    public void SetEntities(string zoneId, IEnumerable<ZoneEntitySnapshot> entities)
    {
        var newList = new List<ZoneEntitySnapshot>(entities);
        _zones[zoneId] = newList;
    }

    /// <inheritdoc/>
    public bool UpdatePosition(string zoneId, Guid entityId, int tileX, int tileY, string direction)
    {
        if (!_zones.TryGetValue(zoneId, out var list))
            return false;

        lock (list)
        {
            var idx = list.FindIndex(e => e.EntityId == entityId);
            if (idx < 0) return false;
            list[idx] = list[idx].WithPosition(tileX, tileY, direction);
            return true;
        }
    }

    /// <inheritdoc/>
    public void ClearZone(string zoneId)
    {
        _zones.TryRemove(zoneId, out _);
        _players.TryRemove(zoneId, out _);
        _groupNames.TryRemove(zoneId, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetActiveZoneIds() => [.. _zones.Keys];

    // ── Player position tracking ─────────────────────────────────────────────

    /// <inheritdoc/>
    public void TrackPlayer(string zoneId, Guid characterId, int tileX, int tileY)
    {
        var dict = _players.GetOrAdd(zoneId, _ => []);
        lock (dict)
            dict[characterId] = (tileX, tileY);
    }

    /// <inheritdoc/>
    public void UntrackPlayer(string zoneId, Guid characterId)
    {
        if (!_players.TryGetValue(zoneId, out var dict))
            return;
        lock (dict)
            dict.Remove(characterId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<(Guid CharacterId, int X, int Y)> GetPlayerPositions(string zoneId)
    {
        if (!_players.TryGetValue(zoneId, out var dict))
            return [];
        lock (dict)
            return dict.Select(kvp => (kvp.Key, kvp.Value.X, kvp.Value.Y)).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetZonesWithPlayers()
    {
        return _players
            .Where(kvp => { lock (kvp.Value) return kvp.Value.Count > 0; })
            .Select(kvp => kvp.Key)
            .ToList();
    }

    // ── Zone group name ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetZoneGroupName(string zoneId, string groupName) =>
        _groupNames[zoneId] = groupName;

    /// <inheritdoc/>
    public string? GetZoneGroupName(string zoneId) =>
        _groupNames.TryGetValue(zoneId, out var name) ? name : null;
}
