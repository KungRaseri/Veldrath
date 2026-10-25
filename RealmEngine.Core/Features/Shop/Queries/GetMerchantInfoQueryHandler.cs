using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Shop.Queries;

/// <summary>
/// Handler for GetMerchantInfoQuery.
/// Returns detailed merchant information for UI display.
/// </summary>
public class GetMerchantInfoQueryHandler : IRequestHandler<GetMerchantInfoQuery, GetMerchantInfoResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ShopEconomyService _shopService;
    private readonly ILogger<GetMerchantInfoQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMerchantInfoQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="shopService">The shop economy service.</param>
    /// <param name="logger">The logger.</param>
    public GetMerchantInfoQueryHandler(
        ISaveGameService saveGameService,
        ShopEconomyService shopService,
        ILogger<GetMerchantInfoQueryHandler> logger)
    {
        _saveGameService = saveGameService;
        _shopService = shopService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetMerchantInfoQuery and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<GetMerchantInfoResult> Handle(GetMerchantInfoQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new GetMerchantInfoResult(false, ErrorMessage: "No active game session"));
            }

            var merchant = saveGame.KnownNPCs.FirstOrDefault(n => n.Id == request.MerchantId);
            if (merchant == null)
            {
                return Task.FromResult(new GetMerchantInfoResult(false, ErrorMessage: "Merchant not found"));
            }

            if (!merchant.Traits.ContainsKey("isMerchant") || !merchant.Traits["isMerchant"].AsBool())
            {
                return Task.FromResult(new GetMerchantInfoResult(false, ErrorMessage: $"{merchant.Name} is not a merchant"));
            }

            // Get shop inventory
            var inventory = _shopService.GetOrCreateInventory(merchant);

            // Extract merchant traits
            var shopType = merchant.Traits.ContainsKey("shopType")
                ? merchant.Traits["shopType"].AsString()
                : "general";

            var shopInventoryType = merchant.Traits.ContainsKey("shopInventoryType")
                ? merchant.Traits["shopInventoryType"].AsString()
                : "hybrid";

            // Calculate modifiers (could be enhanced with merchant traits)
            var buyModifier = 1.5; // Player buys at 1.5x base price
            var sellModifier = 0.5; // Player sells at 0.5x base price

            var merchantInfo = new MerchantInfo(
                Id: merchant.Id,
                Name: merchant.Name,
                Occupation: merchant.Occupation,
                Gold: merchant.Gold,
                ShopType: shopType,
                ShopInventoryType: shopInventoryType,
                TotalItemsForSale: inventory.CoreItems.Count + inventory.DynamicItems.Count + inventory.PlayerSoldItems.Count,
                CoreItemsCount: inventory.CoreItems.Count,
                DynamicItemsCount: inventory.DynamicItems.Count,
                PlayerSoldItemsCount: inventory.PlayerSoldItems.Count,
                AcceptsPlayerItems: shopInventoryType == "hybrid" || shopInventoryType == "dynamic-only",
                BuyPriceModifier: buyModifier,
                SellPriceModifier: sellModifier
            );

            _logger.LogInformation("Retrieved merchant info for {MerchantName} ({MerchantId})",
                merchant.Name, merchant.Id);

            return Task.FromResult(new GetMerchantInfoResult(
                Success: true,
                Merchant: merchantInfo
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting merchant info for {MerchantId}", request.MerchantId);
            return Task.FromResult(new GetMerchantInfoResult(
                false,
                ErrorMessage: $"Failed to get merchant info: {ex.Message}"
            ));
        }
    }
}
