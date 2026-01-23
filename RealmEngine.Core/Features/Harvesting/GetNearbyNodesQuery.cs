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
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string BiomeType { get; set; } = string.Empty;

    public List<NearbyNodeInfo> Nodes { get; set; } = new();
    public int TotalNodes { get; set; }
    public int HarvestablNodes { get; set; }
    public int DepletedNodes { get; set; }
}

/// <summary>
/// Summary info about a nearby node.
/// </summary>
public class NearbyNodeInfo
{
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string MaterialTier { get; set; } = string.Empty;
    public int HealthPercent { get; set; }
    public bool CanHarvest { get; set; }
    public string StateDescription { get; set; } = string.Empty;
    public bool IsRichNode { get; set; }
}
