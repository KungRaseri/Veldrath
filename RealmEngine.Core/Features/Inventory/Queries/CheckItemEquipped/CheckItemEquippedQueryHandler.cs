using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Inventory.Queries.CheckItemEquipped;

/// <summary>
/// Handler for checking if an item is equipped.
/// </summary>
public class CheckItemEquippedQueryHandler : IRequestHandler<CheckItemEquippedQuery, ItemEquippedResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<CheckItemEquippedQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckItemEquippedQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public CheckItemEquippedQueryHandler(
        ISaveGameService saveGameService,
        ILogger<CheckItemEquippedQueryHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the check item equipped query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The item equipped result.</returns>
    public Task<ItemEquippedResult> Handle(CheckItemEquippedQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new ItemEquippedResult
                {
                    Success = false,
                    ErrorMessage = "No active save game or character found"
                });
            }

            var character = saveGame.Character;
            var (isEquipped, slot) = FindEquippedSlot(character, request.ItemId);

            _logger.LogDebug("Item {ItemId} equipped status: {IsEquipped} in slot {Slot}",
                request.ItemId, isEquipped, slot ?? "none");

            return Task.FromResult(new ItemEquippedResult
            {
                Success = true,
                IsEquipped = isEquipped,
                EquipSlot = slot
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if item {ItemId} is equipped", request.ItemId);
            return Task.FromResult(new ItemEquippedResult
            {
                Success = false,
                ErrorMessage = $"Failed to check item equipped status: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Finds which equipment slot contains the specified item.
    /// </summary>
    private static (bool isEquipped, string? slot) FindEquippedSlot(Shared.Models.Character character, string itemId)
    {
        if (character.EquippedMainHand?.Id == itemId) return (true, "MainHand");
        if (character.EquippedOffHand?.Id == itemId) return (true, "OffHand");
        if (character.EquippedHelmet?.Id == itemId) return (true, "Helmet");
        if (character.EquippedShoulders?.Id == itemId) return (true, "Shoulders");
        if (character.EquippedChest?.Id == itemId) return (true, "Chest");
        if (character.EquippedBracers?.Id == itemId) return (true, "Bracers");
        if (character.EquippedGloves?.Id == itemId) return (true, "Gloves");
        if (character.EquippedBelt?.Id == itemId) return (true, "Belt");
        if (character.EquippedLegs?.Id == itemId) return (true, "Legs");
        if (character.EquippedBoots?.Id == itemId) return (true, "Boots");
        if (character.EquippedNecklace?.Id == itemId) return (true, "Necklace");
        if (character.EquippedRing1?.Id == itemId) return (true, "Ring1");
        if (character.EquippedRing2?.Id == itemId) return (true, "Ring2");

        return (false, null);
    }
}
