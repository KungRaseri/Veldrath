using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Command to equip an item on the character.
/// </summary>
public record EquipItemCommand : IRequest<EquipItemResult>
{
    /// <summary>
    /// Gets the player character equipping the item.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the item to equip.
    /// </summary>
    public required Item Item { get; init; }
}

/// <summary>
/// Result of equipping an item.
/// </summary>
public record EquipItemResult
{
    /// <summary>
    /// Gets a value indicating whether the equip was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the equip result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the item that was unequipped from the slot, if any.
    /// </summary>
    public Item? UnequippedItem { get; init; }
}
