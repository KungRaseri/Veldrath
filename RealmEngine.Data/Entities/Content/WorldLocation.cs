namespace RealmEngine.Data.Entities;

/// <summary>
/// World location. TypeKey = location type (e.g. "environments", "locations", "regions").
/// </summary>
public class WorldLocation : ContentBase
{
    /// <summary>"environment" | "location" | "region"</summary>
    public string LocationType { get; set; } = string.Empty;

    /// <summary>Size, danger, and level-range statistics.</summary>
    public WorldLocationStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this location.</summary>
    public WorldLocationTraits Traits { get; set; } = new();
}

/// <summary>Size, danger, and level-range statistics owned by a WorldLocation.</summary>
public class WorldLocationStats
{
    /// <summary>Relative size of the location (1–10 scale).</summary>
    public int? Size { get; set; }
    /// <summary>Overall threat level of the location (1–10 scale).</summary>
    public int? DangerLevel { get; set; }
    /// <summary>Number of inhabitants (NPCs, enemies) that can spawn here.</summary>
    public int? Population { get; set; }
    /// <summary>Minimum character level for safe travel here.</summary>
    public int? MinLevel { get; set; }
    /// <summary>Maximum character level for meaningful challenge here.</summary>
    public int? MaxLevel { get; set; }
}

/// <summary>Boolean trait flags classifying a WorldLocation.</summary>
public class WorldLocationTraits
{
    /// <summary>True if the location is inside a building or cave.</summary>
    public bool? IsIndoor { get; set; }
    /// <summary>True if a merchant NPC is present at this location.</summary>
    public bool? HasMerchant { get; set; }
    /// <summary>True if player-versus-player combat is allowed here.</summary>
    public bool? PvpEnabled { get; set; }
    /// <summary>True if the location is not shown on the map until the player visits it.</summary>
    public bool? IsDiscoverable { get; set; }
    /// <summary>True if this location is a dungeon with structured encounters.</summary>
    public bool? IsDungeon { get; set; }
    /// <summary>True if this location is a populated settlement.</summary>
    public bool? IsTown { get; set; }
}
