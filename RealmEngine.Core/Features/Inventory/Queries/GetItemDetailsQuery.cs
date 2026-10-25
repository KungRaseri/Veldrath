using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Query to get detailed information about a specific item.
/// </summary>
public record GetItemDetailsQuery : IRequest<GetItemDetailsResult>
{
    /// <summary>Gets the item to get details for.</summary>
    public required Item Item { get; init; }
}

/// <summary>
/// Result containing detailed item information.
/// </summary>
public record GetItemDetailsResult
{
    /// <summary>Gets the item name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the item description.</summary>
    public required string Description { get; init; }
    /// <summary>Gets the item type.</summary>
    public required ItemType Type { get; init; }
    /// <summary>Gets the item rarity.</summary>
    public required ItemRarity Rarity { get; init; }
    /// <summary>Gets the item price.</summary>
    public required int Price { get; init; }
    /// <summary>Gets the item traits.</summary>
    public required Dictionary<string, TraitValue> Traits { get; init; }
    /// <summary>Gets the item upgrade level.</summary>
    public required int UpgradeLevel { get; init; }
    /// <summary>Gets the item enchantments.</summary>
    public required List<Enchantment> Enchantments { get; init; }
    /// <summary>Gets the set name if part of a set.</summary>
    public required string? SetName { get; init; }
}
