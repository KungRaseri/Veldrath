using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries.GetInventoryValue;

/// <summary>
/// Handler for calculating inventory value.
/// </summary>
public class GetInventoryValueQueryHandler : IRequestHandler<GetInventoryValueQuery, InventoryValueResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetInventoryValueQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetInventoryValueQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetInventoryValueQueryHandler(
        ISaveGameService saveGameService,
        ILogger<GetInventoryValueQueryHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the inventory value query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inventory value result.</returns>
    public Task<InventoryValueResult> Handle(GetInventoryValueQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character?.Inventory == null)
            {
                return Task.FromResult(new InventoryValueResult
                {
                    Success = false,
                    ErrorMessage = "No active save game or inventory found"
                });
            }

            var character = saveGame.Character;
            var equippedIds = GetEquippedItemIds(character);
            
            int equippedValue = 0;
            int unequippedValue = 0;
            int mostValuablePrice = 0;
            string? mostValuableName = null;

            foreach (var item in character.Inventory)
            {
                int itemValue = item.Price;
                
                if (equippedIds.Contains(item.Id))
                {
                    equippedValue += itemValue;
                }
                else
                {
                    unequippedValue += itemValue;
                }

                if (itemValue > mostValuablePrice)
                {
                    mostValuablePrice = itemValue;
                    mostValuableName = item.Name;
                }
            }

            int totalValue = request.IncludeEquipped 
                ? equippedValue + unequippedValue 
                : unequippedValue;

            var wealthCategory = DetermineWealthCategory(totalValue);

            _logger.LogInformation("Inventory value calculated: {Total}g total ({Equipped}g equipped, {Unequipped}g unequipped), Wealth: {Category}",
                totalValue, equippedValue, unequippedValue, wealthCategory);

            return Task.FromResult(new InventoryValueResult
            {
                Success = true,
                TotalValue = totalValue,
                EquippedValue = equippedValue,
                UnequippedValue = unequippedValue,
                MostValuableItemPrice = mostValuablePrice,
                MostValuableItemName = mostValuableName,
                WealthCategory = wealthCategory
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating inventory value");
            return Task.FromResult(new InventoryValueResult
            {
                Success = false,
                ErrorMessage = $"Failed to calculate inventory value: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Gets a set of all equipped item IDs from the character.
    /// </summary>
    private static HashSet<string> GetEquippedItemIds(Character character)
    {
        var equipped = new HashSet<string>();
        
        if (character.EquippedMainHand != null) equipped.Add(character.EquippedMainHand.Id);
        if (character.EquippedOffHand != null) equipped.Add(character.EquippedOffHand.Id);
        if (character.EquippedHelmet != null) equipped.Add(character.EquippedHelmet.Id);
        if (character.EquippedShoulders != null) equipped.Add(character.EquippedShoulders.Id);
        if (character.EquippedChest != null) equipped.Add(character.EquippedChest.Id);
        if (character.EquippedBracers != null) equipped.Add(character.EquippedBracers.Id);
        if (character.EquippedGloves != null) equipped.Add(character.EquippedGloves.Id);
        if (character.EquippedBelt != null) equipped.Add(character.EquippedBelt.Id);
        if (character.EquippedLegs != null) equipped.Add(character.EquippedLegs.Id);
        if (character.EquippedBoots != null) equipped.Add(character.EquippedBoots.Id);
        if (character.EquippedNecklace != null) equipped.Add(character.EquippedNecklace.Id);
        if (character.EquippedRing1 != null) equipped.Add(character.EquippedRing1.Id);
        if (character.EquippedRing2 != null) equipped.Add(character.EquippedRing2.Id);

        return equipped;
    }

    /// <summary>
    /// Determines the wealth category based on total inventory value.
    /// </summary>
    private static WealthCategory DetermineWealthCategory(int totalValue)
    {
        return totalValue switch
        {
            >= 50000 => WealthCategory.Noble,
            >= 10000 => WealthCategory.Rich,
            >= 2000 => WealthCategory.Wealthy,
            >= 500 => WealthCategory.Comfortable,
            >= 100 => WealthCategory.Common,
            _ => WealthCategory.Pauper
        };
    }
}
