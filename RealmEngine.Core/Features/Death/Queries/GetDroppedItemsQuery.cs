using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Death.Queries;

/// <summary>
/// Query to get dropped items at a specific location.
/// </summary>
public record GetDroppedItemsQuery : IRequest<GetDroppedItemsResult>
{
    /// <summary>Gets the location to check for dropped items.</summary>
    public required string Location { get; init; }
}

/// <summary>
/// Result containing dropped items at the location.
/// </summary>
public record GetDroppedItemsResult
{
    /// <summary>Gets the list of dropped items at the location.</summary>
    public List<Item> Items { get; init; } = new();
    /// <summary>Gets a value indicating whether any items were found.</summary>
    public bool HasItems => Items.Count > 0;
}
