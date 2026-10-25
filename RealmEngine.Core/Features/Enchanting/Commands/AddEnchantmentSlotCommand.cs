using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Command to add an enchantment slot to an item using a socket crystal.
/// Requires Enchanting skill: 1st slot (skill 25), 2nd slot (skill 50), 3rd slot (skill 75).
/// Maximum slots determined by item rarity: Common=1, Rare=2, Legendary=3.
/// </summary>
public class AddEnchantmentSlotCommand : IRequest<AddEnchantmentSlotResult>
{
    /// <summary>
    /// The character adding the slot (for skill validation).
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The item to add a slot to.
    /// </summary>
    public required Item Item { get; set; }

    /// <summary>
    /// The socket crystal item to consume.
    /// </summary>
    public required Item SocketCrystal { get; set; }
}

/// <summary>
/// Result of adding an enchantment slot to an item.
/// </summary>
public class AddEnchantmentSlotResult
{
    /// <summary>
    /// Whether the slot was successfully added.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message describing what happened.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The new MaxPlayerEnchantments value after adding the slot.
    /// </summary>
    public int NewMaxSlots { get; set; }

    /// <summary>
    /// Whether the crystal was consumed.
    /// </summary>
    public bool CrystalConsumed { get; set; }
}
