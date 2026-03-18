using MediatR;

namespace RealmEngine.Core.Features.Shop.Commands;

/// <summary>
/// Command to open and browse a merchant's shop, recording the visit.
/// Returns the shop's full item listing with pricing.
/// For a side-effect-free read, use <see cref="Queries.GetShopItemsQuery"/> instead.
/// </summary>
/// <param name="MerchantId">The merchant identifier.</param>
public record BrowseShopCommand(string MerchantId) : IRequest<BrowseShopResult>;

/// <summary>Result of opening a merchant's shop.</summary>
public record BrowseShopResult
{
    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public required bool Success { get; init; }
    /// <summary>Gets the merchant name.</summary>
    public string? MerchantName { get; init; }
    /// <summary>Gets the core items always in stock.</summary>
    public List<ShopItem> CoreItems { get; init; } = new();
    /// <summary>Gets the dynamic rotating items.</summary>
    public List<ShopItem> DynamicItems { get; init; } = new();
    /// <summary>Gets the items sold by the player.</summary>
    public List<ShopItem> PlayerSoldItems { get; init; } = new();
    /// <summary>Gets the error message if any.</summary>
    public string? ErrorMessage { get; init; }
}
