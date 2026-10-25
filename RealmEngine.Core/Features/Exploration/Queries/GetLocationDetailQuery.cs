using MediatR;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to get detailed information about a location, including spawn rules for Godot UI.
/// </summary>
public record GetLocationDetailQuery(string? LocationName = null) : IRequest<LocationDetailDto>;

/// <summary>
/// Detailed location information including spawn rules for Godot integration.
/// </summary>
public record LocationDetailDto
{
    /// <summary>Indicates if the query was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Location identifier.</summary>
    public string LocationId { get; init; } = string.Empty;

    /// <summary>Location name.</summary>
    public string LocationName { get; init; } = string.Empty;

    /// <summary>Location type (town, dungeon, wilderness, environment, region).</summary>
    public string LocationType { get; init; } = string.Empty;

    /// <summary>Description of the location.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Recommended player level for this location.</summary>
    public int RecommendedLevel { get; init; }

    /// <summary>Danger rating (0-10, where 0 is safe and 10 is extremely dangerous).</summary>
    public int DangerRating { get; init; }

    /// <summary>
    /// Enemy spawn weights by category (e.g., "beasts/wolves" -> 30).
    /// Higher weight = more likely to spawn.
    /// </summary>
    public Dictionary<string, int> EnemySpawnWeights { get; init; } = new();

    /// <summary>
    /// All enemy references that can spawn at this location.
    /// Format: "@enemies/category/type:name" or "@enemies/category:*"
    /// </summary>
    public List<string> EnemyReferences { get; init; } = new();

    /// <summary>
    /// Loot table references for items that can be found at this location.
    /// Format: "@items/category/type:*" or "@items/category:name"
    /// </summary>
    public List<string> LootReferences { get; init; } = new();

    /// <summary>
    /// Loot spawn weights by category (e.g., "weapons/swords" -> 20).
    /// Higher weight = more likely to drop.
    /// </summary>
    public Dictionary<string, int> LootSpawnWeights { get; init; } = new();

    /// <summary>NPC IDs or references present at this location.</summary>
    public List<string> AvailableNPCs { get; init; } = new();

    /// <summary>Merchant/shop IDs available at this location.</summary>
    public List<string> AvailableMerchants { get; init; } = new();

    /// <summary>Location features (e.g., "save_point", "fast_travel", "training_area").</summary>
    public List<string> Features { get; init; } = new();

    /// <summary>Parent region name, if this location is part of a larger region.</summary>
    public string? ParentRegion { get; init; }

    /// <summary>Additional location metadata (terrain type, climate, etc.).</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>Error message if Success is false.</summary>
    public string? ErrorMessage { get; init; }
}
