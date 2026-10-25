using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Command to remove a specific enchantment from an item using a removal scroll.
/// Player selects which enchantment slot to clear. The scroll is always consumed.
/// </summary>
public class RemoveEnchantmentCommand : IRequest<RemoveEnchantmentResult>
{
    /// <summary>
    /// The character removing the enchantment.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The item to remove an enchantment from.
    /// </summary>
    public required Item Item { get; set; }

    /// <summary>
    /// The zero-based index of the enchantment to remove from PlayerEnchantments list.
    /// </summary>
    public int EnchantmentIndex { get; set; }

    /// <summary>
    /// The removal scroll item to consume.
    /// </summary>
    public required Item RemovalScroll { get; set; }
}

/// <summary>
/// Result of removing an enchantment from an item.
/// </summary>
public class RemoveEnchantmentResult
{
    /// <summary>
    /// Whether the enchantment was successfully removed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message describing what happened.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The enchantment that was removed (null if failed).
    /// </summary>
    public Enchantment? RemovedEnchantment { get; set; }

    /// <summary>
    /// Whether the scroll was consumed (always true if validation passed).
    /// </summary>
    public bool ScrollConsumed { get; set; }
}
