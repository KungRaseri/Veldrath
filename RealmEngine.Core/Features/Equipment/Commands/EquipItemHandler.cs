using MediatR;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Equipment.Commands;

/// <summary>
/// Handler for equipping items from inventory to an equipment slot via the save game.
/// </summary>
public class EquipItemHandler : IRequestHandler<EquipItemCommand, EquipItemResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<EquipItemHandler> _logger;

    public EquipItemHandler(ISaveGameService saveGameService, ILogger<EquipItemHandler> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the equip item command.
    /// </summary>
    /// <param name="request">The equip item command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The equip result.</returns>
    public Task<EquipItemResult> Handle(EquipItemCommand request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
        {
            return Task.FromResult(new EquipItemResult
            {
                Success = false,
                Message = "No active game session"
            });
        }

        var character = saveGame.Character;
        var item = character.Inventory.FirstOrDefault(i => i.Id == request.ItemId);
        if (item == null)
        {
            return Task.FromResult(new EquipItemResult
            {
                Success = false,
                Message = $"Item '{request.ItemId}' not found in inventory"
            });
        }

        Item? previousItem = null;

        switch (request.Slot)
        {
            case EquipmentSlot.MainHand:
                previousItem = character.EquippedMainHand;
                character.EquippedMainHand = item;
                break;
            case EquipmentSlot.OffHand:
                previousItem = character.EquippedOffHand;
                character.EquippedOffHand = item;
                break;
            case EquipmentSlot.Head:
                previousItem = character.EquippedHelmet;
                character.EquippedHelmet = item;
                break;
            case EquipmentSlot.Shoulders:
                previousItem = character.EquippedShoulders;
                character.EquippedShoulders = item;
                break;
            case EquipmentSlot.Chest:
                previousItem = character.EquippedChest;
                character.EquippedChest = item;
                break;
            case EquipmentSlot.Bracers:
                previousItem = character.EquippedBracers;
                character.EquippedBracers = item;
                break;
            case EquipmentSlot.Gloves:
                previousItem = character.EquippedGloves;
                character.EquippedGloves = item;
                break;
            case EquipmentSlot.Belt:
                previousItem = character.EquippedBelt;
                character.EquippedBelt = item;
                break;
            case EquipmentSlot.Legs:
                previousItem = character.EquippedLegs;
                character.EquippedLegs = item;
                break;
            case EquipmentSlot.Boots:
                previousItem = character.EquippedBoots;
                character.EquippedBoots = item;
                break;
            case EquipmentSlot.Necklace:
                previousItem = character.EquippedNecklace;
                character.EquippedNecklace = item;
                break;
            case EquipmentSlot.Ring1:
                previousItem = character.EquippedRing1;
                character.EquippedRing1 = item;
                break;
            case EquipmentSlot.Ring2:
                previousItem = character.EquippedRing2;
                character.EquippedRing2 = item;
                break;
        }

        character.Inventory.Remove(item);

        if (previousItem != null)
        {
            character.Inventory.Add(previousItem);
        }

        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Character equipped {ItemName} to slot {Slot}", item.Name, request.Slot);

        return Task.FromResult(new EquipItemResult
        {
            Success = true,
            Message = $"Equipped {item.Name} to {request.Slot}",
            PreviousItem = previousItem
        });
    }
}
