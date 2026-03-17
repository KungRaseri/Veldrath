using RealmEngine.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Handles the EquipItem command.
/// </summary>
public class EquipItemHandler : IRequestHandler<EquipItemCommand, EquipItemResult>
{
    /// <summary>
    /// Handles the equip item command and equips the specified item.
    /// </summary>
    /// <param name="request">The equip item command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the equip result.</returns>
    public Task<EquipItemResult> Handle(EquipItemCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var item = request.Item;

        // Check if item is equipable
        if (item.Type == ItemType.Consumable || item.Type == ItemType.QuestItem)
        {
            return Task.FromResult(new EquipItemResult
            {
                Success = false,
                Message = $"Cannot equip {item.Name} - it's not equipable",
                UnequippedItem = null
            });
        }

        Item? unequipped = null;

        // Equip based on item type
        switch (item.Type)
        {
            case ItemType.Weapon:
                unequipped = player.EquippedMainHand;
                player.EquippedMainHand = item;
                break;
            case ItemType.Shield:
            case ItemType.OffHand:
                unequipped = player.EquippedOffHand;
                player.EquippedOffHand = item;
                break;
            case ItemType.Helmet:
                unequipped = player.EquippedHelmet;
                player.EquippedHelmet = item;
                break;
            case ItemType.Chest:
                unequipped = player.EquippedChest;
                player.EquippedChest = item;
                break;
            case ItemType.Legs:
                unequipped = player.EquippedLegs;
                player.EquippedLegs = item;
                break;
            case ItemType.Boots:
                unequipped = player.EquippedBoots;
                player.EquippedBoots = item;
                break;
            case ItemType.Necklace:
                unequipped = player.EquippedNecklace;
                player.EquippedNecklace = item;
                break;
            case ItemType.Ring:
                if (player.EquippedRing1 == null)
                {
                    player.EquippedRing1 = item;
                }
                else if (player.EquippedRing2 == null)
                {
                    player.EquippedRing2 = item;
                }
                else
                {
                    unequipped = player.EquippedRing1;
                    player.EquippedRing1 = item;
                }
                break;
        }

        // Remove from inventory
        player.Inventory.Remove(item);

        // Add unequipped item back to inventory
        if (unequipped != null)
        {
            player.Inventory.Add(unequipped);
        }

        _logger.LogInformation("Player {PlayerName} equipped {ItemName}",
            player.Name, item.Name);

        return Task.FromResult(new EquipItemResult
        {
            Success = true,
            Message = $"Equipped {item.Name}",
            UnequippedItem = unequipped
        });
    }
}