using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Death;

/// <summary>
/// Service for death-related operations (penalties, item dropping, etc.).
/// </summary>
public class DeathService
{
    private readonly Random _random = new();
    private readonly ILogger<DeathService> _logger;

    public DeathService(ILogger<DeathService> logger)
    {
        _logger = logger;
    }


    /// <summary>
    /// Handle item dropping based on difficulty settings.
    /// </summary>
    public virtual List<Item> HandleItemDropping(
        Character player,
        SaveGame saveGame,
        string location,
        DifficultySettings difficulty)
    {
        var droppedItems = new List<Item>();

        // Early exit if no items to drop
        if (difficulty.ItemsDroppedOnDeath == 0 && !difficulty.DropAllInventoryOnDeath)
        {
            return droppedItems;
        }

        // Drop all inventory
        if (difficulty.DropAllInventoryOnDeath)
        {
            droppedItems.AddRange(player.Inventory);
            player.Inventory.Clear();

            _logger.LogInformation("Dropped all {Count} items at {Location}", droppedItems.Count, location);
        }
        // Drop random items
        else
        {
            var itemsToDrop = Math.Min(difficulty.ItemsDroppedOnDeath, player.Inventory.Count);

            for (int i = 0; i < itemsToDrop; i++)
            {
                if (player.Inventory.Count == 0) break;

                var randomIndex = _random.Next(player.Inventory.Count);
                var item = player.Inventory[randomIndex];

                droppedItems.Add(item);
                player.Inventory.RemoveAt(randomIndex);
            }

            _logger.LogInformation("Dropped {Count} random items at {Location}", droppedItems.Count, location);
        }

        // Store dropped items in save game
        if (droppedItems.Count > 0)
        {
            if (!saveGame.DroppedItemsAtLocations.ContainsKey(location))
            {
                saveGame.DroppedItemsAtLocations[location] = new List<Item>();
            }

            saveGame.DroppedItemsAtLocations[location].AddRange(droppedItems);
        }

        return droppedItems;
    }

    /// <summary>
    /// Retrieve dropped items from a location.
    /// </summary>
    public List<Item> RetrieveDroppedItems(SaveGame saveGame, string location)
    {
        if (saveGame.DroppedItemsAtLocations.TryGetValue(location, out var items))
        {
            saveGame.DroppedItemsAtLocations.Remove(location);
            return items;
        }

        return new List<Item>();
    }
}