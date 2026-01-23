using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Services;

/// <summary>
/// In-memory implementation of IInventoryService for testing and development.
/// </summary>
public class InMemoryInventoryService : IInventoryService
{
    private readonly ILogger<InMemoryInventoryService> _logger;
    private readonly Dictionary<string, Dictionary<string, int>> _inventories;
    private const int DEFAULT_MAX_SLOTS = 100;

    /// <summary>
    /// Initializes a new instance of InMemoryInventoryService.
    /// </summary>
    public InMemoryInventoryService(ILogger<InMemoryInventoryService> logger)
    {
        _logger = logger;
        _inventories = new Dictionary<string, Dictionary<string, int>>();
    }

    /// <inheritdoc />
    public Task<bool> AddItemsAsync(string characterName, List<ItemDrop> items)
    {
        try
        {
            var inventory = GetOrCreateInventory(characterName);

            foreach (var item in items)
            {
                if (inventory.ContainsKey(item.ItemRef))
                {
                    inventory[item.ItemRef] += item.Quantity;
                }
                else
                {
                    inventory[item.ItemRef] = item.Quantity;
                }

                _logger.LogDebug("Added {Quantity}x {ItemName} to {CharacterName}'s inventory", 
                    item.Quantity, item.ItemName, characterName);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add items to {CharacterName}'s inventory", characterName);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> AddItemAsync(string characterName, string itemRef, int quantity)
    {
        try
        {
            var inventory = GetOrCreateInventory(characterName);

            if (inventory.ContainsKey(itemRef))
            {
                inventory[itemRef] += quantity;
            }
            else
            {
                inventory[itemRef] = quantity;
            }

            _logger.LogDebug("Added {Quantity}x {ItemRef} to {CharacterName}'s inventory", 
                quantity, itemRef, characterName);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add item {ItemRef} to {CharacterName}'s inventory", 
                itemRef, characterName);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasInventorySpaceAsync(string characterName, int itemCount)
    {
        var inventory = GetOrCreateInventory(characterName);
        var currentSlots = inventory.Count;
        var hasSpace = currentSlots + itemCount <= DEFAULT_MAX_SLOTS;

        _logger.LogDebug("{CharacterName} inventory: {Current}/{Max} slots, requesting {New} more -> {HasSpace}", 
            characterName, currentSlots, DEFAULT_MAX_SLOTS, itemCount, hasSpace);

        return Task.FromResult(hasSpace);
    }

    /// <inheritdoc />
    public Task<int> GetItemCountAsync(string characterName, string itemRef)
    {
        var inventory = GetOrCreateInventory(characterName);
        var count = inventory.TryGetValue(itemRef, out var quantity) ? quantity : 0;

        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<bool> RemoveItemAsync(string characterName, string itemRef, int quantity)
    {
        try
        {
            var inventory = GetOrCreateInventory(characterName);

            if (!inventory.TryGetValue(itemRef, out var currentQuantity))
            {
                _logger.LogWarning("Cannot remove {ItemRef} from {CharacterName}: item not found", 
                    itemRef, characterName);
                return Task.FromResult(false);
            }

            if (currentQuantity < quantity)
            {
                _logger.LogWarning("Cannot remove {Quantity}x {ItemRef} from {CharacterName}: only has {Current}", 
                    quantity, itemRef, characterName, currentQuantity);
                return Task.FromResult(false);
            }

            inventory[itemRef] -= quantity;

            if (inventory[itemRef] <= 0)
            {
                inventory.Remove(itemRef);
            }

            _logger.LogDebug("Removed {Quantity}x {ItemRef} from {CharacterName}'s inventory", 
                quantity, itemRef, characterName);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item {ItemRef} from {CharacterName}'s inventory", 
                itemRef, characterName);
            return Task.FromResult(false);
        }
    }

    private Dictionary<string, int> GetOrCreateInventory(string characterName)
    {
        if (!_inventories.ContainsKey(characterName))
        {
            _inventories[characterName] = new Dictionary<string, int>();
            _logger.LogDebug("Created new inventory for {CharacterName}", characterName);
        }

        return _inventories[characterName];
    }

    /// <summary>
    /// Clears all inventories (for testing).
    /// </summary>
    public void Clear()
    {
        _inventories.Clear();
    }

    /// <summary>
    /// Gets all items in a character's inventory (for testing).
    /// </summary>
    public Dictionary<string, int> GetInventory(string characterName)
    {
        return GetOrCreateInventory(characterName);
    }
}
