using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Services;

/// <summary>
/// In-memory implementation of <see cref="IInventoryService"/>.
/// Used by the no-database (InMemory) DI path for local development and testing.
/// State is stored in a plain dictionary and is not persisted across restarts.
/// </summary>
public class InMemoryInventoryService : IInventoryService
{
    private readonly ILogger<InMemoryInventoryService> _logger;
    private readonly Dictionary<string, Dictionary<string, int>> _inventories = [];
    private readonly Dictionary<string, Dictionary<string, int>> _durabilities = [];
    private const int MaxSlots = 100;

    /// <param name="logger">Logger.</param>
    public InMemoryInventoryService(ILogger<InMemoryInventoryService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> AddItemsAsync(string characterName, List<ItemDrop> items)
    {
        try
        {
            var inv = GetOrCreate(characterName);
            foreach (var item in items)
            {
                inv[item.ItemRef] = inv.TryGetValue(item.ItemRef, out var q) ? q + item.Quantity : item.Quantity;
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
            var inv = GetOrCreate(characterName);
            inv[itemRef] = inv.TryGetValue(itemRef, out var q) ? q + quantity : quantity;
            _logger.LogDebug("Added {Quantity}x {ItemRef} to {CharacterName}'s inventory", quantity, itemRef, characterName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {ItemRef} to {CharacterName}'s inventory", itemRef, characterName);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasInventorySpaceAsync(string characterName, int itemCount)
    {
        var inv = GetOrCreate(characterName);
        return Task.FromResult(inv.Count + itemCount <= MaxSlots);
    }

    /// <inheritdoc />
    public Task<int> GetItemCountAsync(string characterName, string itemRef)
    {
        var inv = GetOrCreate(characterName);
        return Task.FromResult(inv.TryGetValue(itemRef, out var q) ? q : 0);
    }

    /// <inheritdoc />
    public Task<bool> RemoveItemAsync(string characterName, string itemRef, int quantity)
    {
        try
        {
            var inv = GetOrCreate(characterName);
            if (!inv.TryGetValue(itemRef, out var current))
            {
                _logger.LogWarning("Cannot remove {ItemRef} from {CharacterName}: not found", itemRef, characterName);
                return Task.FromResult(false);
            }
            if (current < quantity)
            {
                _logger.LogWarning("Cannot remove {Quantity}x {ItemRef} from {CharacterName}: only has {Current}",
                    quantity, itemRef, characterName, current);
                return Task.FromResult(false);
            }
            inv[itemRef] -= quantity;
            if (inv[itemRef] == 0) inv.Remove(itemRef);
            _logger.LogDebug("Removed {Quantity}x {ItemRef} from {CharacterName}'s inventory", quantity, itemRef, characterName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {ItemRef} from {CharacterName}'s inventory", itemRef, characterName);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> ReduceItemDurabilityAsync(string characterName, string itemRef, int amount)
    {
        var inv = GetOrCreate(characterName);
        if (!inv.ContainsKey(itemRef))
        {
            _logger.LogWarning("Cannot reduce durability for {ItemRef} on {CharacterName}: not in inventory", itemRef, characterName);
            return Task.FromResult(false);
        }
        var current = _durabilities.TryGetValue(characterName, out var d) && d.TryGetValue(itemRef, out var dur) ? dur : 100;
        var updated = Math.Max(0, current - amount);
        if (!_durabilities.TryGetValue(characterName, out var charDur))
        {
            charDur = [];
            _durabilities[characterName] = charDur;
        }
        charDur[itemRef] = updated;
        _logger.LogDebug("{ItemRef} durability for {CharacterName}: {Old} -> {New}", itemRef, characterName, current, updated);
        return Task.FromResult(true);
    }

    private Dictionary<string, int> GetOrCreate(string characterName)
    {
        if (!_inventories.TryGetValue(characterName, out var inv))
        {
            inv = [];
            _inventories[characterName] = inv;
        }
        return inv;
    }

    /// <summary>Returns all items in a character's inventory. Useful for assertions in tests.</summary>
    public Dictionary<string, int> GetInventory(string characterName) => GetOrCreate(characterName);

    /// <summary>Clears all in-memory inventory state.</summary>
    public void Clear() => _inventories.Clear();
}
