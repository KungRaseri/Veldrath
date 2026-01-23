using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace RealmEngine.Data.Services;

/// <summary>
/// Simplified service for resolving JSON v4.1 references in RealmEngine.Core generators
/// Reference format: @domain/path/category:item-name[filters]?.property.nested
/// </summary>
public class ReferenceResolverService
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<ReferenceResolverService> _logger;
    private static readonly Regex ReferencePattern = new(@"^@(?<domain>[\w-]+)/(?<path>[\w-]+(/[\w-]+)*):(?<item>[\w-*\s]+)(?<filters>\[[^\]]+\])?(?<optional>\?)?(?<property>\.[\w.]+)?$", RegexOptions.Compiled);
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the ReferenceResolverService.
    /// </summary>
    /// <param name="dataCache">Game data cache for accessing JSON files</param>
    /// <param name="logger">Logger for diagnostics</param>
    public ReferenceResolverService(GameDataCache dataCache, ILogger<ReferenceResolverService> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves a reference asynchronously - for compatibility with generators
    /// </summary>
    public async Task<object?> ResolveAsync(string reference)
    {
        return await Task.FromResult(Resolve(reference));
    }

    /// <summary>
    /// Resolves a reference and returns the full JSON object instead of just the ID
    /// </summary>
    public async Task<JToken?> ResolveToObjectAsync(string reference)
    {
        return await Task.FromResult(ResolveToObject(reference));
    }

    /// <summary>
    /// Resolves a reference synchronously and returns the full JSON object
    /// </summary>
    public JToken? ResolveToObject(string reference)
    {
        var components = ParseReference(reference);
        if (components == null)
        {
            _logger.LogWarning("Invalid reference syntax: {Reference}", reference);
            return null;
        }

        // Special case: @items/materials:* should aggregate from ALL material subdirectories
        // Handle this BEFORE try block to avoid any interference
        if (components.ItemName == "*" && 
            components.Domain == "items" && 
            components.Category == "materials")
        {
            _logger.LogDebug("Aggregating items from all material subdirectories for wildcard reference");
            var aggregatedItems = GetAllItemsFromMaterialSubdirectories();
            
            // Apply filters if specified
            if (!string.IsNullOrEmpty(components.Filters))
            {
                aggregatedItems = ApplyFilters(aggregatedItems, components.Filters);
            }
            
            return aggregatedItems;
        }

        try
        {
            // Build catalog paths to try
            // We need to handle two cases:
            // 1. Category is a subdirectory: @abilities/active/offensive:item → abilities/active/offensive/catalog.json
            // 2. Category is in the catalog: @items/weapons/swords:item → items/weapons/catalog.json (swords is category IN the file)
            
            CachedJsonFile? catalogFile = null;
            string catalogPath = "";
            
            // Case 1: Try with category as part of path first (for nested structures like abilities)
            if (!string.IsNullOrEmpty(components.Category))
            {
                catalogPath = string.IsNullOrEmpty(components.Path)
                    ? $"{components.Domain}/{components.Category}/catalog.json"
                    : $"{components.Domain}/{components.Path}/{components.Category}/catalog.json";
                
                catalogFile = _dataCache.GetFile(catalogPath);
                
                // Case 2: If not found, try without category in path (category is in the catalog file)
                if (catalogFile?.JsonData == null)
                {
                    catalogPath = string.IsNullOrEmpty(components.Path)
                        ? $"{components.Domain}/catalog.json"
                        : $"{components.Domain}/{components.Path}/catalog.json";
                    
                    catalogFile = _dataCache.GetFile(catalogPath);
                }
            }
            else
            {
                // No category, use path directly
                catalogPath = string.IsNullOrEmpty(components.Path)
                    ? $"{components.Domain}/catalog.json"
                    : $"{components.Domain}/{components.Path}/catalog.json";
                
                catalogFile = _dataCache.GetFile(catalogPath);
            }

            if (catalogFile?.JsonData == null)
            {
                if (components.IsOptional)
                    return null;
                
                _logger.LogWarning("Catalog not found for reference: {Reference} (expected path: {CatalogPath})", reference, catalogPath);
                return null;
            }

            var catalog = catalogFile.JsonData;

            // Handle wildcard - return random item from category
            if (components.ItemName == "*")
            {
                var items = GetAllItemsInCategory(catalog, components.Category);
                
                // Apply filters if specified
                if (!string.IsNullOrEmpty(components.Filters))
                {
                    items = ApplyFilters(items, components.Filters);
                }
                
                // Return random item weighted by rarityWeight
                if (items.Count > 0)
                {
                    return SelectRandomWeighted(items);
                }
                return null;
            }
            
            // Find specific item and return the full object
            var item = FindItemInCatalog(catalog, components.Category, components.ItemName);
            
            if (item == null)
            {
                if (components.IsOptional)
                    return null;
                
                _logger.LogWarning("Item not found: {ItemName} in {Category} (reference: {Reference})", components.ItemName, components.Category, reference);
                return null;
            }

            // Apply property access if specified
            if (!string.IsNullOrEmpty(components.Property))
            {
                return GetNestedProperty(item, components.Property);
            }

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving reference: {Reference}", reference);
            if (components.IsOptional)
                return null;
            throw;
        }
    }

    /// <summary>
    /// Selects a random item from array using rarityWeight
    /// </summary>
    private JToken? SelectRandomWeighted(JArray items)
    {
        if (items.Count == 0) return null;
        if (items.Count == 1) return items[0];

        var totalWeight = 0;
        foreach (var item in items)
        {
            var weight = item["rarityWeight"]?.Value<int>() ?? 1;
            totalWeight += weight;
        }

        var randomValue = _random.Next(totalWeight);
        var currentWeight = 0;

        foreach (var item in items)
        {
            var weight = item["rarityWeight"]?.Value<int>() ?? 1;
            currentWeight += weight;
            if (randomValue < currentWeight)
            {
                return item;
            }
        }

        return items[^1];
    }

    /// <summary>
    /// Resolves a reference synchronously
    /// </summary>
    public object? Resolve(string reference)
    {
        var components = ParseReference(reference);
        if (components == null)
        {
            _logger.LogWarning("Invalid reference syntax: {Reference}", reference);
            return null;
        }

        try
        {
            // Build catalog paths to try
            // We need to handle two cases:
            // 1. Category is a subdirectory: @abilities/active/offensive:item → abilities/active/offensive/catalog.json
            // 2. Category is in the catalog: @items/weapons/swords:item → items/weapons/catalog.json (swords is category IN the file)
            
            CachedJsonFile? catalogFile = null;
            string catalogPath = "";
            
            // Case 1: Try with category as part of path first (for nested structures like abilities)
            if (!string.IsNullOrEmpty(components.Category))
            {
                catalogPath = string.IsNullOrEmpty(components.Path)
                    ? $"{components.Domain}/{components.Category}/catalog.json"
                    : $"{components.Domain}/{components.Path}/{components.Category}/catalog.json";
                
                catalogFile = _dataCache.GetFile(catalogPath);
                
                // Case 2: If not found, try without category in path (category is in the catalog file)
                if (catalogFile?.JsonData == null)
                {
                    catalogPath = string.IsNullOrEmpty(components.Path)
                        ? $"{components.Domain}/catalog.json"
                        : $"{components.Domain}/{components.Path}/catalog.json";
                    
                    catalogFile = _dataCache.GetFile(catalogPath);
                }
            }
            else
            {
                // No category, use path directly
                catalogPath = string.IsNullOrEmpty(components.Path)
                    ? $"{components.Domain}/catalog.json"
                    : $"{components.Domain}/{components.Path}/catalog.json";
                
                catalogFile = _dataCache.GetFile(catalogPath);
            }

            if (catalogFile?.JsonData == null)
            {
                if (components.IsOptional)
                    return null;
                
                _logger.LogWarning("Catalog not found for reference: {Reference} (expected path: {CatalogPath})", reference, catalogPath);
                return null;
            }

            var catalog = catalogFile.JsonData;

            // Handle wildcard - return array of items
            if (components.ItemName == "*")
            {
                var items = GetAllItemsInCategory(catalog, components.Category);
                
                // Apply filters if specified
                if (!string.IsNullOrEmpty(components.Filters))
                {
                    items = ApplyFilters(items, components.Filters);
                }
                
                return items;
            }
            
            // Find specific item
            var item = FindItemInCatalog(catalog, components.Category, components.ItemName);
            
            if (item == null)
            {
                if (components.IsOptional)
                    return null;
                
                _logger.LogWarning("Item not found: {ItemName} in {Category} (reference: {Reference})", components.ItemName, components.Category, reference);
                return null;
            }

            // For generators, return the resolved ID format
            var resolvedId = string.IsNullOrEmpty(components.Path)
                ? $"{components.Category}:{components.ItemName}"
                : $"{components.Path}/{components.Category}:{components.ItemName}";

            // Apply property access if specified
            if (!string.IsNullOrEmpty(components.Property))
            {
                var propertyValue = GetNestedProperty(item, components.Property);
                return propertyValue?.ToString() ?? resolvedId;
            }

            return resolvedId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving reference: {Reference}", reference);
            if (components.IsOptional)
                return null;
            throw;
        }
    }

    /// <summary>
    /// Parses a reference string into components
    /// </summary>
    private ReferenceComponents? ParseReference(string reference)
    {
        var match = ReferencePattern.Match(reference);
        if (!match.Success)
            return null;

        var fullPath = match.Groups["path"].Value;
        
        string path;
        string category;
        
        var pathParts = fullPath.Split('/');
        if (pathParts.Length == 1)
        {
            path = "";
            category = pathParts[0];
        }
        else
        {
            category = pathParts[^1];
            path = string.Join("/", pathParts[..^1]);
        }

        return new ReferenceComponents
        {
            Domain = match.Groups["domain"].Value,
            Path = path,
            Category = category,
            ItemName = match.Groups["item"].Value,
            Filters = match.Groups["filters"].Success ? match.Groups["filters"].Value.Trim('[', ']') : null,
            IsOptional = match.Groups["optional"].Success,
            Property = match.Groups["property"].Success ? match.Groups["property"].Value.TrimStart('.') : null
        };
    }

    /// <summary>
    /// Finds an item in a catalog
    /// </summary>
    private JToken? FindItemInCatalog(JToken catalog, string? category, string itemName)
    {
        // If no category specified, search root items array directly
        if (string.IsNullOrEmpty(category))
        {
            var rootItems = catalog["items"] as JArray;
            if (rootItems != null)
            {
                foreach (var item in rootItems)
                {
                    // Prefer slug (exact match), fallback to case-insensitive name
                    if (item["slug"]?.ToString() == itemName || 
                        string.Equals(item["name"]?.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
                        return item;
                }
            }
            return null;
        }

        // Try *_types structure first: ability_types.offensive.items, class_types.warrior.items, etc.
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name != "metadata" && property.Name.EndsWith("_types") && property.Value is JObject typesObj)
            {
                // First try exact category match
                var categoryData = typesObj[category];
                if (categoryData != null)
                {
                    var items = categoryData["items"] as JArray;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            // Prefer slug (exact match), fallback to case-insensitive name
                            if (item["slug"]?.ToString() == itemName || 
                                string.Equals(item["name"]?.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
                                return item;
                        }
                    }
                }
                
                // If not found in exact category, search ALL subcategories
                // This handles cases like @abilities/passive:weapon-mastery where "passive" is the file path,
                // but the ability is in ability_types["class_mastery"]["items"]
                foreach (var subcategory in typesObj.Children<JProperty>())
                {
                    if (subcategory.Value is JObject subcatObj)
                    {
                        var items = subcatObj["items"] as JArray;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                if (item["slug"]?.ToString() == itemName || 
                                    string.Equals(item["name"]?.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
                                    return item;
                            }
                        }
                    }
                }
            }
        }

        // Fallback: Try flat items[] array structure (used by materials subdirectories)
        // This handles catalogs without *_types wrapper, like items/materials/ingots/catalog.json
        var flatItems = catalog["items"] as JArray;
        if (flatItems != null)
        {
            foreach (var item in flatItems)
            {
                // Prefer slug (exact match), fallback to case-insensitive name
                if (item["slug"]?.ToString() == itemName || 
                    string.Equals(item["name"]?.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets nested property from JSON token
    /// </summary>
    private JToken? GetNestedProperty(JToken item, string propertyPath)
    {
        var properties = propertyPath.Split('.');
        var current = item;

        foreach (var prop in properties)
        {
            current = current[prop];
            if (current == null)
                return null;
        }

        return current;
    }

    /// <summary>
    /// Gets all items in a category
    /// </summary>
    private JArray GetAllItemsInCategory(JToken catalog, string? category)
    {
        var result = new JArray();
        
        if (string.IsNullOrEmpty(category))
        {
            // First try root items array
            var rootItems = catalog["items"] as JArray;
            if (rootItems != null)
            {
                foreach (var item in rootItems)
                    result.Add(item);
            }
            
            // Also check *_types structures for ALL items (when no category specified)
            foreach (var property in catalog.Children<JProperty>())
            {
                if (property.Name != "metadata" && property.Name.EndsWith("_types") && property.Value is JObject typesObj)
                {
                    // Get items from ALL subcategories
                    foreach (var subcategory in typesObj.Children<JProperty>())
                    {
                        var items = subcategory.Value["items"] as JArray;
                        if (items != null)
                        {
                            foreach (var item in items)
                                result.Add(item);
                        }
                    }
                }
            }
            
            return result;
        }

        // Try *_types structure (ability_types.offensive.items, etc.)
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name != "metadata" && property.Name.EndsWith("_types") && property.Value is JObject typesObj)
            {
                var categoryData = typesObj[category];
                if (categoryData != null)
                {
                    var items = categoryData["items"] as JArray;
                    if (items != null)
                    {
                        foreach (var item in items)
                            result.Add(item);
                    }
                }
            }
        }

        // LOOT TABLES: Try "types" structure (types.woods.nodes, types.enemies, etc.)
        var typesProperty = catalog["types"] as JObject;
        if (typesProperty != null && category != null)
        {
            var categoryData = typesProperty[category];
            if (categoryData != null)
            {
                // Loot tables use "nodes" for harvesting or direct items for enemies
                var nodes = categoryData["nodes"] as JObject;
                if (nodes != null)
                {
                    // Harvesting loot tables: woods.nodes.oak, ores.nodes.iron
                    foreach (var node in nodes.Children<JProperty>())
                    {
                        result.Add(node.Value);
                    }
                }
                else
                {
                    // Enemy loot tables: humanoids.enemies.goblin, beasts.enemies.wolf
                    var enemies = categoryData["enemies"] as JObject;
                    if (enemies != null)
                    {
                        foreach (var enemy in enemies.Children<JProperty>())
                        {
                            result.Add(enemy.Value);
                        }
                    }
                }
            }
        }

        // If no items found for the specific category, return ALL items from ALL subcategories
        // This handles cases like @items/consumables:* where "consumables" is the path, not a category
        if (result.Count == 0)
        {
            // First try root items array
            var rootItems = catalog["items"] as JArray;
            if (rootItems != null)
            {
                foreach (var item in rootItems)
                    result.Add(item);
            }
            
            // Get items from ALL subcategories in *_types structures
            foreach (var property in catalog.Children<JProperty>())
            {
                if (property.Name != "metadata" && property.Name.EndsWith("_types") && property.Value is JObject typesObj)
                {
                    foreach (var subcategory in typesObj.Children<JProperty>())
                    {
                        var items = subcategory.Value["items"] as JArray;
                        if (items != null)
                        {
                            foreach (var item in items)
                                result.Add(item);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all items from all material subdirectories
    /// Handles @items/materials:* by aggregating from essences, gems, ingots, leather, ores, organics, reagents, stone, wood
    /// </summary>
    private JArray GetAllItemsFromMaterialSubdirectories()
    {
        var result = new JArray();
        
        // List of material subdirectories
        var subdirectories = new[] 
        { 
            "essences", "gems", "ingots", "leather", "ores", 
            "organics", "reagents", "stone", "wood" 
        };
        
        _logger.LogInformation("🔍 Aggregating materials from {Count} subdirectories", subdirectories.Length);
        
        foreach (var subdir in subdirectories)
        {
            var catalogPath = $"items/materials/{subdir}/catalog.json";
            var catalogFile = _dataCache.GetFile(catalogPath);
            
            if (catalogFile?.JsonData != null)
            {
                var items = catalogFile.JsonData["items"] as JArray;
                if (items != null)
                {
                    _logger.LogDebug("✓ Found {ItemCount} items in {Subdir}", items.Count, subdir);
                    foreach (var item in items)
                    {
                        result.Add(item);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠ No items array in {CatalogPath}", catalogPath);
                }
            }
            else
            {
                _logger.LogWarning("⚠ Material subdirectory catalog not found: {CatalogPath}", catalogPath);
            }
        }
        
        _logger.LogInformation("✓ Aggregated {TotalCount} total materials", result.Count);
        return result;
    }

    /// <summary>
    /// Applies filters to items array
    /// Format: property=value or property.nested=value or property EXISTS
    /// Operators: =, !=, &lt;, &lt;=, &gt;, &gt;=
    /// Special: property=true checks if property exists and is truthy (object/array/true)
    /// </summary>
    private JArray ApplyFilters(JArray items, string filters)
    {
        var result = new JArray();
        
        // Split multiple filters by & or comma
        var filterParts = filters.Split(['&', ','], StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var item in items)
        {
            bool matchesAll = true;
            
            foreach (var filter in filterParts)
            {
                var trimmedFilter = filter.Trim();
                
                // Detect operator (>=, <=, !=, =, <, >)
                string op = "=";
                int opIndex = -1;
                
                if (trimmedFilter.Contains(">="))
                {
                    op = ">=";
                    opIndex = trimmedFilter.IndexOf(">=");
                }
                else if (trimmedFilter.Contains("<="))
                {
                    op = "<=";
                    opIndex = trimmedFilter.IndexOf("<=");
                }
                else if (trimmedFilter.Contains("!="))
                {
                    op = "!=";
                    opIndex = trimmedFilter.IndexOf("!=");
                }
                else if (trimmedFilter.Contains("="))
                {
                    op = "=";
                    opIndex = trimmedFilter.IndexOf("=");
                }
                else if (trimmedFilter.Contains(">"))
                {
                    op = ">";
                    opIndex = trimmedFilter.IndexOf(">");
                }
                else if (trimmedFilter.Contains("<"))
                {
                    op = "<";
                    opIndex = trimmedFilter.IndexOf("<");
                }
                
                // No operator means existence check
                if (opIndex < 0)
                {
                    var actualValue = GetNestedProperty(item, trimmedFilter);
                    if (actualValue == null || actualValue.Type == JTokenType.Null)
                    {
                        matchesAll = false;
                        break;
                    }
                    continue;
                }
                
                var propertyPath = trimmedFilter.Substring(0, opIndex).Trim();
                var expectedValue = trimmedFilter.Substring(opIndex + op.Length).Trim();
                
                var actualValue2 = GetNestedProperty(item, propertyPath);
                
                // Handle "property=true" as an existence check
                if (op == "=" && expectedValue.ToLower() == "true")
                {
                    if (actualValue2 == null || actualValue2.Type == JTokenType.Null)
                    {
                        matchesAll = false;
                        break;
                    }
                    if (actualValue2.Type == JTokenType.Boolean)
                    {
                        if (!actualValue2.Value<bool>())
                        {
                            matchesAll = false;
                            break;
                        }
                    }
                    continue;
                }
                
                // Property must exist
                if (actualValue2 == null)
                {
                    matchesAll = false;
                    break;
                }
                
                // Handle numeric comparisons
                if (actualValue2.Type == JTokenType.Integer || actualValue2.Type == JTokenType.Float)
                {
                    if (!double.TryParse(expectedValue, out var expectedNum))
                    {
                        matchesAll = false;
                        break;
                    }
                    
                    var actualNum = actualValue2.Value<double>();
                    
                    bool matches = op switch
                    {
                        "=" => actualNum == expectedNum,
                        "!=" => actualNum != expectedNum,
                        ">" => actualNum > expectedNum,
                        ">=" => actualNum >= expectedNum,
                        "<" => actualNum < expectedNum,
                        "<=" => actualNum <= expectedNum,
                        _ => false
                    };
                    
                    if (!matches)
                    {
                        matchesAll = false;
                        break;
                    }
                    continue;
                }
                
                // String/boolean comparisons
                var actualString = actualValue2.ToString();
                
                if (op == "=" && expectedValue.ToLower() == "false")
                {
                    if (actualValue2.Type == JTokenType.Boolean)
                    {
                        if (actualValue2.Value<bool>())
                        {
                            matchesAll = false;
                            break;
                        }
                    }
                    else
                    {
                        matchesAll = false;
                        break;
                    }
                }
                else if (op == "=" && actualString != expectedValue)
                {
                    matchesAll = false;
                    break;
                }
                else if (op == "!=" && actualString == expectedValue)
                {
                    matchesAll = false;
                    break;
                }
            }
            
            if (matchesAll)
                result.Add(item);
        }
        
        return result;
    }
}

/// <summary>
/// Parsed components of a reference
/// </summary>
internal class ReferenceComponents
{
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Filters { get; set; }
    public bool IsOptional { get; set; }
    public string? Property { get; set; }
}