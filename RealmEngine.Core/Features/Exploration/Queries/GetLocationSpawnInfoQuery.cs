using MediatR;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to get enemy spawn information for the current location.
/// Used by Godot to determine which enemies can appear during exploration.
/// </summary>
public record GetLocationSpawnInfoQuery(string? LocationName = null) : IRequest<LocationSpawnInfoDto>;

/// <summary>
/// DTO containing location-specific spawn rules and loot information.
/// Consumed by Godot UI for combat encounters and loot generation.
/// </summary>
public record LocationSpawnInfoDto
{
    /// <summary>
    /// Whether the query was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The location name.
    /// </summary>
    public string? LocationName { get; init; }

    /// <summary>
    /// The location type (town, dungeon, wilderness).
    /// </summary>
    public string? LocationType { get; init; }

    /// <summary>
    /// Recommended player level range for this location (e.g., "1-5", "10-15").
    /// </summary>
    public string? RecommendedLevel { get; init; }

    /// <summary>
    /// Danger rating (0-100, where 0 is safe and 100 is extremely dangerous).
    /// </summary>
    public int DangerRating { get; init; }

    /// <summary>
    /// Map of enemy category to spawn weight.
    /// Higher weights mean more likely to spawn.
    /// Example: { "wolves": 50, "bandits": 30, "goblins": 20 }
    /// </summary>
    public Dictionary<string, int> EnemySpawnWeights { get; init; } = new();

    /// <summary>
    /// List of specific enemy reference IDs that can spawn here.
    /// JSON references like "@enemies/beasts/wolves:*" or "@enemies/humanoids/bandits:Bandit"
    /// </summary>
    public List<string> EnemyReferences { get; init; } = new();

    /// <summary>
    /// List of loot/reward reference IDs available at this location.
    /// JSON references like "@items/weapons:*" or "@items/consumables:*"
    /// </summary>
    public List<string> LootReferences { get; init; } = new();

    /// <summary>
    /// List of NPC IDs present at this location.
    /// </summary>
    public List<string> AvailableNPCs { get; init; } = new();

    /// <summary>
    /// List of merchant NPC IDs at this location.
    /// Subset of AvailableNPCs that have merchant functionality.
    /// </summary>
    public List<string> AvailableMerchants { get; init; } = new();

    /// <summary>
    /// Additional location metadata (terrain, features, etc.).
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
