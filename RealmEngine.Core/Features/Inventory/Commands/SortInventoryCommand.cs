using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Command to sort inventory items by specified criteria.
/// </summary>
public record SortInventoryCommand : IRequest<SortInventoryResult>
{
    /// <summary>
    /// Gets the player character whose inventory to sort.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the criteria to sort by.
    /// </summary>
    public required SortCriteria SortBy { get; init; }
}

/// <summary>
/// Result of sorting inventory.
/// </summary>
public record SortInventoryResult
{
    /// <summary>
    /// Gets a value indicating whether the sort was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the sort result.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Criteria for sorting inventory.
/// </summary>
public enum SortCriteria
{
    /// <summary>
    /// Sort by item name.
    /// </summary>
    Name,

    /// <summary>
    /// Sort by item type.
    /// </summary>
    Type,

    /// <summary>
    /// Sort by item rarity.
    /// </summary>
    Rarity,

    /// <summary>
    /// Sort by item value.
    /// </summary>
    Value
}
