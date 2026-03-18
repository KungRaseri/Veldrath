using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Shop.Queries;

/// <summary>
/// Query to retrieve a merchant's current shop inventory with pricing information.
/// This is a pure read operation — no visit is recorded and no state is changed.
/// To open a shop with visit tracking, use <see cref="Commands.BrowseShopCommand"/> instead.
/// </summary>
/// <param name="MerchantId">The merchant identifier.</param>
public record GetShopItemsQuery(string MerchantId) : IRequest<GetShopItemsResult>;

/// <summary>Read-only snapshot of a merchant's shop inventory with buy and sell prices.</summary>
public record GetShopItemsResult
{
    /// <summary>Gets a value indicating whether the query was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the merchant's display name.</summary>
    public string? MerchantName { get; init; }

    /// <summary>Gets the items that are always in stock at this shop.</summary>
    public List<ShopItem> CoreItems { get; init; } = [];

    /// <summary>Gets the rotating dynamic items currently available.</summary>
    public List<ShopItem> DynamicItems { get; init; } = [];

    /// <summary>Gets the items previously sold to this merchant by the player.</summary>
    public List<ShopItem> PlayerSoldItems { get; init; } = [];

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the total number of items available across all categories.</summary>
    public int TotalItemCount => CoreItems.Count + DynamicItems.Count + PlayerSoldItems.Count;
}

/// <summary>Handles <see cref="GetShopItemsQuery"/> — returns shop inventory without recording a visit.</summary>
public class GetShopItemsHandler : IRequestHandler<GetShopItemsQuery, GetShopItemsResult>
{
    private readonly ShopEconomyService _shopService;
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetShopItemsHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetShopItemsHandler"/>.</summary>
    /// <param name="shopService">The shop economy service.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetShopItemsHandler(
        ShopEconomyService shopService,
        ISaveGameService saveGameService,
        ILogger<GetShopItemsHandler> logger)
    {
        _shopService      = shopService      ?? throw new ArgumentNullException(nameof(shopService));
        _saveGameService  = saveGameService  ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger           = logger           ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Returns the shop's current inventory without side effects.</summary>
    public Task<GetShopItemsResult> Handle(GetShopItemsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
                return Task.FromResult(new GetShopItemsResult { Success = false, ErrorMessage = "No active game session" });

            var merchant = saveGame.KnownNPCs?.FirstOrDefault(n => n.Id == request.MerchantId);
            if (merchant == null)
                return Task.FromResult(new GetShopItemsResult { Success = false, ErrorMessage = "Merchant not found" });

            if (!merchant.Traits.ContainsKey("isMerchant") || !merchant.Traits["isMerchant"].AsBool())
                return Task.FromResult(new GetShopItemsResult { Success = false, ErrorMessage = $"{merchant.Name} is not a merchant" });

            var inventory = _shopService.GetOrCreateInventory(merchant);

            var coreItems = inventory.CoreItems.Select(item => new ShopItem
            {
                Item       = item,
                BuyPrice   = _shopService.CalculateSellPrice(item, merchant),
                SellPrice  = _shopService.CalculateBuyPrice(item, merchant),
                IsUnlimited = true
            }).ToList();

            var dynamicItems = inventory.DynamicItems.Select(item => new ShopItem
            {
                Item       = item,
                BuyPrice   = _shopService.CalculateSellPrice(item, merchant),
                SellPrice  = _shopService.CalculateBuyPrice(item, merchant),
                IsUnlimited = false
            }).ToList();

            var playerSoldItems = inventory.PlayerSoldItems.Select(pi => new ShopItem
            {
                Item         = pi.Item,
                BuyPrice     = pi.ResellPrice,
                SellPrice    = _shopService.CalculateBuyPrice(pi.Item, merchant),
                IsUnlimited  = false,
                DaysRemaining = pi.DaysRemaining
            }).ToList();

            _logger.LogDebug("GetShopItems: {MerchantName} — {Total} items",
                merchant.Name, coreItems.Count + dynamicItems.Count + playerSoldItems.Count);

            return Task.FromResult(new GetShopItemsResult
            {
                Success         = true,
                MerchantName    = merchant.Name,
                CoreItems       = coreItems,
                DynamicItems    = dynamicItems,
                PlayerSoldItems = playerSoldItems
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shop items for merchant {MerchantId}", request.MerchantId);
            return Task.FromResult(new GetShopItemsResult { Success = false, ErrorMessage = $"Error accessing shop: {ex.Message}" });
        }
    }
}
