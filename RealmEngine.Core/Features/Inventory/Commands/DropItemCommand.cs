using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Command to drop an item from inventory.
/// </summary>
public record DropItemCommand : IRequest<DropItemResult>
{
    /// <summary>
    /// Gets the player character dropping the item.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the item to drop.
    /// </summary>
    public required Item Item { get; init; }
}

/// <summary>
/// Result of dropping an item.
/// </summary>
public record DropItemResult
{
    /// <summary>
    /// Gets a value indicating whether the drop was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the drop result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
