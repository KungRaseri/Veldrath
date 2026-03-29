namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Directed travel edge between two adjacent zones.
/// Bidirectional travel is represented as two rows (A→B and B→A).
/// </summary>
public class ZoneConnection
{
    /// <summary>Slug of the origin zone.</summary>
    public string FromZoneId { get; set; } = string.Empty;

    /// <summary>Slug of the destination zone.</summary>
    public string ToZoneId { get; set; } = string.Empty;

    /// <summary>When <see langword="true"/>, this edge is a hidden/secret passage and must not appear on the world map.</summary>
    public bool IsHidden { get; set; }

    // Navigation
    /// <summary>Origin zone.</summary>
    public Zone FromZone { get; set; } = null!;

    /// <summary>Destination zone.</summary>
    public Zone ToZone { get; set; } = null!;
}
