using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Generators.Modern;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for managing NPC shop economy, including hybrid inventory,
/// player trading, item decay, and dynamic pricing.
/// Implements the v4.0 shop economy system from NPC catalog.
/// Supports hybrid static catalog + budget-generated inventory.
/// </summary>
public class ShopEconomyService
{
    private readonly Dictionary<string, ShopInventory> _shopInventories = new();
    private DateTime _lastRefreshDate = DateTime.UtcNow.Date;
    private readonly ItemDataService _catalogLoader;
    private readonly Random _random = new();
    private readonly BudgetHelperService? _budgetHelper;
    private readonly ItemGenerator? _itemGenerator;

    /// <summary>
    /// Initializes a new instance of the ShopEconomyService class.
    /// </summary>
    /// <param name="catalogLoader">The item catalog loader.</param>
    /// <param name="budgetHelper">Optional budget helper for procedural inventory generation.</param>
    /// <param name="itemGenerator">Optional item generator for procedural inventory.</param>
    public ShopEconomyService(
        ItemDataService catalogLoader,
        BudgetHelperService? budgetHelper = null,
        ItemGenerator? itemGenerator = null)
    {
        _catalogLoader = catalogLoader;
        _budgetHelper = budgetHelper;
        _itemGenerator = itemGenerator;
    }

    /// <summary>
    /// Get or create shop inventory for an NPC.
    /// </summary>
    /// <param name="npc">The NPC merchant.</param>
    /// <returns>The shop inventory.</returns>
    public ShopInventory GetOrCreateInventory(NPC npc)
    {
        if (!npc.Traits.ContainsKey("isMerchant") || !npc.Traits["isMerchant"].AsBool())
        {
            throw new InvalidOperationException($"NPC {npc.Name} is not a merchant");
        }

        var npcId = npc.Id;

        if (!_shopInventories.ContainsKey(npcId))
        {
            _shopInventories[npcId] = CreateInitialInventory(npc);
            _logger.LogInformation("Created initial inventory for merchant {MerchantName}", npc.Name);
        }

        // Check if daily refresh is needed
        RefreshInventoryIfNeeded(npc, _shopInventories[npcId]);

        return _shopInventories[npcId];
    }

    /// <summary>
    /// Calculate the sell price for an item (merchant sells to player).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="merchant">The merchant NPC.</param>
    /// <returns>The sell price.</returns>
    public int CalculateSellPrice(Item item, NPC merchant)
    {
        var basePrice = item.Price;

        // Apply quality pricing: (100 / AccumulatedRarityWeight) * basePrice
        // Higher rarity weight = rarer = more expensive
        var qualityMultiplier = CalculateQualityMultiplier(item);
        var price = (int)(basePrice * qualityMultiplier);

        // Apply merchant background modifiers
        price = ApplyBackgroundModifiers(price, merchant, isBuying: false);

        // Apply merchant trait modifiers
        price = ApplyTraitModifiers(price, merchant, isBuying: false);

        return Math.Max(1, price); // Minimum 1 gold
    }

    /// <summary>
    /// Calculate the buy price for an item (merchant buys from player).
    /// Default: Player sells at 40% of merchant's sell price.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="merchant">The merchant NPC.</param>
    /// <returns>The buy price.</returns>
    public int CalculateBuyPrice(Item item, NPC merchant)
    {
        var sellPrice = CalculateSellPrice(item, merchant);

        // Base buy percentage (merchant buys at 40% of sell price)
        var buyPercentage = 0.40;

        // Apply background buy price modifiers
        if (merchant.Traits.ContainsKey("backgroundBuyPriceMultiplier"))
        {
            buyPercentage *= merchant.Traits["backgroundBuyPriceMultiplier"].AsDouble();
        }

        // Apply trait buy price modifiers
        if (merchant.Traits.ContainsKey("traitBuyPriceMultiplier"))
        {
            buyPercentage *= merchant.Traits["traitBuyPriceMultiplier"].AsDouble();
        }

        var price = (int)(sellPrice * buyPercentage);
        return Math.Max(1, price); // Minimum 1 gold
    }

    /// <summary>
    /// Calculate resell price (merchant resells player item at 80%).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="merchant">The merchant NPC.</param>
    /// <returns>The resell price.</returns>
    public int CalculateResellPrice(Item item, NPC merchant)
    {
        var originalPrice = CalculateSellPrice(item, merchant);
        return (int)(originalPrice * 0.80); // 80% of original price
    }

    /// <summary>
    /// Buy item from player (player sells to merchant).
    /// </summary>
    /// <param name="merchant">The merchant NPC.</param>
    /// <param name="item">The item to buy.</param>
    /// <param name="pricePaid">The price paid to player.</param>
    /// <returns>True if purchase was successful.</returns>
    public bool BuyFromPlayer(NPC merchant, Item item, out int pricePaid)
    {
        pricePaid = 0;

        // Check if merchant accepts player items
        var acceptsPlayerItems = merchant.Traits.ContainsKey("shopInventoryType") &&
                                 merchant.Traits["shopInventoryType"].AsString() == "hybrid";

        if (!acceptsPlayerItems)
        {
            _logger.LogWarning("Merchant {MerchantName} does not accept player items", merchant.Name);
            return false;
        }

        // Calculate buy price
        pricePaid = CalculateBuyPrice(item, merchant);

        // Check merchant gold
        if (merchant.Gold < pricePaid)
        {
            _logger.LogWarning("Merchant {MerchantName} cannot afford item (has {Gold}, needs {Price})",
                merchant.Name, merchant.Gold, pricePaid);
            return false;
        }

        // Add item to merchant inventory with decay tracking
        var inventory = GetOrCreateInventory(merchant);
        var playerItem = new PlayerSoldItem
        {
            Item = item,
            PurchaseDate = DateTime.UtcNow.Date,
            DaysRemaining = 7, // 7-day decay
            ResellPrice = CalculateResellPrice(item, merchant)
        };
        inventory.PlayerSoldItems.Add(playerItem);

        // Deduct gold from merchant
        merchant.Gold -= pricePaid;

        _logger.LogInformation("Merchant {MerchantName} bought {ItemName} from player for {Price}g",
            merchant.Name, item.Name, pricePaid);

        return true;
    }

    /// <summary>
    /// Sell item to player (player buys from merchant).
    /// </summary>
    /// <param name="merchant">The merchant NPC.</param>
    /// <param name="item">The item to sell.</param>
    /// <param name="priceCharged">The price charged to player.</param>
    /// <returns>True if sale was successful.</returns>
    public bool SellToPlayer(NPC merchant, Item item, out int priceCharged)
    {
        priceCharged = CalculateSellPrice(item, merchant);

        var inventory = GetOrCreateInventory(merchant);

        // Check if item is in core inventory
        var coreItem = inventory.CoreItems.FirstOrDefault(i => i.Id == item.Id);
        if (coreItem != null)
        {
            // Core items have unlimited quantity
            _logger.LogInformation("Merchant {MerchantName} sold core item {ItemName} to player for {Price}g",
                merchant.Name, item.Name, priceCharged);
            return true;
        }

        // Check if item is in dynamic inventory
        var dynamicItem = inventory.DynamicItems.FirstOrDefault(i => i.Id == item.Id);
        if (dynamicItem != null)
        {
            inventory.DynamicItems.Remove(dynamicItem);
            _logger.LogInformation("Merchant {MerchantName} sold dynamic item {ItemName} to player for {Price}g",
                merchant.Name, item.Name, priceCharged);
            return true;
        }

        // Check if item is player-sold item
        var playerItem = inventory.PlayerSoldItems.FirstOrDefault(pi => pi.Item.Id == item.Id);
        if (playerItem != null)
        {
            priceCharged = playerItem.ResellPrice;
            inventory.PlayerSoldItems.Remove(playerItem);
            _logger.LogInformation("Merchant {MerchantName} sold player item {ItemName} to player for {Price}g",
                merchant.Name, item.Name, priceCharged);
            return true;
        }

        _logger.LogWarning("Item {ItemName} not found in merchant {MerchantName} inventory",
            item.Name, merchant.Name);
        return false;
    }

    /// <summary>
    /// Refresh shop inventory if schedule requires it (daily refresh).
    /// </summary>
    private void RefreshInventoryIfNeeded(NPC merchant, ShopInventory inventory)
    {
        var today = DateTime.UtcNow.Date;

        // Check if it's a new day
        if (today > _lastRefreshDate)
        {
            _lastRefreshDate = today;

            // Refresh dynamic items
            RefreshDynamicInventory(merchant, inventory);

            // Apply decay to player-sold items
            ApplyPlayerItemDecay(inventory);

            _logger.LogInformation("Refreshed inventory for merchant {MerchantName}", merchant.Name);
        }
    }

    /// <summary>
    /// Create initial inventory for a merchant NPC.
    /// </summary>
    private ShopInventory CreateInitialInventory(NPC npc)
    {
        var inventory = new ShopInventory
        {
            MerchantId = npc.Id,
            MerchantName = npc.Name,
            LastRefresh = DateTime.UtcNow.Date
        };

        // Load core items from catalog based on shop type
        var shopType = GetShopType(npc);
        var coreItemCount = GetCoreItemCount(npc);
        
        inventory.CoreItems = GenerateCoreInventory(shopType, coreItemCount);
        
        _logger.LogInformation("Created initial inventory for {MerchantName} ({ShopType}) with {CoreCount} core items",
            npc.Name, shopType, inventory.CoreItems.Count);

        return inventory;
    }

    /// <summary>
    /// Refresh dynamic inventory items (daily).
    /// </summary>
    private void RefreshDynamicInventory(NPC merchant, ShopInventory inventory)
    {
        // Clear existing dynamic items
        inventory.DynamicItems.Clear();

        // Generate new dynamic items
        var shopType = GetShopType(merchant);
        var dynamicCount = GetDynamicItemCount(merchant);
        
        inventory.DynamicItems = GenerateDynamicInventory(shopType, dynamicCount);

        inventory.LastRefresh = DateTime.UtcNow.Date;
        
        _logger.LogInformation("Refreshed dynamic inventory for {MerchantName} with {DynamicCount} items",
            merchant.Name, inventory.DynamicItems.Count);
    }

    /// <summary>
    /// Apply 10% daily decay to player-sold items (7-day expiration).
    /// </summary>
    private void ApplyPlayerItemDecay(ShopInventory inventory)
    {
        var itemsToRemove = new List<PlayerSoldItem>();

        foreach (var playerItem in inventory.PlayerSoldItems)
        {
            // Calculate days since purchase
            var daysSincePurchase = (DateTime.UtcNow.Date - playerItem.PurchaseDate).Days;
            playerItem.DaysRemaining = Math.Max(0, 7 - daysSincePurchase);

            // Apply 10% price decay per day
            var decayFactor = Math.Pow(0.90, daysSincePurchase);
            var originalResellPrice = playerItem.ResellPrice;
            playerItem.ResellPrice = (int)(originalResellPrice * decayFactor);

            // Remove if expired (7 days passed)
            if (playerItem.DaysRemaining == 0)
            {
                itemsToRemove.Add(playerItem);
                _logger.LogInformation("Player item {ItemName} expired from inventory", playerItem.Item.Name);
            }
        }

        // Remove expired items
        foreach (var item in itemsToRemove)
        {
            inventory.PlayerSoldItems.Remove(item);
        }
    }

    /// <summary>
    /// Calculate quality multiplier based on item rarity weight.
    /// Formula: 100 / AccumulatedRarityWeight
    /// </summary>
    private double CalculateQualityMultiplier(Item item)
    {
        // Use item rarity as a proxy for quality
        // Rarity weights: Common=100, Uncommon=70, Rare=40, Epic=20, Legendary=10
        var rarityWeight = item.Rarity switch
        {
            ItemRarity.Common => 100,
            ItemRarity.Uncommon => 70,
            ItemRarity.Rare => 40,
            ItemRarity.Epic => 20,
            ItemRarity.Legendary => 10,
            _ => 50 // Default for unknown rarities
        };

        return 100.0 / Math.Max(1, rarityWeight);
    }

    /// <summary>
    /// Apply background shop modifiers to price.
    /// </summary>
    private int ApplyBackgroundModifiers(int basePrice, NPC merchant, bool isBuying)
    {
        var price = (double)basePrice;

        if (merchant.Traits.ContainsKey("backgroundPriceMultiplier"))
        {
            var multiplier = merchant.Traits["backgroundPriceMultiplier"].AsDouble();
            price *= multiplier;
        }

        return (int)price;
    }

    /// <summary>
    /// Apply personality trait shop modifiers to price.
    /// </summary>
    private int ApplyTraitModifiers(int basePrice, NPC merchant, bool isBuying)
    {
        var price = (double)basePrice;

        if (merchant.Traits.ContainsKey("traitPriceMultiplier"))
        {
            var multiplier = merchant.Traits["traitPriceMultiplier"].AsDouble();
            price *= multiplier;
        }

        // Apply quality bonus if present
        if (merchant.Traits.ContainsKey("traitQualityBonus"))
        {
            var qualityBonus = merchant.Traits["traitQualityBonus"].AsInt();
            // Quality bonus increases price slightly
            price *= (1.0 + (qualityBonus / 100.0));
        }

        return (int)price;
    }

    /// <summary>
    /// Get shop type from merchant traits.
    /// </summary>
    private string GetShopType(NPC merchant)
    {
        if (merchant.Traits.TryGetValue("shopType", out var shopTypeTrait))
        {
            return shopTypeTrait.AsString().ToLower();
        }

        // Default to general store
        return "general";
    }

    /// <summary>
    /// Get core item count from merchant traits.
    /// </summary>
    private int GetCoreItemCount(NPC merchant)
    {
        if (merchant.Traits.TryGetValue("shopCoreCount", out var coreCountTrait))
        {
            return coreCountTrait.AsInt();
        }

        // Default based on shop type
        return GetShopType(merchant) switch
        {
            "weaponsmith" => 10,
            "armorer" => 10,
            "apothecary" => 15,
            "general" => 20,
            _ => 15
        };
    }

    /// <summary>
    /// Get dynamic item count from merchant traits.
    /// </summary>
    private int GetDynamicItemCount(NPC merchant)
    {
        if (merchant.Traits.TryGetValue("shopDynamicCount", out var dynamicCountTrait))
        {
            return dynamicCountTrait.AsInt();
        }

        // Default to 5-10 dynamic items
        return _random.Next(5, 11);
    }

    /// <summary>
    /// Generate core inventory items based on shop type.
    /// Core items are always available with unlimited quantity.
    /// </summary>
    private List<Item> GenerateCoreInventory(string shopType, int count)
    {
        var categories = GetCategoriesForShopType(shopType);
        var items = _catalogLoader.LoadMultipleCategories(categories, ItemRarity.Common);
        
        return SelectItemsByWeight(items, count);
    }

    /// <summary>
    /// Generate dynamic inventory items that refresh daily.
    /// Includes uncommon and rare items with limited availability.
    /// </summary>
    private List<Item> GenerateDynamicInventory(string shopType, int count)
    {
        var categories = GetCategoriesForShopType(shopType);
        var items = _catalogLoader.LoadMultipleCategories(categories);
        
        // Weight towards uncommon/rare for dynamic inventory
        var filtered = items.Where(i => i.Rarity >= ItemRarity.Uncommon).ToList();
        
        if (filtered.Count == 0)
        {
            filtered = items; // Fallback to all if no uncommon+ items
        }
        
        return SelectItemsByWeight(filtered, count);
    }

    /// <summary>
    /// Get item categories appropriate for shop type.
    /// </summary>
    private List<string> GetCategoriesForShopType(string shopType)
    {
        return shopType.ToLower() switch
        {
            "weaponsmith" => new List<string> { "weapons" },
            "armorer" => new List<string> { "armor" },
            "apothecary" => new List<string> { "consumables" },
            "alchemist" => new List<string> { "consumables" },
            "general" => new List<string> { "weapons", "armor", "consumables" },
            "blacksmith" => new List<string> { "weapons", "armor" },
            _ => new List<string> { "consumables" } // Default to potions
        };
    }

    /// <summary>
    /// Select items from templates using weighted random selection.
    /// </summary>
    private List<Item> SelectItemsByWeight(List<ItemTemplate> templates, int count)
    {
        var selectedItems = new List<Item>();
        
        if (templates.Count == 0)
        {
            _logger.LogWarning("No item templates available for selection");
            return selectedItems;
        }

        // Calculate total weight
        int totalWeight = templates.Sum(t => t.RarityWeight);
        
        for (int i = 0; i < count && templates.Count > 0; i++)
        {
            // Weighted random selection
            int roll = _random.Next(totalWeight);
            int cumulative = 0;
            
            ItemTemplate? selected = null;
            
            foreach (var template in templates)
            {
                cumulative += template.RarityWeight;
                if (roll < cumulative)
                {
                    selected = template;
                    break;
                }
            }

            if (selected != null)
            {
                // Create item from template
                var item = CreateItemFromTemplate(selected);
                selectedItems.Add(item);
            }
        }

        return selectedItems;
    }

    /// <summary>
    /// Create an Item instance from an ItemTemplate.
    /// </summary>
    private Item CreateItemFromTemplate(ItemTemplate template)
    {
        return new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = template.Name,
            Price = template.BasePrice,
            Rarity = template.Rarity,
            TotalRarityWeight = template.RarityWeight,
            Type = template.Category switch
            {
                "weapons" => ItemType.Weapon,
                "armor" => ItemType.Chest, // Default armor pieces to chest
                "consumables" => ItemType.Consumable,
                _ => ItemType.Consumable
            }
        };
    }
}

/// <summary>
/// Represents a merchant's shop inventory.
/// </summary>
public class ShopInventory
{
    /// <summary>
    /// Gets or sets the unique identifier of the merchant who owns this shop.
    /// </summary>
    public string MerchantId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name of the merchant.
    /// </summary>
    public string MerchantName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the timestamp of the last inventory refresh.
    /// </summary>
    public DateTime LastRefresh { get; set; }

    /// <summary>
    /// Core items that are always available (unlimited quantity).
    /// </summary>
    public List<Item> CoreItems { get; set; } = new();

    /// <summary>
    /// Dynamic items that refresh daily (limited quantity).
    /// </summary>
    public List<Item> DynamicItems { get; set; } = new();

    /// <summary>
    /// Items purchased from player (7-day decay, limited quantity).
    /// </summary>
    public List<PlayerSoldItem> PlayerSoldItems { get; set; } = new();
}

/// <summary>
/// Represents an item purchased from the player.
/// Items decay over 7 days and are then removed from shop inventory.
/// </summary>
public class PlayerSoldItem
{
    /// <summary>
    /// Gets or sets the item that was sold by the player.
    /// </summary>
    public Item Item { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the date when the merchant purchased this item from the player.
    /// </summary>
    public DateTime PurchaseDate { get; set; }
    
    /// <summary>
    /// Gets or sets the number of days remaining before this item is removed from inventory.
    /// </summary>
    public int DaysRemaining { get; set; }
    
    /// <summary>
    /// Gets or sets the resale price the merchant is asking for this item.
    /// </summary>
    public int ResellPrice { get; set; }
}