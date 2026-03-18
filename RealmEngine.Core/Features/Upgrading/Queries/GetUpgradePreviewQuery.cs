using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Upgrading.Queries;

/// <summary>
/// Query to preview the next upgrade for an item without committing the operation.
/// Returns success rate, required essences, and projected stat changes.
/// </summary>
public record GetUpgradePreviewQuery(Item Item) : IRequest<GetUpgradePreviewResult>;

/// <summary>
/// Result of the upgrade preview query.
/// </summary>
public class GetUpgradePreviewResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the item name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the item can still be upgraded.</summary>
    public bool CanUpgrade { get; set; }

    /// <summary>Gets or sets preview details for the next upgrade level (null when at max).</summary>
    public UpgradePreviewInfo? NextLevelPreview { get; set; }

    /// <summary>Gets or sets the full upgrade path showing all remaining levels.</summary>
    public List<UpgradeLevelSummary> RemainingLevels { get; set; } = new();
}

/// <summary>
/// Summary of a single upgrade level for display in a progression table.
/// </summary>
public class UpgradeLevelSummary
{
    /// <summary>Gets or sets the upgrade level number.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the success rate percentage for this level.</summary>
    public double SuccessRate { get; set; }

    /// <summary>Gets or sets the stat multiplier at this level.</summary>
    public double StatMultiplier { get; set; }

    /// <summary>Gets or sets the required essence tier names.</summary>
    public List<string> RequiredEssences { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether this is in the safe zone (+1–+5).</summary>
    public bool IsSafeZone { get; set; }
}
