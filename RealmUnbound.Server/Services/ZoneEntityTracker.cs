using System.Collections.Concurrent;

namespace RealmUnbound.Server.Services;

/// <summary>
/// In-memory snapshot of all live entities (enemies, NPCs) currently spawned in active zones.
/// Populated when the first player enters a zone; cleared when the last player leaves.
/// Not persisted — enemy positions reset on server restart.
/// </summary>
public interface IZoneEntityTracker
{
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
    public void ClearZone(string zoneId) => _zones.TryRemove(zoneId, out _);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetActiveZoneIds() => [.. _zones.Keys];
}
