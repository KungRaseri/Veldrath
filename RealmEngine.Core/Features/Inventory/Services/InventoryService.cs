using RealmEngine.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Inventory;

/// <summary>
/// Service for managing character inventory operations.
/// </summary>
public class InventoryService
{
    private readonly IMediator _mediator;
    private readonly ILogger<InventoryService> _logger;
    private readonly List<Item> _inventory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryService"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="logger">The logger.</param>
    public InventoryService(IMediator mediator, ILogger<InventoryService> logger)
    {
        _mediator = mediator;
        _logger = logger;
        _inventory = new List<Item>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryService"/> class with existing inventory.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="existingInventory">The existing inventory items.</param>
    /// <param name="logger">The logger.</param>
    public InventoryService(IMediator mediator, List<Item> existingInventory, ILogger<InventoryService> logger)
    {
        _mediator = mediator;
        _logger = logger;
        _inventory = existingInventory ?? new List<Item>();
    }

    /// <summary>
    /// Gets all items in the inventory.
    /// </summary>
    public IReadOnlyList<Item> GetAllItems() => _inventory.AsReadOnly();

    /// <summary>
    /// Gets the count of items in the inventory.
    /// </summary>
    public int Count => _inventory.Count;

    /// <summary>
    /// Adds an item to the inventory.
    /// If the item is stackable, it will be combined with an existing stack if possible.
    /// </summary>
    public async Task<bool> AddItemAsync(Item item, string playerName)
    {
        if (item == null)
        {
            _logger.LogWarning("Attempted to add null item to inventory");
            return false;
        }

        // Check if item can stack with an existing item
        if (item.IsStackable)
        {
            var existingStack = _inventory.FirstOrDefault(i => i.CanStackWith(item));
            if (existingStack != null)
            {
                existingStack.AddQuantity(item.Quantity);
                await _mediator.Publish(new ItemAcquired(playerName, item.Name));
                _logger.LogInformation("Item stacked in inventory: {ItemName} x{Quantity} (Total: {Total})", 
                    item.Name, item.Quantity, existingStack.Quantity);
                return true;
            }
        }

        // Add as new item if not stackable or no existing stack found
        _inventory.Add(item);
        await _mediator.Publish(new ItemAcquired(playerName, item.Name));
        _logger.LogInformation("Item added to inventory: {ItemName} ({ItemType}) x{Quantity}", 
            item.Name, item.Type, item.Quantity);
        return true;
    }

    /// <summary>
    /// Removes an item from the inventory by ID.
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        var item = _inventory.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
        {
            _logger.LogWarning("Attempted to remove non-existent item: {ItemId}", itemId);
            return false;
        }

        _inventory.Remove(item);
        _logger.LogInformation("Item removed from inventory: {ItemName}", item.Name);
        return true;
    }

    /// <summary>
    /// Removes an item from the inventory by reference.
    /// </summary>
    public bool RemoveItem(Item item)
    {
        if (item == null || !_inventory.Contains(item))
        {
            _logger.LogWarning("Attempted to remove non-existent item");
            return false;
        }

        _inventory.Remove(item);
        _logger.LogInformation("Item removed from inventory: {ItemName}", item.Name);
        return true;
    }

    /// <summary>
    /// Gets items filtered by type.
    /// </summary>
    public List<Item> GetItemsByType(ItemType type)
    {
        return _inventory.Where(i => i.Type == type).ToList();
    }

    /// <summary>
    /// Gets items filtered by rarity.
    /// </summary>
    public List<Item> GetItemsByRarity(ItemRarity rarity)
    {
        return _inventory.Where(i => i.Rarity == rarity).ToList();
    }

    /// <summary>
    /// Finds an item by ID.
    /// </summary>
    public Item? FindItemById(string itemId)
    {
        return _inventory.FirstOrDefault(i => i.Id == itemId);
    }

    /// <summary>
    /// Uses a consumable item and applies its effects to the character.
    /// </summary>
    public Task<bool> UseItemAsync(Item item, Character character, string playerName)
    {
        if (item == null || character == null)
        {
            _logger.LogWarning("Invalid item or character for UseItem");
            return Task.FromResult(false);
        }

        if (!_inventory.Contains(item))
        {
            _logger.LogWarning("Attempted to use item not in inventory: {ItemName}", item.Name);
            return Task.FromResult(false);
        }

        if (item.Type != ItemType.Consumable)
        {
            _logger.LogWarning("Attempted to use non-consumable item: {ItemName} ({ItemType})", item.Name, item.Type);
            return Task.FromResult(false);
        }

        // Apply consumable effects based on item name/description
        // This is a simple implementation - can be expanded with item effects system
        ApplyConsumableEffects(item, character);

        // Remove consumable after use
        _inventory.Remove(item);
        _logger.LogInformation("Player {PlayerName} used item: {ItemName}", playerName, item.Name);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Applies the effects of a consumable item to a character.
    /// </summary>
    private void ApplyConsumableEffects(Item item, Character character)
    {
        var itemNameLower = item.Name.ToLower();

        // Mana potions (check first to avoid "potion" matching health)
        if (itemNameLower.Contains("mana") || itemNameLower.Contains("magic") || itemNameLower.Contains("energy"))
        {
            var manaAmount = item.Rarity switch
            {
                ItemRarity.Common => 20,
                ItemRarity.Uncommon => 35,
                ItemRarity.Rare => 50,
                ItemRarity.Epic => 75,
                ItemRarity.Legendary => 100,
                _ => 15
            };

            character.Mana = Math.Min(character.Mana + manaAmount, character.MaxMana);
            _logger.LogDebug("Restored {ManaAmount} mana", manaAmount);
        }
        // Health potions
        else if (itemNameLower.Contains("health") || itemNameLower.Contains("potion") || itemNameLower.Contains("healing"))
        {
            var healAmount = item.Rarity switch
            {
                ItemRarity.Common => 30,
                ItemRarity.Uncommon => 50,
                ItemRarity.Rare => 75,
                ItemRarity.Epic => 100,
                ItemRarity.Legendary => 150,
                _ => 20
            };

            character.Health = Math.Min(character.Health + healAmount, character.MaxHealth);
            _logger.LogDebug("Healed {HealAmount} HP", healAmount);
        }
        // Default: small health boost
        else
        {
            character.Health = Math.Min(character.Health + 10, character.MaxHealth);
            _logger.LogDebug("Applied default consumable effect: +10 HP");
        }
    }

    /// <summary>
    /// Checks if the inventory contains any items of a specific type.
    /// </summary>
    public bool HasItemOfType(ItemType type)
    {
        return _inventory.Any(i => i.Type == type);
    }

    /// <summary>
    /// Gets the total value of all items in the inventory.
    /// </summary>
    public int GetTotalValue()
    {
        return _inventory.Sum(i => i.Price);
    }

    /// <summary>
    /// Clears all items from the inventory.
    /// </summary>
    public void Clear()
    {
        _inventory.Clear();
        _logger.LogInformation("Inventory cleared");
    }

    /// <summary>
    /// Sorts inventory by a specified criterion.
    /// </summary>
    public void SortByName()
    {
        _inventory.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sorts inventory by item type.
    /// </summary>
    public void SortByType()
    {
        _inventory.Sort((a, b) => a.Type.CompareTo(b.Type));
    }

    /// <summary>
    /// Sorts inventory by rarity (descending).
    /// </summary>
    public void SortByRarity()
    {
        _inventory.Sort((a, b) => b.Rarity.CompareTo(a.Rarity)); // Descending (Legendary first)
    }

    /// <summary>
    /// Sorts inventory by value (descending).
    /// </summary>
    public void SortByValue()
    {
        _inventory.Sort((a, b) => b.Price.CompareTo(a.Price)); // Descending (most expensive first)
    }
}