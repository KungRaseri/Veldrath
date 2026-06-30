namespace RealmEngine.Data.Entities;

/// <summary>
/// A directed connection between two <see cref="ZoneLocation"/> entities within the same zone
/// or across zones. Bidirectional travel is represented as two rows (A→B and B→A).
/// Soft references — no FK constraints; the navigable connection graph is resolved at query time.
/// </summary>
public class ZoneLocationConnection
{
    /// <summary>Primary key — stable across imports; generated once on first insert.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Slug of the origin <see cref="ZoneLocation"/>.</summary>
    public string FromLocationSlug { get; set; } = string.Empty;

    /// <summary>Slug of the destination <see cref="ZoneLocation"/>.</summary>
    public string ToLocationSlug { get; set; } = string.Empty;

    /// <summary>
    /// Zone ID of the destination location when the connection crosses zone boundaries;
    /// <see langword="null"/> when both locations belong to the same zone.
    /// </summary>
    public string? ToZoneId { get; set; }

    /// <summary>
    /// Region ID of the destination when the connection exits to the region map;
    /// <see langword="null"/> for intra-zone and cross-zone connections.
    /// </summary>
    public string? ToRegionId { get; set; }

    /// <summary>
    /// Type of connection: <c>"walk"</c> for intra-zone pedestrian travel,
    /// <c>"exit"</c> for cross-zone transitions, <c>"region_exit"</c> for region map access.
    /// </summary>
    public string ConnectionType { get; set; } = "walk";

    /// <summary>
    /// <see langword="true"/> if this connection is currently usable by players;
    /// <see langword="false"/> if it is blocked (e.g. by quest state, event, or lock).
    /// </summary>
    public bool IsTraversable { get; set; } = true;
}
