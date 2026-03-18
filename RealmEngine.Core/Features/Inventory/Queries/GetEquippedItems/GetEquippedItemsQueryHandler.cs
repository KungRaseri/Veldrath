using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Inventory.Queries.GetEquippedItems;

/// <summary>
/// Handler for retrieving all equipped items.
/// </summary>
public class GetEquippedItemsQueryHandler : IRequestHandler<GetEquippedItemsQuery, EquippedItemsResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetEquippedItemsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetEquippedItemsQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetEquippedItemsQueryHandler(
        ISaveGameService saveGameService,
        ILogger<GetEquippedItemsQueryHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the equipped items query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The equipped items result.</returns>
    public Task<EquippedItemsResult> Handle(GetEquippedItemsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new EquippedItemsResult
                {
                    Success = false,
                    ErrorMessage = "No active save game or character found"
                });
            }

            var character = saveGame.Character;
            var loadout = new EquipmentLoadout
            {
                MainHand = character.EquippedMainHand,
                OffHand = character.EquippedOffHand,
                Helm = character.EquippedHelmet,
                Shoulders = character.EquippedShoulders,
                Chest = character.EquippedChest,
                Bracers = character.EquippedBracers,
                Gloves = character.EquippedGloves,
                Belt = character.EquippedBelt,
                Legs = character.EquippedLegs,
                Boots = character.EquippedBoots,
                Necklace = character.EquippedNecklace,
                Ring1 = character.EquippedRing1,
                Ring2 = character.EquippedRing2
            };

            var stats = CalculateEquipmentStats(loadout);

            _logger.LogInformation("Retrieved equipment loadout: {Count} items equipped, {Value}g total value",
                stats.TotalEquippedItems, stats.TotalValue);

            return Task.FromResult(new EquippedItemsResult
            {
                Success = true,
                Equipment = loadout,
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving equipped items");
            return Task.FromResult(new EquippedItemsResult
            {
                Success = false,
                ErrorMessage = $"Failed to retrieve equipped items: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Calculates aggregated statistics from all equipped items.
    /// </summary>
    private static EquipmentStats CalculateEquipmentStats(EquipmentLoadout loadout)
    {
        var allItems = new[] {
            loadout.MainHand, loadout.OffHand, loadout.Helm, loadout.Shoulders,
            loadout.Chest, loadout.Bracers, loadout.Gloves, loadout.Belt,
            loadout.Legs, loadout.Boots, loadout.Necklace, loadout.Ring1,
            loadout.Ring2
        }.Where(item => item != null).ToList();

        int totalValue = 0;
        int totalAttack = 0;
        int totalDefense = 0;
        int totalSockets = 0;
        int filledSockets = 0;
        var setBonuses = new Dictionary<string, int>();

        foreach (var item in allItems)
        {
            if (item == null) continue;

            totalValue += item.Price;

            // Calculate attack bonus from damage traits
            if (item.Traits != null)
            {
                foreach (var trait in item.Traits)
                {
                    if (trait.Key.Contains("damage", StringComparison.OrdinalIgnoreCase) ||
                        trait.Key.Contains("attack", StringComparison.OrdinalIgnoreCase))
                    {
                        if (trait.Value?.Value is int damageValue)
                        {
                            totalAttack += damageValue;
                        }
                    }

                    if (trait.Key.Contains("defense", StringComparison.OrdinalIgnoreCase) ||
                        trait.Key.Contains("armor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (trait.Value?.Value is int defenseValue)
                        {
                            totalDefense += defenseValue;
                        }
                    }
                }
            }

            // Count sockets
            if (item.Sockets != null)
            {
                totalSockets += item.Sockets.Sum(kvp => kvp.Value.Count);
                filledSockets += item.Sockets.Sum(kvp => kvp.Value.Count(s => s.Content != null));
            }

            // Track set pieces (from SetName property, not prefixes)
            if (!string.IsNullOrEmpty(item.SetName))
            {
                if (!setBonuses.ContainsKey(item.SetName))
                {
                    setBonuses[item.SetName] = 0;
                }
                setBonuses[item.SetName]++;
            }
        }

        return new EquipmentStats
        {
            TotalEquippedItems = allItems.Count,
            TotalValue = totalValue,
            TotalAttackBonus = totalAttack,
            TotalDefenseBonus = totalDefense,
            TotalSockets = totalSockets,
            FilledSockets = filledSockets,
            SetBonuses = setBonuses
        };
    }
}
