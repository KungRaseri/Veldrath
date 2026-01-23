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
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Node basic info
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string MaterialTier { get; set; } = string.Empty;

    // Node state
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int HealthPercent { get; set; }
    public NodeState State { get; set; }
    public bool CanHarvest { get; set; }

    // Requirements
    public int MinToolTier { get; set; }
    public string RequiredToolType { get; set; } = string.Empty;
    public string RequiredSkill { get; set; } = string.Empty;

    // Yields
    public int BaseYield { get; set; }
    public int EstimatedYield { get; set; }
    public List<string> PossibleMaterials { get; set; } = new();

    // Additional info
    public bool IsRichNode { get; set; }
    public int TimesHarvested { get; set; }
    public DateTime? LastHarvestedAt { get; set; }
}
