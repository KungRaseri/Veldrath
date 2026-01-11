using MediatR;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for VisitShopCommand.
/// </summary>
public class VisitShopHandler : IRequestHandler<VisitShopCommand, VisitShopResult>
{
  private readonly ExplorationService _explorationService;
  private readonly ShopEconomyService _shopService;
  private readonly ILogger<VisitShopHandler> _logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="VisitShopHandler"/> class.
  /// </summary>
  public VisitShopHandler(
      ExplorationService explorationService,
      ShopEconomyService shopService,
      ILogger<VisitShopHandler> logger)
  {
    _explorationService = explorationService;
    _shopService = shopService;
    _logger = logger;
  }

  /// <summary>
  /// Handles the VisitShopCommand.
  /// </summary>
  public async Task<VisitShopResult> Handle(VisitShopCommand request, CancellationToken cancellationToken)
  {
    try
    {
      // Find the location
      var locations = await _explorationService.GetKnownLocationsAsync();
      var location = locations.FirstOrDefault(l => l.Id == request.LocationId);

      if (location == null)
      {
        return new VisitShopResult
        {
          Success = false,
          ErrorMessage = $"Location '{request.LocationId}' not found."
        };
      }

      // Check if location has a shop
      if (!location.HasShop)
      {
        return new VisitShopResult
        {
          Success = false,
          ErrorMessage = $"{location.Name} does not have a shop."
        };
      }

      // Find a merchant NPC at the location
      var merchant = location.NpcObjects.FirstOrDefault(npc =>
          npc.Occupation?.ToLower().Contains("merchant") == true ||
          npc.Occupation?.ToLower().Contains("blacksmith") == true ||
          npc.Occupation?.ToLower().Contains("shopkeeper") == true);

      if (merchant == null)
      {
        _logger.LogWarning("Location {LocationId} has HasShop=true but no merchant NPCs found", location.Id);
        return new VisitShopResult
        {
          Success = false,
          ErrorMessage = $"No merchants available at {location.Name}."
        };
      }

            // Get or create shop inventory for this merchant
            var shopInventory = _shopEconomyService.GetOrCreateInventory(merchant);
            var inventory = shopInventory.Items.ToList();

            return new VisitShopResult
            {
                Success = true,
                Merchant = merchant,
                Inventory = inventory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error visiting shop at location {LocationId}", request.LocationId);
            return new VisitShopResult
            {
                Success = false,
                ErrorMessage = $"An error occurred while visiting the shop: {ex.Message}"
            };
        }
    }

    private string DetermineShopType(NPC merchant)
  {
    var occupation = merchant.Occupation?.ToLower() ?? "";

    if (occupation.Contains("blacksmith") || occupation.Contains("weaponsmith"))
      return "weaponsmith";
    if (occupation.Contains("armorer") || occupation.Contains("armor"))
      return "armorer";
    if (occupation.Contains("alchemist") || occupation.Contains("apothecary"))
      return "apothecary";
    if (occupation.Contains("general"))
      return "general";

    return "general"; // Default
  }
}
