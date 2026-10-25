using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Query to get all items in player's inventory.
/// </summary>
public record GetInventoryItemsQuery : IRequest<GetInventoryItemsResult>
{
    /// <summary>Gets the player character.</summary>
    public required Character Player { get; init; }
}

/// <summary>
/// Result containing inventory items.
/// </summary>
public record GetInventoryItemsResult
{
    /// <summary>Gets the list of inventory items.</summary>
    public required List<Item> Items { get; init; }
    /// <summary>Gets the total count of items.</summary>
    public required int TotalItems { get; init; }
}
