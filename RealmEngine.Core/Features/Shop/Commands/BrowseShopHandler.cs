using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Shop.Commands;

/// <summary>
/// Handler for browsing a merchant's shop inventory.
/// </summary>
public class BrowseShopHandler : IRequestHandler<BrowseShopCommand, BrowseShopResult>
{
    private readonly ShopEconomyService _shopService;
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<BrowseShopHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowseShopHandler"/> class.
    /// </summary>
    /// <param name="shopService">The shop economy service.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public BrowseShopHandler(
        ShopEconomyService shopService,
        ISaveGameService saveGameService,
        ILogger<BrowseShopHandler> logger)
    {
        _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the BrowseShopCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<BrowseShopResult> Handle(BrowseShopCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new BrowseShopResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            // Find merchant NPC
            var merchant = saveGame.KnownNPCs?.FirstOrDefault(n => n.Id == request.MerchantId);
            if (merchant == null)
            {
                return Task.FromResult(new BrowseShopResult
                {
                    Success = false,
                    ErrorMessage = "Merchant not found"
                });
            }

            // Check if NPC is a merchant
            if (!merchant.Traits.ContainsKey("isMerchant") || !merchant.Traits["isMerchant"].AsBool())
            {
                return Task.FromResult(new BrowseShopResult
                {
                    Success = false,
                    ErrorMessage = $"{merchant.Name} is not a merchant"
                });
            }

            // Get shop inventory
            var inventory = _shopService.GetOrCreateInventory(merchant);

            // Build result with pricing
            var coreItems = inventory.CoreItems.Select(item => new ShopItem
            {
                Item = item,
                BuyPrice = _shopService.CalculateSellPrice(item, merchant),
                SellPrice = _shopService.CalculateBuyPrice(item, merchant),
                IsUnlimited = true
            }).ToList();

            var dynamicItems = inventory.DynamicItems.Select(item => new ShopItem
            {
                Item = item,
                BuyPrice = _shopService.CalculateSellPrice(item, merchant),
                SellPrice = _shopService.CalculateBuyPrice(item, merchant),
                IsUnlimited = false
            }).ToList();

            var playerSoldItems = inventory.PlayerSoldItems.Select(pi => new ShopItem
            {
                Item = pi.Item,
                BuyPrice = pi.ResellPrice,
                SellPrice = _shopService.CalculateBuyPrice(pi.Item, merchant),
                IsUnlimited = false,
                DaysRemaining = pi.DaysRemaining
            }).ToList();

            _logger.LogInformation("Player browsing {MerchantName}'s shop", merchant.Name);

            return Task.FromResult(new BrowseShopResult
            {
                Success = true,
                MerchantName = merchant.Name,
                CoreItems = coreItems,
                DynamicItems = dynamicItems,
                PlayerSoldItems = playerSoldItems
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing shop for merchant {MerchantId}", request.MerchantId);
            return Task.FromResult(new BrowseShopResult
            {
                Success = false,
                ErrorMessage = $"Error accessing shop: {ex.Message}"
            });
        }
    }
}
