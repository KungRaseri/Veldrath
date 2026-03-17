using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Shop.Commands;

/// <summary>
/// Handler for RefreshMerchantInventoryCommand.
/// Restocks dynamic items and removes expired player-sold items.
/// </summary>
public class RefreshMerchantInventoryCommandHandler : IRequestHandler<RefreshMerchantInventoryCommand, RefreshMerchantInventoryResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ShopEconomyService _shopService;
    private readonly ILogger<RefreshMerchantInventoryCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshMerchantInventoryCommandHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="shopService">The shop economy service.</param>
    /// <param name="logger">The logger.</param>
    public RefreshMerchantInventoryCommandHandler(
        ISaveGameService saveGameService,
        ShopEconomyService shopService,
        ILogger<RefreshMerchantInventoryCommandHandler> logger)
    {
        _saveGameService = saveGameService;
        _shopService = shopService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the RefreshMerchantInventoryCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<RefreshMerchantInventoryResult> Handle(RefreshMerchantInventoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new RefreshMerchantInventoryResult(
                    false, 0, 0, 0,
                    ErrorMessage: "No active game session"
                ));
            }

            var merchant = saveGame.KnownNPCs.FirstOrDefault(n => n.Id == request.MerchantId);
            if (merchant == null)
            {
                return Task.FromResult(new RefreshMerchantInventoryResult(
                    false, 0, 0, 0,
                    ErrorMessage: "Merchant not found"
                ));
            }

            if (!merchant.Traits.ContainsKey("isMerchant") || !merchant.Traits["isMerchant"].AsBool())
            {
                return Task.FromResult(new RefreshMerchantInventoryResult(
                    false, 0, 0, 0,
                    ErrorMessage: $"{merchant.Name} is not a merchant"
                ));
            }

            var inventory = _shopService.GetOrCreateInventory(merchant);
            var initialDynamicCount = inventory.DynamicItems.Count;
            var initialPlayerSoldCount = inventory.PlayerSoldItems.Count;

            // Remove expired player-sold items (7+ days old)
            var expiredItems = inventory.PlayerSoldItems
                .Where(item => item.DaysRemaining <= 0)
                .ToList();

            foreach (var item in expiredItems)
            {
                inventory.PlayerSoldItems.Remove(item);
            }

            // Age remaining player-sold items
            foreach (var item in inventory.PlayerSoldItems)
            {
                item.DaysRemaining--;
            }

            // Refresh dynamic inventory (placeholder logic - could be enhanced)
            // For now, just ensure we have some minimum number of dynamic items
            var shopInventoryType = merchant.Traits.ContainsKey("shopInventoryType")
                ? merchant.Traits["shopInventoryType"].AsString()
                : "hybrid";

            int itemsAdded = 0;
            if (shopInventoryType == "dynamic-only" || shopInventoryType == "hybrid")
            {
                _shopService.RefreshDynamicInventory(merchant, inventory);
                itemsAdded = inventory.DynamicItems.Count;
            }

            var itemsRemoved = initialDynamicCount - inventory.DynamicItems.Count;
            var itemsExpired = expiredItems.Count;

            _logger.LogInformation(
                "Refreshed inventory for {MerchantName}: {Added} added, {Removed} removed, {Expired} expired",
                merchant.Name, itemsAdded, itemsRemoved, itemsExpired);

            return Task.FromResult(new RefreshMerchantInventoryResult(
                Success: true,
                ItemsAdded: itemsAdded,
                ItemsRemoved: itemsRemoved,
                ItemsExpired: itemsExpired
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing merchant inventory for {MerchantId}", request.MerchantId);
            return Task.FromResult(new RefreshMerchantInventoryResult(
                false, 0, 0, 0,
                ErrorMessage: $"Failed to refresh inventory: {ex.Message}"
            ));
        }
    }
}
