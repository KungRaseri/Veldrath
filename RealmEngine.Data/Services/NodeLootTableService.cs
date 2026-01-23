using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Services;

/// <summary>
/// Service for loading and processing loot tables for harvestable resource nodes.
/// </summary>
public class NodeLootTableService
{
    private readonly ILogger<NodeLootTableService> _logger;
    private readonly string _dataPath;
    private readonly Random _random;
    private Dictionary<string, JObject>? _cachedTables;
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeLootTableService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="dataPath">The base path to the Data/Json directory (defaults to relative path).</param>
    /// <param name="seed">Optional seed for random number generation (for testing).</param>
    public NodeLootTableService(
        ILogger<NodeLootTableService> logger,
        string dataPath = "Data/Json",
        int? seed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _isLoaded = false;
    }

    /// <summary>
    /// Loads all loot tables from the loot-tables/nodes directory.
    /// </summary>
    public void LoadLootTables()
    {
        if (_isLoaded)
        {
            _logger.LogDebug("Loot tables already loaded, returning cached data");
            return;
        }

        var lootTablesPath = Path.Combine(_dataPath, "loot-tables", "nodes");
        
        if (!Directory.Exists(lootTablesPath))
        {
            _logger.LogError("Loot tables directory not found: {Path}", lootTablesPath);
            throw new DirectoryNotFoundException($"Loot tables directory not found: {lootTablesPath}");
        }

        try
        {
            _cachedTables = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            
            // Scan all subdirectories (ores, woods, etc.)
            var jsonFiles = Directory.GetFiles(lootTablesPath, "*.json", SearchOption.AllDirectories);
            
            _logger.LogInformation("Loading {Count} loot table files from {Path}", jsonFiles.Length, lootTablesPath);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var lootTable = JObject.Parse(json);
                    
                    // Use relative path as key (e.g., "ores/copper", "woods/oak")
                    var relativePath = Path.GetRelativePath(lootTablesPath, filePath);
                    var key = Path.ChangeExtension(relativePath, null).Replace("\\", "/");
                    
                    _cachedTables[key] = lootTable;
                    _logger.LogDebug("Loaded loot table: {Key}", key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load loot table from {FilePath}", filePath);
                }
            }

            _isLoaded = true;
            _logger.LogInformation("Successfully loaded {Count} loot tables", _cachedTables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load loot tables from {Path}", lootTablesPath);
            throw;
        }
    }

    /// <summary>
    /// Rolls loot drops for a harvestable node based on its loot table reference.
    /// </summary>
    /// <param name="lootTableRef">The loot table reference (e.g., "@loot-tables/nodes/ores:copper").</param>
    /// <param name="baseYield">The base yield quantity calculated for the harvest.</param>
    /// <param name="isCriticalHarvest">Whether this is a critical harvest (affects bonus drop chances).</param>
    /// <returns>List of item drops with material references and quantities.</returns>
    public List<ItemDrop> RollLootDrops(string lootTableRef, int baseYield, bool isCriticalHarvest)
    {
        if (string.IsNullOrWhiteSpace(lootTableRef))
        {
            _logger.LogWarning("Cannot roll loot for null/empty loot table reference");
            return new List<ItemDrop>();
        }

        if (!_isLoaded)
        {
            LoadLootTables();
        }

        // Parse the reference (format: @loot-tables/nodes/ores:copper)
        var tablePath = ParseLootTableReference(lootTableRef);
        if (tablePath == null)
        {
            _logger.LogWarning("Invalid loot table reference format: {Reference}", lootTableRef);
            return new List<ItemDrop>();
        }

        if (!_cachedTables!.TryGetValue(tablePath, out var lootTable))
        {
            _logger.LogWarning("Loot table not found: {TablePath}", tablePath);
            return new List<ItemDrop>();
        }

        var drops = new List<ItemDrop>();
        var dropsArray = lootTable["drops"] as JArray;

        if (dropsArray == null)
        {
            _logger.LogWarning("Loot table {TablePath} has no drops array", tablePath);
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

            // Roll for drop chance
            var roll = _random.Next(100);
            if (roll >= dropChance)
            {
                continue;
            }

            var minQuantity = dropData["minQuantity"]?.Value<int>() ?? 1;
            var maxQuantity = dropData["maxQuantity"]?.Value<int>() ?? 1;
            
            // For primary drops (not bonus), scale quantity by yield
            var quantity = isBonusDrop 
                ? _random.Next(minQuantity, maxQuantity + 1)
                : baseYield * _random.Next(minQuantity, maxQuantity + 1);

            drops.Add(new ItemDrop
            {
                ItemRef = materialRef ?? string.Empty,
                ItemName = ExtractItemName(materialRef),
                Quantity = quantity,
                IsBonus = isBonusDrop
            });

            _logger.LogDebug("Rolled loot drop: {Material} x{Quantity} (bonus: {IsBonus})", 
                materialRef, quantity, isBonusDrop);
        }

        return drops;
    }

    /// <summary>
    /// Parses a loot table reference into a file path.
    /// Example: "@loot-tables/nodes/ores:copper" -> "ores/copper"
    /// </summary>
    /// <param name="reference">The loot table reference.</param>
    /// <returns>The file path, or null if invalid.</returns>
    private string? ParseLootTableReference(string reference)
    {
        if (!reference.StartsWith("@loot-tables/nodes/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Remove "@loot-tables/nodes/" prefix
        var pathPart = reference.Substring(19);
        
        // Replace colon with slash
        pathPart = pathPart.Replace(':', '/');
        
        return pathPart;
    }

    /// <summary>
    /// Extracts a display name from a material reference.
    /// Example: "@items/materials/ore:copper-ore" -> "Copper Ore"
    /// </summary>
    /// <param name="materialRef">The material reference.</param>
    /// <returns>A formatted display name.</returns>
    private string ExtractItemName(string? materialRef)
    {
        if (string.IsNullOrWhiteSpace(materialRef))
        {
            return "Unknown Material";
        }

        // Get the part after the last colon
        var colonIndex = materialRef.LastIndexOf(':');
        if (colonIndex < 0)
        {
            return "Unknown Material";
        }

        var itemId = materialRef.Substring(colonIndex + 1);
        
        // Convert kebab-case to Title Case
        var words = itemId.Split('-');
        var titleCase = string.Join(" ", words.Select(w => 
            char.ToUpper(w[0]) + w.Substring(1).ToLower()));
        
        return titleCase;
    }

    /// <summary>
    /// Clears the cached loot tables (useful for testing).
    /// </summary>
    public void ClearCache()
    {
        _cachedTables = null;
        _isLoaded = false;
        _logger.LogInformation("Loot table cache cleared");
    }
}
