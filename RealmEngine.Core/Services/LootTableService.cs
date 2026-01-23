using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Generators.Modern;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for generating loot based on location loot tables.
/// Handles weighted random selection and rarity filtering.
/// Supports hybrid static references + budget-generated items.
/// </summary>
public class LootTableService
{
    private readonly ILogger<LootTableService> _logger;
    private readonly Random _random;
    private readonly BudgetHelperService? _budgetHelper;
    private readonly ItemGenerator? _itemGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LootTableService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="budgetHelper">Optional budget helper for procedural loot generation.</param>
    /// <param name="itemGenerator">Optional item generator for procedural loot.</param>
    /// <param name="seed">Optional random seed for deterministic results (useful for testing).</param>
    public LootTableService(
        ILogger<LootTableService> logger,
        BudgetHelperService? budgetHelper = null,
        ItemGenerator? itemGenerator = null,
        int? seed = null)
    {
        _logger = logger;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _budgetHelper = budgetHelper;
        _itemGenerator = itemGenerator;
    }

    /// <summary>
    /// Generates loot for a location based on its loot references.
    /// </summary>
    /// <param name="location">The location to generate loot for.</param>
    /// <param name="count">Number of loot items to generate.</param>
    /// <param name="minRarity">Minimum rarity filter (optional).</param>
    /// <param name="maxRarity">Maximum rarity filter (optional).</param>
    /// <returns>List of loot item references.</returns>
    public List<string> GenerateLootForLocation(
        Location location,
        int count = 1,
        ItemRarity? minRarity = null,
        ItemRarity? maxRarity = null)
    {
        if (location == null)
        {
            _logger.LogWarning("Cannot generate loot for null location");
            return new List<string>();
        }

        if (location.Loot == null || !location.Loot.Any())
        {
            _logger.LogInformation("Location {LocationName} has no loot table", location.Name);
            return new List<string>();
        }

        var lootTable = BuildLootTable(location.Loot);
        var generatedLoot = new List<string>();

        for (int i = 0; i < count; i++)
        {
            var lootRef = SelectWeightedLoot(lootTable);
            if (lootRef != null)
            {
                // Apply rarity filter if needed
                // Note: Rarity filtering would require resolving the reference to check rarity
                // For now, we return the reference and let the caller handle filtering
                generatedLoot.Add(lootRef);
            }
        }

        _logger.LogDebug("Generated {Count} loot items for location {LocationName}", 
            generatedLoot.Count, location.Name);

        return generatedLoot;
    }

    /// <summary>
    /// Gets the loot spawn weights for a location by category.
    /// </summary>
    /// <param name="location">The location to analyze.</param>
    /// <returns>Dictionary of category to weight.</returns>
    public Dictionary<string, int> GetLootSpawnWeights(Location location)
    {
        if (location == null || location.Loot == null)
        {
            return new Dictionary<string, int>();
        }

        return BuildLootTable(location.Loot);
    }

    /// <summary>
    /// Gets all unique loot categories for a location.
    /// </summary>
    /// <param name="location">The location to analyze.</param>
    /// <returns>List of loot categories.</returns>
    public List<string> GetLootCategories(Location location)
    {
        if (location == null || location.Loot == null)
        {
            return new List<string>();
        }

        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lootRef in location.Loot)
        {
            var category = ExtractCategory(lootRef);
            if (!string.IsNullOrEmpty(category))
            {
                categories.Add(category);
            }
        }

        return categories.ToList();
    }

    /// <summary>
    /// Calculates the total loot weight for a location.
    /// </summary>
    /// <param name="location">The location to analyze.</param>
    /// <returns>Total weight value.</returns>
    public int GetTotalLootWeight(Location location)
    {
        if (location == null || location.Loot == null)
        {
            return 0;
        }

        var lootTable = BuildLootTable(location.Loot);
        return lootTable.Values.Sum();
    }

    /// <summary>
    /// Builds a weighted loot table from loot references.
    /// Each reference adds 10 weight to its category.
    /// </summary>
    /// <param name="lootReferences">The loot references to process.</param>
    /// <returns>Dictionary of category to weight.</returns>
    private Dictionary<string, int> BuildLootTable(List<string> lootReferences)
    {
        var lootTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var lootRef in lootReferences)
        {
            if (lootRef.StartsWith("@items/", StringComparison.OrdinalIgnoreCase))
            {
                var category = ExtractCategory(lootRef);
                if (!string.IsNullOrEmpty(category))
                {
                    lootTable[category] = lootTable.GetValueOrDefault(category, 0) + 10;
                }
            }
        }

        return lootTable;
    }

    /// <summary>
    /// Extracts the category from an item reference.
    /// Example: "@items/weapons/swords:*" -> "weapons/swords"
    /// </summary>
    /// <param name="reference">The item reference.</param>
    /// <returns>The category, or empty string if invalid.</returns>
    private string ExtractCategory(string reference)
    {
        if (!reference.StartsWith("@items/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // Remove "@items/" prefix
        var refPart = reference.Substring(7);
        
        // Find colon separator
        var colonIndex = refPart.IndexOf(':');
        if (colonIndex > 0)
        {
            return refPart.Substring(0, colonIndex);
        }

        return refPart;
    }

    /// <summary>
    /// Selects a loot reference using weighted random selection.
    /// </summary>
    /// <param name="lootTable">The weighted loot table.</param>
    /// <param name="random">Optional random instance to use (defaults to instance _random).</param>
    /// <returns>A randomly selected loot category, or null if table is empty.</returns>
    private string? SelectWeightedLoot(Dictionary<string, int> lootTable, Random? random = null)
    {
        if (lootTable == null || !lootTable.Any())
        {
            return null;
        }

        var rng = random ?? _random;
        var totalWeight = lootTable.Values.Sum();
        var roll = rng.Next(totalWeight);
        var cumulative = 0;

        foreach (var (category, weight) in lootTable)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                // Return a wildcard reference for this category
                return $"@items/{category}:*";
            }
        }

        // Fallback to first category (should never reach here)
        return $"@items/{lootTable.Keys.First()}:*";
    }

    /// <summary>
    /// Generates loot with specific category preference.
    /// </summary>
    /// <param name="location">The location to generate loot for.</param>
    /// <param name="preferredCategory">The preferred loot category (e.g., "weapons/swords").</param>
    /// <param name="count">Number of items to generate.</param>
    /// <param name="seed">Optional random seed for deterministic results (creates temporary RNG instance).</param>
    /// <returns>List of loot references, preferring the specified category.</returns>
    public List<string> GenerateLootWithPreference(
        Location location,
        string preferredCategory,
        int count = 1,
        int? seed = null)
    {
        if (location == null || location.Loot == null || !location.Loot.Any())
        {
            return new List<string>();
        }

        var lootTable = BuildLootTable(location.Loot);
        
        // Boost the preferred category weight by 50%
        if (lootTable.ContainsKey(preferredCategory))
        {
            lootTable[preferredCategory] = (int)(lootTable[preferredCategory] * 1.5);
        }

        // Use provided seed or instance random
        var random = seed.HasValue ? new Random(seed.Value) : _random;

        var generatedLoot = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var lootRef = SelectWeightedLoot(lootTable, random);
            if (lootRef != null)
            {
                generatedLoot.Add(lootRef);
            }
        }

        return generatedLoot;
    }

    /// <summary>
    /// Generates loot for a chest using hybrid approach: static references + budget-generated items.
    /// </summary>
    /// <param name="chestRarity">The chest rarity tier</param>
    /// <param name="locationLevel">The location level where chest is found</param>
    /// <param name="itemCategory">The item category to generate (weapons, armor, etc.)</param>
    /// <param name="count">Number of items to generate</param>
    /// <param name="staticReferences">Optional static loot table references (e.g., legendary items)</param>
    /// <returns>List of generated items (async)</returns>
    public async Task<List<Item>> GenerateChestLootAsync(
        RarityTier chestRarity,
        int locationLevel,
        string itemCategory,
        int count = 1,
        List<string>? staticReferences = null)
    {
        var items = new List<Item>();

        if (_budgetHelper == null || _itemGenerator == null)
        {
            _logger.LogWarning("Cannot generate procedural chest loot without BudgetHelper and ItemGenerator");
            return items;
        }

        // 20% chance for static legendary items if provided
        if (staticReferences != null && staticReferences.Any() && _random.Next(100) < 20)
        {
            _logger.LogInformation("Chest contains static legendary item from loot table");
            
            // Select random static reference
            var selectedRef = staticReferences[_random.Next(staticReferences.Count)];
            
            // Try to resolve and generate from reference
            if (selectedRef.StartsWith("@"))
            {
                // Parse reference: @items/weapons/swords:legendary-blade
                var parts = selectedRef.TrimStart('@').Split(':');
                if (parts.Length == 2)
                {
                    var pathParts = parts[0].Split('/');
                    var category = pathParts.Length >= 2 ? pathParts[1] : pathParts[0];
                    var itemName = parts[1];

                    var staticItem = await _itemGenerator.GenerateItemByNameAsync(category, itemName, hydrate: true);
                    if (staticItem != null)
                    {
                        items.Add(staticItem);
                        count--;
                        _logger.LogInformation("Generated static legendary item: {ItemName}", staticItem.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to resolve static reference: {Ref}", selectedRef);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Invalid static reference format (expected @reference): {Ref}", selectedRef);
            }
        }

        // Generate remaining items using budget system (80% of loot)
        var itemBudget = _budgetHelper.GetBudgetForChest(chestRarity, locationLevel);
        
        for (int i = 0; i < count; i++)
        {
            var request = new BudgetItemRequest
            {
                EnemyType = "treasure_chest",
                EnemyLevel = locationLevel,
                ItemCategory = itemCategory,
                AllowQuality = true
            };

            var item = await _itemGenerator.GenerateItemWithBudgetAsync(request);
            items.Add(item);
            
            _logger.LogDebug("Generated chest loot: {ItemName} (budget={Budget}, rarity={Rarity})",
                item.Name, itemBudget, chestRarity);
        }

        return items;
    }
}
