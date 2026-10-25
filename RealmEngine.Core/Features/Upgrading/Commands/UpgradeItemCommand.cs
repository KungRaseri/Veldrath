using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Upgrading.Commands;

/// <summary>
/// Command to upgrade an item's level using essences.
/// Levels +1 to +5 are safe (100% success). Levels +6 to +10 have failure risk.
/// Failed upgrades drop item to previous level and consume essences.
/// </summary>
public class UpgradeItemCommand : IRequest<UpgradeItemResult>
{
    /// <summary>
    /// The character performing the upgrade.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The item to upgrade.
    /// </summary>
    public required Item Item { get; set; }

    /// <summary>
    /// The essence items to consume for the upgrade.
    /// Should match the item type (Weapon/Armor/Accessory Essence).
    /// </summary>
    public required List<Item> Essences { get; set; }
}

/// <summary>
/// Result of upgrading an item.
/// </summary>
public class UpgradeItemResult
{
    /// <summary>
    /// Whether the upgrade succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message describing what happened.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The new upgrade level (may be lower if failed).
    /// </summary>
    public int NewUpgradeLevel { get; set; }

    /// <summary>
    /// The old upgrade level before attempting.
    /// </summary>
    public int OldUpgradeLevel { get; set; }

    /// <summary>
    /// The success rate for the upgrade attempt (for UI display).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Whether essences were consumed (always true).
    /// </summary>
    public bool EssencesConsumed { get; set; }

    /// <summary>
    /// The stat multiplier after upgrade (for UI display).
    /// Formula: 1 + (level * 0.10) + (level² * 0.01)
    /// </summary>
    public double StatMultiplier { get; set; }
}
