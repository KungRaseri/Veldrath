using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Command to unequip an item from a specific slot.
/// </summary>
public record UnequipItemCommand : IRequest<UnequipItemResult>
{
    /// <summary>
    /// Gets the player character unequipping the item.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the slot type to unequip from.
    /// </summary>
    public required ItemType SlotType { get; init; }
}

/// <summary>
/// Result of unequipping an item.
/// </summary>
public record UnequipItemResult
{
    /// <summary>
    /// Gets a value indicating whether the unequip was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the unequip result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the item that was unequipped.
    /// </summary>
    public Item? UnequippedItem { get; init; }
}
