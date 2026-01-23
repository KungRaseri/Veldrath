using MediatR;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Query to get all harvestable nodes near the character's location.
/// </summary>
public class GetNearbyNodesQuery : IRequest<NearbyNodesResult>
{
    /// <summary>
    /// Character name requesting nearby nodes.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Optional location ID (null = use character's current location).
    /// </summary>
    public string? LocationId { get; set; }

    /// <summary>
    /// Filter by node type (ore_vein, tree, herb_patch, etc.).
    /// </summary>
    public string? NodeTypeFilter { get; set; }

    /// <summary>
    /// Only show nodes that can currently be harvested (not depleted).
    /// </summary>
    public bool OnlyHarvestable { get; set; } = true;
}

/// <summary>
/// Result containing nearby harvestable nodes.
/// </summary>
public class NearbyNodesResult
{
    /// <summary>
    /// Whether the query was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The location ID where nodes were queried.
    /// </summary>
    public string LocationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the location.
    /// </summary>
    public string LocationName { get; set; } = string.Empty;
    
    /// <summary>
    /// Biome type of the location.
    /// </summary>
    public string BiomeType { get; set; } = string.Empty;

    /// <summary>
    /// List of nearby harvestable nodes.
    /// </summary>
    public List<NearbyNodeInfo> Nodes { get; set; } = new();
    
    /// <summary>
    /// Total number of nodes found.
    /// </summary>
    public int TotalNodes { get; set; }
    
    /// <summary>
    /// Number of nodes that can be harvested.
    /// </summary>
    public int HarvestablNodes { get; set; }
    
    /// <summary>
    /// Number of depleted nodes.
    /// </summary>
    public int DepletedNodes { get; set; }
}

/// <summary>
/// Summary info about a nearby node.
/// </summary>
public class NearbyNodeInfo
{
    /// <summary>
    /// Unique identifier for the node.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the node.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the node (e.g., copper_vein, oak_tree).
    /// </summary>
    public string NodeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Material tier of the node.
    /// </summary>
    public string MaterialTier { get; set; } = string.Empty;
    
    /// <summary>
    /// Health percentage (0-100).
    /// </summary>
    public int HealthPercent { get; set; }
    
    /// <summary>
    /// Whether the node can be harvested.
    /// </summary>
    public bool CanHarvest { get; set; }
    
    /// <summary>
    /// Description of the node's current state.
    /// </summary>
    public string StateDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a rich node with bonus drops.
    /// </summary>
    public bool IsRichNode { get; set; }
}
