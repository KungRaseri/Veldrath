using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Query to get all equipped items on player.
/// </summary>
public record GetEquippedItemsQuery : IRequest<GetEquippedItemsResult>
{
    /// <summary>Gets the player character.</summary>
    public required Character Player { get; init; }
}

/// <summary>
/// Result containing equipped items.
/// </summary>
public record GetEquippedItemsResult
{
    /// <summary>Gets the dictionary of equipped items by slot name.</summary>
    public required Dictionary<string, Item?> EquippedItems { get; init; }
}
