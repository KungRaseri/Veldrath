using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Services;

/// <summary>
/// Generic service for loading and processing loot tables across all game systems.
/// Supports harvesting, enemies, chests with inheritance and override mechanics.
/// Uses GameDataCache for efficient data access.
/// </summary>
public class LootTableService
{
    private readonly ILogger<LootTableService> _logger;
    private readonly GameDataCache _dataCache;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="LootTableService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="dataCache">The game data cache instance.</param>
    /// <param name="seed">Optional seed for random number generation (for testing).</param>
    public LootTableService(
        ILogger<LootTableService> logger,
        GameDataCache dataCache,
        int? seed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Rolls loot drops for a harvestable node based on its loot table reference.
    /// </summary>
    /// <param name="lootTableRef">The loot table reference (e.g., "@loot-tables/harvesting/woods:oak").</param>
    /// <param name="baseYield">The base yield quantity calculated for the harvest.</param>
    /// <param name="isCriticalHarvest">Whether this is a critical harvest (affects bonus drop chances).</param>
    /// <returns>List of item drops with material references and quantities.</returns>
    public List<ItemDrop> RollHarvestingDrops(string lootTableRef, int baseYield, bool isCriticalHarvest)
    {
        if (string.IsNullOrWhiteSpace(lootTableRef))
        {
            _logger.LogWarning("Cannot roll loot for null/empty loot table reference");
            return new List<ItemDrop>();
        }

        // Parse the reference (format: @loot-tables/harvesting/woods:oak)
        var (catalogKey, typeName, nodeName) = ParseHarvestingReference(lootTableRef);
        if (catalogKey == null || typeName == null || nodeName == null)
        {
            _logger.LogWarning("Invalid loot table reference format: {Reference}", lootTableRef);
            return new List<ItemDrop>();
        }

        // Get catalog from cache
        var catalogFile = _dataCache.GetLootTableCatalog(catalogKey);
        if (catalogFile == null)
        {
            _logger.LogWarning("Loot table catalog not found: {CatalogKey}", catalogKey);
            return new List<ItemDrop>();
        }

        var catalog = catalogFile.JsonData;

        // Navigate: catalog.types[typeName].nodes[nodeName].drops
        var typeNode = catalog["types"]?[typeName];
        if (typeNode == null)
        {
            _logger.LogWarning("Type not found in catalog: {TypeName}", typeName);
            return new List<ItemDrop>();
        }

        var nodeData = typeNode["nodes"]?[nodeName];
        if (nodeData == null)
        {
            _logger.LogWarning("Node not found in type: {NodeName}", nodeName);
            return new List<ItemDrop>();
        }

        var drops = new List<ItemDrop>();
        var dropsArray = nodeData["drops"] as JArray;

        if (dropsArray == null)
        {
            _logger.LogWarning("Node {NodeName} has no drops array", nodeName);
            return drops;
        }

        foreach (var dropToken in dropsArray)
        {
            var dropData = dropToken as JObject;
            if (dropData == null) continue;

            var materialRef = dropData["materialRef"]?.Value<string>();
            var dropChance = dropData["dropChance"]?.Value<int>() ?? 0;
            var isBonusDrop = dropData["isBonusDrop"]?.Value<bool>() ?? false;
            var requiresCritical = dropData["requiresCritical"]?.Value<bool>() ?? false;

            // Skip critical-only drops if not a critical harvest
            if (requiresCritical && !isCriticalHarvest)
            {
                continue;
            }

            // Roll drop chance
            var roll = _random.Next(1, 101);
            if (roll > dropChance)
            {
                continue; // Drop failed
            }

            // Calculate quantity
            var minQuantity = dropData["minQuantity"]?.Value<int>() ?? 1;
            var maxQuantity = dropData["maxQuantity"]?.Value<int>() ?? 1;
            var quantity = _random.Next(minQuantity, maxQuantity + 1);

            // Scale by base yield (only for non-bonus drops)
            if (!isBonusDrop && baseYield > 1)
            {
                quantity = (int)Math.Ceiling(quantity * (baseYield / 1.0));
            }

            if (!string.IsNullOrEmpty(materialRef))
            {
                drops.Add(new ItemDrop
                {
                    ItemRef = materialRef,
                    ItemName = ExtractItemName(materialRef),
                    Quantity = quantity,
                    IsBonus = isBonusDrop
                });
            }
        }

        _logger.LogDebug("Rolled {Count} drops for {Reference} (critical: {IsCritical})", 
            drops.Count, lootTableRef, isCriticalHarvest);

        return drops;
    }

    /// <summary>
    /// Rolls loot drops for an enemy with inheritance from type defaults.
    /// </summary>
    /// <param name="lootTableRef">The loot table reference (e.g., "@loot-tables/enemies/humanoids:goblin").</param>
    /// <returns>Result containing drops and gold amount.</returns>
    public EnemyLootResult RollEnemyDrops(string lootTableRef)
    {
        if (string.IsNullOrWhiteSpace(lootTableRef))
        {
            _logger.LogWarning("Cannot roll enemy loot for null/empty reference");
            return new EnemyLootResult();
        }

        var (catalogKey, typeName, enemyName) = ParseHarvestingReference(lootTableRef);
        if (catalogKey == null || typeName == null || enemyName == null)
        {
            _logger.LogWarning("Invalid enemy loot table reference: {Reference}", lootTableRef);
            return new EnemyLootResult();
        }

        // Get catalog from cache
        var catalogFile = _dataCache.GetLootTableCatalog(catalogKey);
        if (catalogFile == null)
        {
            _logger.LogWarning("Enemy catalog not found: {CatalogKey}", catalogKey);
            return new EnemyLootResult();
        }

        var catalog = catalogFile.JsonData;

        var typeNode = catalog["types"]?[typeName];
        if (typeNode == null)
        {
            _logger.LogWarning("Enemy type not found: {TypeName}", typeName);
            return new EnemyLootResult();
        }

        var enemyData = typeNode["enemies"]?[enemyName];
        if (enemyData == null)
        {
            _logger.LogWarning("Enemy not found: {EnemyName}", enemyName);
            return new EnemyLootResult();
        }

        // Apply inheritance: enemy-specific fields override type defaults
        var poolRefs = GetPoolRefsWithInheritance(typeNode, enemyData);
        var goldRange = GetGoldRangeWithInheritance(typeNode, enemyData);
        var dropChance = GetDropChanceWithInheritance(typeNode, enemyData);

        var result = new EnemyLootResult
        {
            Gold = _random.Next(goldRange.min, goldRange.max + 1),
            Drops = new List<ItemDrop>()
        };

        // Roll pool-based drops
        var poolRoll = _random.Next(1, 101);
        if (poolRoll <= dropChance)
        {
            // TODO: Integrate with MaterialPoolService to resolve pool references
            _logger.LogDebug("Pool drops would be rolled from: {PoolRefs}", string.Join(", ", poolRefs));
        }

        // Roll guaranteed drops
        var guaranteedDrops = enemyData["guaranteedDrops"] as JArray;
        if (guaranteedDrops != null)
        {
            foreach (var dropToken in guaranteedDrops)
            {
                var dropData = dropToken as JObject;
                if (dropData == null) continue;

                var materialRef = dropData["materialRef"]?.Value<string>();
                var dropChance2 = dropData["dropChance"]?.Value<int>() ?? 100;

                var roll = _random.Next(1, 101);
                if (roll > dropChance2) continue;

                var minQuantity = dropData["minQuantity"]?.Value<int>() ?? 1;
                var maxQuantity = dropData["maxQuantity"]?.Value<int>() ?? 1;
                var quantity = _random.Next(minQuantity, maxQuantity + 1);

                if (!string.IsNullOrEmpty(materialRef))
                {
                    result.Drops.Add(new ItemDrop
                    {
                        ItemRef = materialRef,
                        ItemName = ExtractItemName(materialRef),
                        Quantity = quantity,
                        IsBonus = false
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Rolls loot for a chest with support for multi-lookup merge strategies.
    /// </summary>
    /// <param name="lootLookups">List of loot table references to merge.</param>
    /// <param name="mergeStrategy">How to combine multiple lookups (addPools, prioritizeFirst, prioritizeLast).</param>
    /// <returns>Result containing drops and gold amount.</returns>
    public ChestLootResult RollChestDrops(List<string> lootLookups, string mergeStrategy = "addPools")
    {
        if (lootLookups == null || lootLookups.Count == 0)
        {
            _logger.LogWarning("Cannot roll chest loot with empty lookups");
            return new ChestLootResult();
        }

        // TODO: Implement merge strategies and chest loot rolling
        _logger.LogDebug("Rolling chest loot with strategy {Strategy}: {Lookups}", 
            mergeStrategy, string.Join(", ", lootLookups));

        return new ChestLootResult
        {
            Gold = 0,
            Drops = new List<ItemDrop>()
        };
    }

    private (string? catalogKey, string? typeName, string? itemName) ParseHarvestingReference(string reference)
    {
        // Format: @loot-tables/harvesting/woods:oak
        if (!reference.StartsWith("@loot-tables/"))
            return (null, null, null);

        var withoutPrefix = reference.Substring("@loot-tables/".Length);
        var parts = withoutPrefix.Split(':');
        if (parts.Length != 2)
            return (null, null, null);

        var pathParts = parts[0].Split('/');
        if (pathParts.Length != 2)
            return (null, null, null);

        return (pathParts[0], pathParts[1], parts[1]);
    }

    private string ExtractItemName(string? materialRef)
    {
        if (string.IsNullOrWhiteSpace(materialRef))
            return "Unknown Material";

        // Get the part after the last colon
        var colonIndex = materialRef.LastIndexOf(':');
        if (colonIndex < 0)
            return "Unknown Material";

        var itemId = materialRef.Substring(colonIndex + 1);
        
        // Convert kebab-case to Title Case
        var words = itemId.Split('-');
        var titleCase = string.Join(" ", words.Select(w => 
            char.ToUpper(w[0]) + w.Substring(1).ToLower()));
        
        return titleCase;
    }

    private List<string> GetPoolRefsWithInheritance(JToken typeNode, JToken enemyNode)
    {
        // Check for additionalPools (additive inheritance)
        var additionalPools = enemyNode["additionalPools"] as JArray;
        if (additionalPools != null)
        {
            var basePools = typeNode["poolRefs"]?.ToObject<List<string>>() ?? new List<string>();
            var extraPools = additionalPools.ToObject<List<string>>() ?? new List<string>();
            return basePools.Concat(extraPools).ToList();
        }

        // Check for poolRefs override (replace inheritance)
        var enemyPools = enemyNode["poolRefs"] as JArray;
        if (enemyPools != null)
        {
            return enemyPools.ToObject<List<string>>() ?? new List<string>();
        }

        // Default: inherit from type
        return typeNode["poolRefs"]?.ToObject<List<string>>() ?? new List<string>();
    }

    private (int min, int max) GetGoldRangeWithInheritance(JToken typeNode, JToken enemyNode)
    {
        var enemyGoldRange = enemyNode["goldRange"] as JArray;
        if (enemyGoldRange != null && enemyGoldRange.Count == 2)
        {
            return (enemyGoldRange[0].Value<int>(), enemyGoldRange[1].Value<int>());
        }

        var typeGoldRange = typeNode["goldRange"] as JArray;
        if (typeGoldRange != null && typeGoldRange.Count == 2)
        {
            return (typeGoldRange[0].Value<int>(), typeGoldRange[1].Value<int>());
        }

        return (0, 0);
    }

    private int GetDropChanceWithInheritance(JToken typeNode, JToken enemyNode)
    {
        var enemyChance = enemyNode["dropChance"]?.Value<int>();
        if (enemyChance.HasValue)
        {
            return enemyChance.Value;
        }

        return typeNode["dropChance"]?.Value<int>() ?? 0;
    }
}

/// <summary>
/// Result of rolling enemy loot drops.
/// </summary>
public class EnemyLootResult
{
    /// <summary>
    /// Gold amount dropped.
    /// </summary>
    public int Gold { get; set; }
    
    /// <summary>
    /// List of item drops with quantities.
    /// </summary>
    public List<ItemDrop> Drops { get; set; } = new List<ItemDrop>();
}

/// <summary>
/// Result of rolling chest loot drops.
/// </summary>
public class ChestLootResult
{
    /// <summary>
    /// Gold amount in chest.
    /// </summary>
    public int Gold { get; set; }
    
    /// <summary>
    /// List of item drops with quantities.
    /// </summary>
    public List<ItemDrop> Drops { get; set; } = new List<ItemDrop>();
}
