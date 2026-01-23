using MediatR;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Query to get detailed information about a resource node.
/// </summary>
public class InspectNodeQuery : IRequest<NodeInspectionResult>
{
    /// <summary>
    /// Character name performing the inspection.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// ID of the node to inspect.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Result of inspecting a resource node.
/// </summary>
public class NodeInspectionResult
{
    /// <summary>
    /// Whether the inspection was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if inspection failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Node basic info
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the node.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Node type (e.g., copper_vein, oak_tree).
    /// </summary>
    public string NodeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Material tier of the node.
    /// </summary>
    public string MaterialTier { get; set; } = string.Empty;

    // Node state
    /// <summary>
    /// Current health of the node.
    /// </summary>
    public int CurrentHealth { get; set; }
    
    /// <summary>
    /// Maximum health of the node.
    /// </summary>
    public int MaxHealth { get; set; }
    
    /// <summary>
    /// Health percentage (0-100).
    /// </summary>
    public int HealthPercent { get; set; }
    
    /// <summary>
    /// Current state of the node.
    /// </summary>
    public NodeState State { get; set; }
    
    /// <summary>
    /// Whether the node can be harvested.
    /// </summary>
    public bool CanHarvest { get; set; }

    // Requirements
    /// <summary>
    /// Minimum tool tier required.
    /// </summary>
    public int MinToolTier { get; set; }
    
    /// <summary>
    /// Required tool type (e.g., pickaxe, axe).
    /// </summary>
    public string RequiredToolType { get; set; } = string.Empty;
    
    /// <summary>
    /// Required skill name.
    /// </summary>
    public string RequiredSkill { get; set; } = string.Empty;

    // Yields
    /// <summary>
    /// Base yield before bonuses.
    /// </summary>
    public int BaseYield { get; set; }
    
    /// <summary>
    /// Estimated yield with current stats.
    /// </summary>
    public int EstimatedYield { get; set; }
    
    /// <summary>
    /// Possible materials that can be harvested.
    /// </summary>
    public List<string> PossibleMaterials { get; set; } = new();

    // Additional info
    /// <summary>
    /// Whether this is a rich node with bonus drops.
    /// </summary>
    public bool IsRichNode { get; set; }
    
    /// <summary>
    /// Number of times the node has been harvested.
    /// </summary>
    public int TimesHarvested { get; set; }
    
    /// <summary>
    /// Last harvest timestamp.
    /// </summary>
    public DateTime? LastHarvestedAt { get; set; }
}
