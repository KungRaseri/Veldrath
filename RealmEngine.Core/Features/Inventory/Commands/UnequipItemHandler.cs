using RealmEngine.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Handles the UnequipItem command.
/// </summary>
public class UnequipItemHandler : IRequestHandler<UnequipItemCommand, UnequipItemResult>
{
    /// <summary>
    /// Handles the unequip item command and unequips the item from the specified slot.
    /// </summary>
    /// <param name="request">The unequip item command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the unequip result.</returns>
    public Task<UnequipItemResult> Handle(UnequipItemCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var slotType = request.SlotType;

        Item? unequipped = null;

        // Unequip based on slot type
        switch (slotType)
        {
            case ItemType.Weapon:
                unequipped = player.EquippedMainHand;
                player.EquippedMainHand = null;
                break;
            case ItemType.Shield:
            case ItemType.OffHand:
                unequipped = player.EquippedOffHand;
                player.EquippedOffHand = null;
                break;
            case ItemType.Helmet:
                unequipped = player.EquippedHelmet;
                player.EquippedHelmet = null;
                break;
            case ItemType.Chest:
                unequipped = player.EquippedChest;
                player.EquippedChest = null;
                break;
            case ItemType.Legs:
                unequipped = player.EquippedLegs;
                player.EquippedLegs = null;
                break;
            case ItemType.Boots:
                unequipped = player.EquippedBoots;
                player.EquippedBoots = null;
                break;
            case ItemType.Necklace:
                unequipped = player.EquippedNecklace;
                player.EquippedNecklace = null;
                break;
            case ItemType.Ring:
                // Unequip Ring1 by default
                if (player.EquippedRing1 != null)
                {
                    unequipped = player.EquippedRing1;
                    player.EquippedRing1 = null;
                }
                else if (player.EquippedRing2 != null)
                {
                    unequipped = player.EquippedRing2;
                    player.EquippedRing2 = null;
                }
                break;
        }

        if (unequipped == null)
        {
            return Task.FromResult(new UnequipItemResult
            {
                Success = false,
                Message = $"No {slotType} equipped",
                UnequippedItem = null
            });
        }

        // Add to inventory
        player.Inventory.Add(unequipped);

        _logger.LogInformation("Player {PlayerName} unequipped {ItemName}",
            player.Name, unequipped.Name);

        return Task.FromResult(new UnequipItemResult
        {
            Success = true,
            Message = $"Unequipped {unequipped.Name}",
            UnequippedItem = unequipped
        });
    }
}