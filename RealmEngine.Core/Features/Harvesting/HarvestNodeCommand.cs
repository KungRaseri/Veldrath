using MediatR;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Command to harvest materials from a resource node.
/// </summary>
public class HarvestNodeCommand : IRequest<HarvestResult>
{
    /// <summary>
    /// Character name performing the harvest.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// ID of the node being harvested.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Equipped tool item reference (optional for common materials).
    /// </summary>
    public string? EquippedToolRef { get; set; }

    /// <summary>
    /// Override skill rank for testing (null = use character's actual skill).
    /// </summary>
    public int? SkillRankOverride { get; set; }
}
