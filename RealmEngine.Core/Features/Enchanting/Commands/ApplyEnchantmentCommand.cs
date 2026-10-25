using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Command to apply an enchantment scroll to an item.
/// Success rate is skill-based: Slot 1 (100%), Slot 2 (75% + skill), Slot 3 (50% + skill).
/// Failed applications consume the scroll.
/// </summary>
public class ApplyEnchantmentCommand : IRequest<ApplyEnchantmentResult>
{
    /// <summary>
    /// The character applying the enchantment (for skill validation).
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The item to enchant.
    /// </summary>
    public required Item Item { get; set; }

    /// <summary>
    /// The enchantment scroll item to apply.
    /// Must have Type = Enchantment or similar.
    /// </summary>
    public required Item EnchantmentScroll { get; set; }
}

/// <summary>
/// Result of applying an enchantment to an item.
/// </summary>
public class ApplyEnchantmentResult
{
    /// <summary>
    /// Whether the enchantment was successfully applied.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message describing what happened.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The enchantment that was applied (null if failed).
    /// </summary>
    public Enchantment? AppliedEnchantment { get; set; }

    /// <summary>
    /// The success rate used for the attempt (for UI display).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Whether the scroll was consumed (always true).
    /// </summary>
    public bool ScrollConsumed { get; set; }
}
