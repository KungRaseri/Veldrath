namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Directed travel edge between two adjacent regions.
/// Bidirectional travel is represented as two rows (A→B and B→A).
/// </summary>
public class RegionConnection
{
    /// <summary>Slug of the origin region.</summary>
    public string FromRegionId { get; set; } = string.Empty;

    /// <summary>Slug of the destination region.</summary>
    public string ToRegionId { get; set; } = string.Empty;

    // Navigation
    /// <summary>Origin region.</summary>
    public Region FromRegion { get; set; } = null!;

    /// <summary>Destination region.</summary>
    public Region ToRegion { get; set; } = null!;
}
