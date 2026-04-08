namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// A named zone (area) that players can inhabit concurrently.
/// Static zones are seeded at DB initialisation. Procedural zones are generated on demand.
/// </summary>
public class Zone
{
    public string Id { get; set; } = string.Empty; // e.g. "fenwick-crossing", "aldenmere"

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ZoneType Type { get; set; } = ZoneType.Wilderness;

    /// <summary>Minimum character level to enter (0 = no restriction).</summary>
    public int MinLevel { get; set; }

    /// <summary>Maximum concurrent players (0 = unlimited).</summary>
    public int MaxPlayers { get; set; }

    /// <summary>True for the zone new characters start in.</summary>
    public bool IsStarter { get; set; }

    /// <summary>True if the zone contains an inn where characters can rest.</summary>
    public bool HasInn { get; set; }

    /// <summary>True if the zone contains a merchant shop.</summary>
    public bool HasMerchant { get; set; }

    /// <summary>True if player-versus-player combat is allowed in this zone.</summary>
    public bool IsPvpEnabled { get; set; }

    /// <summary>False for zones that are unlocked through progression or quests rather than open exploration.</summary>
    public bool IsDiscoverable { get; set; } = true;

    /// <summary>Accumulated gold contributed to the rescue fund from player deaths in this zone.</summary>
    public long RescueFundTotal { get; set; }

    // FK
    /// <summary>The region this zone belongs to.</summary>
    public string RegionId { get; set; } = string.Empty;

    // Navigation
    /// <summary>Parent region.</summary>
    public Region Region { get; set; } = null!;

    /// <summary>Active player sessions currently inside this zone.</summary>
    public ICollection<ZoneSession> Sessions { get; set; } = [];
}

public enum ZoneType
{
    Town,
    Dungeon,
    Wilderness,
}
