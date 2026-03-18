using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Shop;

/// <summary>
/// Represents a single item available in a merchant's shop, with buy and sell prices.
/// Used by both <see cref="Commands.BrowseShopCommand"/> and
/// <see cref="Queries.GetShopItemsQuery"/> so callers can share downstream logic.
/// </summary>
public record ShopItem
{
    /// <summary>Gets the item.</summary>
    public required Item Item { get; init; }

    /// <summary>Gets the buy price — the gold amount the player pays to purchase this item.</summary>
    public required int BuyPrice { get; init; }

    /// <summary>Gets the sell price — the gold amount the player receives when selling this item back.</summary>
    public required int SellPrice { get; init; }

    /// <summary>Gets a value indicating whether this item has unlimited stock.</summary>
    public bool IsUnlimited { get; init; }

    /// <summary>
    /// Gets the number of in-game days before this player-sold item is removed from the shop,
    /// or <see langword="null"/> for core and dynamic items.
    /// </summary>
    public int? DaysRemaining { get; init; }
}
