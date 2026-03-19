using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services;

/// <summary>
/// Generic service for loading and processing loot tables across all game systems.
/// Supports harvesting, enemies, chests with weighted random selection.
/// </summary>
public class LootTableService
{
    private readonly ILogger<LootTableService> _logger;
    private readonly ILootTableRepository _lootTableRepo;
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="LootTableService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="lootTableRepo">The loot table repository.</param>
    /// <param name="dbFactory">The database context factory for material pool lookups.</param>
    /// <param name="seed">Optional seed for random number generation (for testing).</param>
    public LootTableService(
        ILogger<LootTableService> logger,
        ILootTableRepository lootTableRepo,
        IDbContextFactory<ContentDbContext> dbFactory,
        int? seed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lootTableRepo = lootTableRepo ?? throw new ArgumentNullException(nameof(lootTableRepo));
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Rolls loot drops for a harvestable node based on its loot table reference.
    /// </summary>
    /// <param name="lootTableRef">The loot table reference (e.g., "@loot-tables/harvesting/woods:oak").</param>
    /// <param name="baseYield">The base yield quantity calculated for the harvest.</param>
    /// <param name="isCriticalHarvest">Whether this is a critical harvest (affects bonus drop chances).</param>
    /// <returns>List of item drops with material references and quantities.</returns>
    public async Task<List<ItemDrop>> RollHarvestingDrops(string lootTableRef, int baseYield, bool isCriticalHarvest)
    {
        if (string.IsNullOrWhiteSpace(lootTableRef))
        {
            _logger.LogWarning("Cannot roll loot for null/empty loot table reference");
            return [];
        }

        var slug = ExtractSlugFromRef(lootTableRef);
        if (slug == null)
        {
            _logger.LogWarning("Invalid loot table reference format: {Reference}", lootTableRef);
            return [];
        }

        var lootTable = await _lootTableRepo.GetBySlugAsync(slug);
        if (lootTable == null)
        {
            _logger.LogWarning("Loot table not found for slug: {Slug}", slug);
            return [];
        }

        var drops = new List<ItemDrop>();
        foreach (var entry in lootTable.Entries)
        {
            if (!entry.IsGuaranteed)
            {
                var roll = _random.Next(1, 101);
                if (roll > entry.DropWeight)
                    continue;
            }

            var quantity = _random.Next(entry.QuantityMin, entry.QuantityMax + 1);
            if (baseYield > 1)
                quantity = (int)Math.Ceiling(quantity * (baseYield / 1.0));

            var itemRef = $"@{entry.ItemDomain}:{entry.ItemSlug}";
            drops.Add(new ItemDrop
            {
                ItemRef = itemRef,
                ItemName = ExtractItemName(itemRef),
                Quantity = quantity,
                IsBonus = false
            });
        }

        _logger.LogDebug("Rolled {Count} drops for {Reference} (critical: {IsCritical})",
            drops.Count, lootTableRef, isCriticalHarvest);

        return drops;
    }

    /// <summary>
    /// Rolls loot drops for an enemy.
    /// </summary>
    /// <param name="lootTableRef">The loot table reference (e.g., "@loot-tables/enemies/humanoids:goblin").</param>
    /// <returns>Result containing drops and gold amount.</returns>
    public async Task<EnemyLootResult> RollEnemyDrops(string lootTableRef)
    {
        if (string.IsNullOrWhiteSpace(lootTableRef))
        {
            _logger.LogWarning("Cannot roll enemy loot for null/empty reference");
            return new EnemyLootResult();
        }

        var slug = ExtractSlugFromRef(lootTableRef);
        if (slug == null)
        {
            _logger.LogWarning("Invalid enemy loot table reference: {Reference}", lootTableRef);
            return new EnemyLootResult();
        }

        var lootTable = await _lootTableRepo.GetBySlugAsync(slug);
        if (lootTable == null)
        {
            _logger.LogWarning("Enemy loot table not found for slug: {Slug}", slug);
            return new EnemyLootResult();
        }

        var result = new EnemyLootResult { Drops = [] };

        foreach (var entry in lootTable.Entries)
        {
            if (!entry.IsGuaranteed)
            {
                var roll = _random.Next(1, 101);
                if (roll > entry.DropWeight)
                    continue;
            }

            var quantity = _random.Next(entry.QuantityMin, entry.QuantityMax + 1);
            var itemRef = $"@{entry.ItemDomain}:{entry.ItemSlug}";
            result.Drops.Add(new ItemDrop
            {
                ItemRef = itemRef,
                ItemName = ExtractItemName(itemRef),
                Quantity = quantity,
                IsBonus = false
            });
        }

        return result;
    }

    /// <summary>
    /// Rolls loot for a chest with support for multi-lookup merge strategies.
    /// </summary>
    /// <param name="lootLookups">List of loot table references to merge.</param>
    /// <param name="mergeStrategy">How to combine multiple lookups (addPools, prioritizeFirst, prioritizeLast).</param>
    /// <returns>Result containing drops and gold amount.</returns>
    public async Task<ChestLootResult> RollChestDrops(List<string> lootLookups, string mergeStrategy = "addPools")
    {
        if (lootLookups == null || lootLookups.Count == 0)
        {
            _logger.LogWarning("Cannot roll chest loot with empty lookups");
            return new ChestLootResult();
        }

        var result = new ChestLootResult
        {
            Gold = 0,
            Drops = new List<ItemDrop>()
        };

        switch (mergeStrategy.ToLowerInvariant())
        {
            case "addpools":
                // Add all pools together (more loot)
                foreach (var lookup in lootLookups)
                {
                    var lootResult = await RollSingleChestLookup(lookup);
                    result.Gold += lootResult.Gold;
                    result.Drops.AddRange(lootResult.Drops);
                }
                break;

            case "prioritizefirst":
                // Use first lookup, ignore others
                if (lootLookups.Count > 0)
                {
                    result = await RollSingleChestLookup(lootLookups[0]);
                }
                break;

            case "prioritizelast":
                // Use last lookup, ignore others
                if (lootLookups.Count > 0)
                {
                    result = await RollSingleChestLookup(lootLookups[^1]);
                }
                break;

            default:
                _logger.LogWarning("Unknown merge strategy: {Strategy}, using addPools", mergeStrategy);
                goto case "addpools";
        }

        _logger.LogDebug("Rolled chest loot with strategy {Strategy}: {Gold} gold, {Count} drops",
            mergeStrategy, result.Gold, result.Drops.Count);

        return result;
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

    private static string? ExtractSlugFromRef(string reference)
    {
        // Format: @loot-tables/harvesting/woods:oak  → "oak"
        var colonIdx = reference.LastIndexOf(':');
        return colonIdx >= 0 ? reference[(colonIdx + 1)..] : null;
    }

    /// <summary>
    /// Rolls loot for a single chest lookup reference by loading its loot table from the repository.
    /// </summary>
    private async Task<ChestLootResult> RollSingleChestLookup(string lookup)
    {
        if (string.IsNullOrWhiteSpace(lookup))
        {
            _logger.LogWarning("Chest loot lookup reference is null or empty");
            return new ChestLootResult();
        }

        var slug = ExtractSlugFromRef(lookup);
        if (slug == null)
        {
            _logger.LogWarning("Invalid chest loot table reference format: {Lookup}", lookup);
            return new ChestLootResult();
        }

        var lootTable = await _lootTableRepo.GetBySlugAsync(slug);
        if (lootTable == null)
        {
            _logger.LogWarning("Chest loot table not found for slug: {Slug}", slug);
            return new ChestLootResult();
        }

        var result = new ChestLootResult { Drops = [] };

        foreach (var entry in lootTable.Entries)
        {
            if (!entry.IsGuaranteed)
            {
                var roll = _random.Next(1, 101);
                if (roll > entry.DropWeight)
                    continue;
            }

            // Gold entries use the "gold" item domain convention
            if (string.Equals(entry.ItemDomain, "gold", StringComparison.OrdinalIgnoreCase))
            {
                result.Gold += _random.Next(entry.QuantityMin, entry.QuantityMax + 1);
                continue;
            }

            var quantity = _random.Next(entry.QuantityMin, entry.QuantityMax + 1);
            var itemRef = $"@{entry.ItemDomain}:{entry.ItemSlug}";
            result.Drops.Add(new ItemDrop
            {
                ItemRef = itemRef,
                ItemName = ExtractItemName(itemRef),
                Quantity = quantity,
                IsBonus = false
            });
        }

        return result;
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
