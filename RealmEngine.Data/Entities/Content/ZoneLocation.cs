namespace RealmEngine.Data.Entities;

/// <summary>
/// A named location within a Zone — a point-of-interest for content generation.
/// TypeKey = location category (e.g. "dungeons", "locations", "environments").
/// </summary>
public class ZoneLocation : ContentBase
{
    /// <summary>The Zone this location belongs to (loose cross-context reference).</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>"dungeon" | "location" | "environment"</summary>
    public string LocationType { get; set; } = string.Empty;

    /// <summary>Size, danger, and level-range statistics.</summary>
    public ZoneLocationStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this location.</summary>
    public ZoneLocationTraits Traits { get; set; } = new();

    /// <summary>Weighted archetype entries that determine what actors can spawn at this location.</summary>
    public IList<ActorPoolEntry> ActorPool { get; set; } = [];
}

/// <summary>Size, danger, and level-range statistics owned by a ZoneLocation.</summary>
public class ZoneLocationStats
{
    /// <summary>Relative size of the location (1–10 scale).</summary>
    public int? Size { get; set; }
    /// <summary>Overall threat level of the location (1–10 scale).</summary>
    public int? DangerLevel { get; set; }
    /// <summary>Number of inhabitants (NPCs, enemies) that can spawn here.</summary>
    public int? Population { get; set; }
    /// <summary>Minimum character level for meaningful challenge here.</summary>
    public int? MinLevel { get; set; }
    /// <summary>Maximum character level for meaningful challenge here.</summary>
    public int? MaxLevel { get; set; }
}

/// <summary>Boolean trait flags classifying a ZoneLocation.</summary>
public class ZoneLocationTraits
{
    /// <summary>True if the location is inside a building or cave.</summary>
    public bool? IsIndoor { get; set; }
    /// <summary>True if a merchant NPC is present at this location.</summary>
    public bool? HasMerchant { get; set; }
    /// <summary>True if the location is not shown on the map until the player visits it.</summary>
    public bool? IsDiscoverable { get; set; }
    /// <summary>True if this location is a dungeon with structured encounters.</summary>
    public bool? IsDungeon { get; set; }
    /// <summary>True if this location is a populated settlement.</summary>
    public bool? IsTown { get; set; }
    /// <summary>True if this location is completely hidden until an unlock condition is met.</summary>
    public bool? IsHidden { get; set; }
    /// <summary>Unlock trigger type: "skill_check_passive" | "skill_check_active" | "quest" | "item" | "achievement" | "manual".</summary>
    public string? UnlockType { get; set; }
    /// <summary>Slug or identifier of the quest, item, or achievement required to unlock this location.</summary>
    public string? UnlockKey { get; set; }
    /// <summary>Character level threshold required for passive skill-check discovery (and target DC for active search rolls).</summary>
    public int? DiscoverThreshold { get; set; }
}

/// <summary>A weighted entry in an actor pool for a zone location.</summary>
public class ActorPoolEntry
{
    /// <summary>Slug of the archetype that can spawn at this location.</summary>
    public string ArchetypeSlug { get; set; } = string.Empty;

    /// <summary>Relative spawn weight for this archetype (higher = more likely).</summary>
    public int Weight { get; set; } = 1;
}

/// <summary>A directed traversal edge linking one ZoneLocation to another location or zone.</summary>
public class ZoneLocationConnection
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Slug of the ZoneLocation that contains this exit.</summary>
    public string FromLocationSlug { get; set; } = string.Empty;

    /// <summary>Slug of the destination ZoneLocation, or <see langword="null"/> when the destination is a whole zone.</summary>
    public string? ToLocationSlug { get; set; }

    /// <summary>Zone slug to enter when this connection is traversed (set when the destination is an entire zone).</summary>
    public string? ToZoneId { get; set; }

    /// <summary>"path" | "portal" | "dungeon_entrance" | "secret_passage"</summary>
    public string ConnectionType { get; set; } = "path";

    /// <summary>False when the connection is temporarily blocked (quest gate, locked door, etc.).</summary>
    public bool IsTraversable { get; set; } = true;

    /// <summary>True when this connection is hidden and only returned for characters who have unlocked it.</summary>
    public bool IsHidden { get; set; } = false;
}
