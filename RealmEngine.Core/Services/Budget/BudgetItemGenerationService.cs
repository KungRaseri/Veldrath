using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Budget-based item generation service implementing forward-building approach.
/// Generates items by allocating budget across materials and components.
/// </summary>
public class BudgetItemGenerationService
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly BudgetCalculator _budgetCalculator;
    private readonly MaterialPoolService _materialPoolService;
    private readonly BudgetConfigFactory _configFactory;
    private readonly ItemGenerationRulesService _generationRulesService;
    private readonly ILogger<BudgetItemGenerationService> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetItemGenerationService"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="budgetCalculator">The budget calculator.</param>
    /// <param name="materialPoolService">The material pool service.</param>
    /// <param name="configFactory">The budget config factory.</param>
    /// <param name="generationRulesService">The item generation rules service.</param>
    /// <param name="logger">The logger instance.</param>
    public BudgetItemGenerationService(
        GameDataCache dataCache,
        ReferenceResolverService referenceResolver,
        BudgetCalculator budgetCalculator,
        MaterialPoolService materialPoolService,
        BudgetConfigFactory configFactory,
        ItemGenerationRulesService generationRulesService,
        ILogger<BudgetItemGenerationService> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _budgetCalculator = budgetCalculator ?? throw new ArgumentNullException(nameof(budgetCalculator));
        _materialPoolService = materialPoolService ?? throw new ArgumentNullException(nameof(materialPoolService));
        _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
        _generationRulesService = generationRulesService ?? throw new ArgumentNullException(nameof(generationRulesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Generate an item with budget-based forward building.
    /// Uses a budget-fitting approach that always succeeds by selecting affordable components.
    /// </summary>
    public async Task<BudgetItemResult> GenerateItemAsync(BudgetItemRequest request)
    {
        try
        {
            // Step 1: Calculate base budget
            var baseBudget = _budgetCalculator.CalculateBaseBudget(
                request.EnemyLevel, 
                request.IsBoss, 
                request.IsElite);

            // Apply enemy type budget multiplier
            var typeMultiplier = _materialPoolService.GetBudgetMultiplier(request.EnemyType);
            baseBudget = (int)Math.Round(baseBudget * typeMultiplier);

            _logger.LogDebug("Base budget calculated: {Budget} (level={Level}, type={Type}, multiplier={Multiplier})", 
                baseBudget, request.EnemyLevel, request.EnemyType, typeMultiplier);

            // Step 2: Split budget into material and component budgets
            var materialPercentageOverride = _materialPoolService.GetMaterialPercentage(request.EnemyType);
            var materialBudget = _budgetCalculator.CalculateMaterialBudget(baseBudget, materialPercentageOverride);
            var componentBudget = _budgetCalculator.CalculateComponentBudget(baseBudget, materialBudget);

            _logger.LogDebug("Budget split: Material={MaterialBudget}, Components={ComponentBudget}", 
                materialBudget, componentBudget);

            // Step 3: Select cheapest base item FIRST (so we can check its allowedMaterials)
            var baseItem = await SelectCheapestBaseItemAsync(request.ItemCategory);
            var baseItemCost = _budgetCalculator.CalculateMaterialCost(baseItem);

            _logger.LogDebug("Base item selected: {ItemName} (cost={Cost})", 
                GetStringProperty(baseItem, "name"), baseItemCost);

            // Step 4: Select material from pool (only for items that use materials)
            // Check base item's allowedMaterials field first, then fall back to category default
            var materialTypes = GetMaterialTypesForItem(baseItem, request.ItemCategory);
            JToken? material = null;
            int materialCost = 0;

            if (materialTypes != null && materialTypes.Any())
            {
                // Try each allowed material type in order until one succeeds
                foreach (var materialType in materialTypes)
                {
                    material = await _materialPoolService.SelectMaterialAsync(materialType, materialBudget);
                    if (material != null)
                    {
                        _logger.LogDebug("Material type '{MaterialType}' succeeded", materialType);
                        break;
                    }
                    _logger.LogDebug("No affordable material found for type '{MaterialType}', trying next...", materialType);
                }
                
                if (material == null)
                {
                    // Final fallback: Get ANY material from first pool and use it regardless of cost
                    _logger.LogWarning("No affordable material found in any pool, using fallback for category '{Category}'", request.ItemCategory);
                    material = await GetFallbackMaterialAsync(materialTypes.First());
                }

                materialCost = _budgetCalculator.CalculateMaterialCost(material);

                _logger.LogDebug("Material selected: {MaterialName} (cost={Cost})", 
                    GetStringProperty(material, "name"), materialCost);
            }
            else
            {
                _logger.LogDebug("Skipping material selection for category '{Category}' (doesn't use materials)", 
                    request.ItemCategory);
            }

            // Step 5: Calculate remaining budget for enhancements
            var remainingBudget = componentBudget - baseItemCost;
            if (remainingBudget < 0) remainingBudget = 0;

            // Step 6: Fit quality to budget (optional, only if budget allows)
            JToken? qualityComponent = null;
            var qualityModifier = 0.0;
            var qualityCost = 0;

            if (request.AllowQuality && remainingBudget > 0)
            {
                var qualityResult = await SelectAffordableQualityAsync(remainingBudget);
                qualityComponent = qualityResult.Component;
                qualityModifier = qualityResult.Modifier;
                qualityCost = qualityResult.Cost;
                remainingBudget -= qualityCost;

            if (qualityComponent != null)
            {
                _logger.LogDebug("Quality selected: {Quality} (modifier={Modifier}, cost={Cost}, remaining={Remaining})", 
                    GetStringProperty(qualityComponent, "value"), qualityModifier, qualityCost, remainingBudget);
            }
            }

            // Step 7: Select pattern (ALWAYS succeeds - has default fallback)
            var pattern = await SelectPatternAsync(request.ItemCategory);
            var patternString = GetStringProperty(pattern, "pattern");
            var patternCost = _budgetCalculator.GetPatternCost(patternString);

            // Step 8: Fill remaining budget with affordable components (enforcing generation rules limits)
            var rarityTier = CalculateRarityTier(baseBudget);
            var components = await SelectAffordableComponentsAsync(
                request.ItemCategory, 
                patternString, 
                remainingBudget,
                rarityTier);

            // Step 9: Generate sockets based on total budget
            var totalSpent = materialCost + baseItemCost + qualityCost + patternCost + components.Sum(c => c.Cost);
            // Note: Sockets are empty for now - socket generation is handled elsewhere
            var sockets = new Dictionary<SocketType, List<Socket>>();

            // Step 10: Build result
            var result = new BudgetItemResult
            {
                BaseBudget = baseBudget,
                AdjustedBudget = baseBudget, // No longer using quality modifier on budget
                MaterialBudget = materialBudget,
                ComponentBudget = componentBudget,
                SpentBudget = totalSpent,
                Material = material,
                MaterialCost = materialCost,
                Quality = qualityComponent,
                QualityModifier = qualityModifier,
                BaseItem = baseItem,
                BaseItemCost = baseItemCost,
                Pattern = patternString,
                PatternCost = patternCost,
                Components = components.Select(c => c.Component).ToList(),
                ComponentCosts = components
                    .GroupBy(c => GetStringProperty(c.Component, "value"))
                    .ToDictionary(g => g.Key, g => g.First().Cost),
                Sockets = sockets
            };

            _logger.LogInformation("Item generation completed: Spent {Spent}/{Budget} budget ({Utilization:F1}%)", 
                totalSpent, baseBudget, (totalSpent / (double)baseBudget) * 100);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GENERATION FAILED with EXCEPTION for {EnemyType} level {Level} {Category}: {Message}", 
                request.EnemyType, request.EnemyLevel, request.ItemCategory, ex.Message);
            
            // Even on exception, return a minimal valid item
            return CreateMinimalItem(request);
        }
    }

    /// <summary>
    /// Get a fallback material when budget is too low for any material in the pool.
    /// Returns the cheapest material available.
    /// </summary>
    private async Task<JToken> GetFallbackMaterialAsync(string materialType)
    {
        // Try to get any material from the pool
        var material = await _materialPoolService.SelectMaterialAsync(materialType, int.MaxValue);
        if (material != null)
            return material;

        // Ultimate fallback: Create a basic material token
        _logger.LogWarning("No materials found in pool for {MaterialType}, using generic material", materialType);
        return JToken.FromObject(new
        {
            name = "Iron",
            rarityWeight = 100,
            description = "Basic material"
        });
    }
    
    /// <summary>
    /// Determines all possible material types for an item by checking its allowedMaterials field.
    /// Falls back to material-filters.json configuration using property matching and category defaults.
    /// Returns empty list for item types that don't use materials (consumables, scrolls, etc.)
    /// </summary>
    private List<string> GetMaterialTypesForItem(JToken baseItem, string category)
    {
        var materialTypes = new List<string>();
        
        // Step 1: Check if item has explicit allowedMaterials field
        var allowedMaterials = baseItem["allowedMaterials"];
        if (allowedMaterials != null && allowedMaterials.Type == JTokenType.Array)
        {
            foreach (var materialRefToken in allowedMaterials)
            {
                var materialRef = materialRefToken.Value<string>();
                // Extract material type from reference like "@properties/materials/fabrics:*"
                if (!string.IsNullOrEmpty(materialRef) && materialRef.Contains("/materials/"))
                {
                    var parts = materialRef.Split('/');
                    if (parts.Length >= 3)
                    {
                        var typePart = parts[2]; // "fabrics:*" or just "fabrics"
                        var materialType = typePart.Split(':')[0]; // Remove :* wildcard
                        if (!string.IsNullOrEmpty(materialType))
                        {
                            materialTypes.Add(materialType);
                        }
                    }
                }
            }
            
            if (materialTypes.Any())
            {
                _logger.LogDebug("Using explicit allowedMaterials from item: {Materials}", string.Join(", ", materialTypes));
                return materialTypes; // Use explicit allowedMaterials
            }
        }

        // Step 2: Fall back to material-filters.json configuration
        var config = _configFactory.GetMaterialFilters();
        var categoryKey = category?.ToLower() ?? "unknown";

        if (config.Categories.TryGetValue(categoryKey, out var categoryFilter))
        {
            // Step 2a: Check property-based matches first (e.g., armorClass, blockChance)
            if (categoryFilter.PropertyMatches != null)
            {
                foreach (var match in categoryFilter.PropertyMatches)
                {
                    var propertyToken = baseItem[match.Property];
                    if (propertyToken != null)
                    {
                        // Check condition
                        if (match.Condition == "exists")
                        {
                            _logger.LogDebug("Property match: {Property} exists, using materials: {Materials}",
                                match.Property, string.Join(", ", match.AllowedMaterials));
                            return match.AllowedMaterials.ToList();
                        }
                        
                        // Check value match
                        if (match.Value != null)
                        {
                            var propertyValue = propertyToken.Value<string>()?.ToLower();
                            if (propertyValue == match.Value.ToLower())
                            {
                                _logger.LogDebug("Property match: {Property}={Value}, using materials: {Materials}",
                                    match.Property, match.Value, string.Join(", ", match.AllowedMaterials));
                                return match.AllowedMaterials.ToList();
                            }
                        }
                    }
                }
            }

            // Step 2b: Check type-specific overrides (e.g., heavy-blades, light-armor)
            var itemType = GetStringProperty(baseItem, "slug") ?? GetStringProperty(baseItem, "name")?.ToLower();
            if (!string.IsNullOrEmpty(itemType))
            {
                // First try exact type match
                if (categoryFilter.Types.TryGetValue(itemType, out var typeFilter))
                {
                    _logger.LogDebug("Type match: {Type}, using materials: {Materials}",
                        itemType, string.Join(", ", typeFilter.AllowedMaterials));
                    return typeFilter.AllowedMaterials.ToList();
                }

                // Try category from catalog structure (e.g., "heavy-blades" from weapon_types)
                var weaponType = FindWeaponTypeFromItem(baseItem);
                if (!string.IsNullOrEmpty(weaponType) && categoryFilter.Types.TryGetValue(weaponType, out var weaponTypeFilter))
                {
                    _logger.LogDebug("Weapon type match: {Type}, using materials: {Materials}",
                        weaponType, string.Join(", ", weaponTypeFilter.AllowedMaterials));
                    return weaponTypeFilter.AllowedMaterials.ToList();
                }
            }

            // Step 2c: Use category default materials
            if (categoryFilter.DefaultMaterials.Any())
            {
                _logger.LogDebug("Using category default materials for '{Category}': {Materials}",
                    categoryKey, string.Join(", ", categoryFilter.DefaultMaterials));
                return categoryFilter.DefaultMaterials.ToList();
            }
        }

        // Step 3: Check defaults for unknown categories
        if (config.Defaults.TryGetValue("unknown", out var defaultFilter))
        {
            _logger.LogDebug("Using unknown default materials: {Materials}", 
                string.Join(", ", defaultFilter.AllowedMaterials));
            return defaultFilter.AllowedMaterials.ToList();
        }

        // Step 4: No materials found (consumables, scrolls, etc.)
        _logger.LogDebug("No materials configured for category '{Category}'", categoryKey);
        return materialTypes;
    }

    /// <summary>
    /// Find weapon type from item by checking parent weapon_types structure in catalog
    /// </summary>
    private string? FindWeaponTypeFromItem(JToken baseItem)
    {
        try
        {
            // Try to navigate up to find weapon_type parent
            var parent = baseItem.Parent;
            while (parent != null)
            {
                if (parent is JProperty prop && parent.Parent?.Parent is JProperty typeProperty)
                {
                    // Check if we're inside weapon_types
                    if (typeProperty.Name == "weapon_types" || typeProperty.Name == "armor_types")
                    {
                        // The property name before "items" array is the type
                        if (prop.Name == "items" && prop.Parent is JObject parentObj)
                        {
                            var typeName = ((JProperty)parentObj.Parent!)?.Name;
                            return typeName;
                        }
                    }
                }
                parent = parent.Parent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding weapon type from item structure");
        }
        return null;
    }
    
    /// <summary>
    /// Create a minimal valid item when generation fails completely.
    /// Ensures we always return something rather than null.
    /// </summary>
    private BudgetItemResult CreateMinimalItem(BudgetItemRequest request)
    {
        _logger.LogWarning("Creating minimal fallback item for {Category}", request.ItemCategory);

        var baseBudget = 10;
        var material = JToken.FromObject(new { name = "Iron", rarityWeight = 100 });
        var baseItem = JToken.FromObject(new { name = "Sword", rarityWeight = 50 });

        return new BudgetItemResult
        {
            BaseBudget = baseBudget,
            AdjustedBudget = baseBudget,
            MaterialBudget = 5,
            ComponentBudget = 5,
            SpentBudget = 10,
            Material = material,
            MaterialCost = 5,
            Quality = null,
            QualityModifier = 0,
            BaseItem = baseItem,
            BaseItemCost = 5,
            Pattern = "{base}",
            PatternCost = 0,
            Components = new List<JToken>(),
            ComponentCosts = new Dictionary<string, int>(),
            Sockets = new Dictionary<SocketType, List<Socket>>()
        };
    }

    /// <summary>
    /// Get all items from a leaf category by loading its catalog directly.
    /// </summary>
    private List<JToken>? GetItemsForCategory(string category)
    {
        // Load direct catalog for this leaf category
        var catalogPath = $"items/{category}/catalog.json";
        var catalogFile = _dataCache.GetFile(catalogPath);
        
        if (catalogFile?.JsonData != null)
        {
            var items = GetItemsFromCatalog(catalogFile.JsonData);
            if (items != null && items.Any())
            {
                return items.ToList();
            }
        }

        _logger.LogWarning("No items found in catalog: {Path}", catalogPath);
        return null;
    }

    /// <summary>
    /// Get names.json data for a leaf category.
    /// Returns null for material subcategories since they only have catalog.json files.
    /// </summary>
    private JToken? GetNamesDataForCategory(string category)
    {
        // Skip names.json lookup for material subcategories - they only have catalog.json
        if (category?.StartsWith("materials/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }
        
        // Load direct names.json for this leaf category
        var namesPath = $"items/{category}/names.json";
        if (!_dataCache.FileExists(namesPath))
        {
            return null;
        }
        
        var namesFile = _dataCache.GetFile(namesPath);
        return namesFile?.JsonData;
    }

    /// <summary>
    /// Select the cheapest base item in the category.
    /// Always succeeds by picking the item with lowest cost.
    /// </summary>
    private Task<JToken> SelectCheapestBaseItemAsync(string category)
    {
        var items = GetItemsForCategory(category);
        if (items == null || !items.Any())
        {
            _logger.LogWarning("No items found for category {Category}, using fallback base item", category);
            return Task.FromResult(JToken.FromObject(new { name = "Basic Item", rarityWeight = 100 }));
        }

        // Sort by cost and pick cheapest
        var cheapest = items
            .OrderBy(item => _budgetCalculator.CalculateMaterialCost(item))
            .First();

        _logger.LogDebug("Selected cheapest base item: {Name} (cost={Cost})", 
            GetStringProperty(cheapest, "name"), 
            _budgetCalculator.CalculateMaterialCost(cheapest));

        return Task.FromResult(cheapest);
    }

    /// <summary>
    /// Select quality that fits within the budget.
    /// Returns the best quality affordable, or none if budget is too low.
    /// </summary>
    private Task<(JToken? Component, double Modifier, int Cost)> SelectAffordableQualityAsync(int availableBudget)
    {
        var namesPath = "items/materials/names.json";
        if (!_dataCache.FileExists(namesPath))
            return Task.FromResult<(JToken?, double, int)>((null, 0.0, 0));

        var namesFile = _dataCache.GetFile(namesPath);
        if (namesFile?.JsonData == null)
            return Task.FromResult<(JToken?, double, int)>((null, 0.0, 0));

        var qualityComponents = namesFile.JsonData["components"]?["quality"];
        if (qualityComponents == null || !qualityComponents.Any())
            return Task.FromResult<(JToken?, double, int)>((null, 0.0, 0));

        // Get all affordable quality options
        var affordableQualities = qualityComponents
            .Where(q =>
            {
                var cost = _budgetCalculator.CalculateComponentCost(q);
                return cost <= availableBudget;
            })
            .Select(q => new
            {
                Component = q,
                Cost = _budgetCalculator.CalculateComponentCost(q),
                Modifier = GetDoubleProperty(q, "budgetModifier", 0.0)
            })
            .OrderByDescending(q => q.Modifier) // Pick best quality affordable
            .ToList();

        if (!affordableQualities.Any())
            return Task.FromResult<(JToken?, double, int)>((null, 0.0, 0));

        var best = affordableQualities.First();
        return Task.FromResult<(JToken?, double, int)>((best.Component, best.Modifier, best.Cost));
    }

    /// <summary>
    /// Select components that fit within budget, prioritizing by cost-effectiveness.
    /// Always succeeds (returns empty list if no budget).
    /// </summary>
    private Task<List<(JToken Component, int Cost)>> SelectAffordableComponentsAsync(
        string category,
        string patternString,
        int availableBudget,
        string rarityTier = "common")
    {
        var result = new List<(JToken Component, int Cost)>();

        if (availableBudget <= 0)
        {
            _logger.LogDebug("No budget for components, skipping");
            return Task.FromResult(result);
        }

        var namesData = GetNamesDataForCategory(category);
        if (namesData == null)
            return Task.FromResult(result);

        var components = namesData["components"];
        if (components == null)
            return Task.FromResult(result);

        // Parse pattern tokens
        var tokens = ExtractTokens(patternString);

        foreach (var token in tokens)
        {
            // Skip base and quality tokens
            if (token == "base" || token == "quality")
                continue;

            var componentArray = components[token];
            if (componentArray == null || !componentArray.Any())
                continue;

            // Get all affordable components for this slot, sorted by cost (cheapest first)
            var affordableComponents = componentArray
                .Select(c => new
                {
                    Component = c,
                    Cost = _budgetCalculator.CalculateComponentCost(c)
                })
                .Where(c => c.Cost <= availableBudget)
                .OrderBy(c => c.Cost)
                .ToList();

            if (!affordableComponents.Any())
            {
                _logger.LogDebug("No affordable {Token} components with budget {Budget}", token, availableBudget);
                continue;
            }

            // Pick cheapest affordable component (deterministic)
            var selected = affordableComponents.First();
            result.Add((selected.Component, selected.Cost));
            availableBudget -= selected.Cost;

            _logger.LogDebug("Selected {Token} component: {Name} (cost={Cost}, remaining={Remaining})", 
                token, GetStringProperty(selected.Component, "value"), selected.Cost, availableBudget);

            // If budget exhausted, stop
            if (availableBudget <= 0)
                break;
        }

        return Task.FromResult(result);
    }

    private Task<JToken> SelectPatternAsync(string category)
    {
        var namesData = GetNamesDataForCategory(category);
        if (namesData == null)
        {
            _logger.LogWarning("Pattern file not found for category {Category}, using default pattern", category);
            return Task.FromResult(JToken.FromObject(new { pattern = "{base}", rarityWeight = 100 }));
        }

        var patterns = namesData["patterns"];
        if (patterns == null || !patterns.Any())
        {
            _logger.LogWarning("No patterns found for category {Category}, using default pattern", category);
            return Task.FromResult(JToken.FromObject(new { pattern = "{base}", rarityWeight = 100 }));
        }

        _logger.LogDebug("Pattern selection: Found {Count} patterns for category {Category}", patterns.Count(), category);
        return Task.FromResult(SelectWeightedRandomPattern(patterns) ?? JToken.FromObject(new { pattern = "{base}", rarityWeight = 100 }));
    }

    private Task<List<(JToken Component, int Cost)>> SelectComponentsAsync(
        string category,
        string patternString,
        int availableBudget,
        string rarityTier = "common")
    {
        var result = new List<(JToken Component, int Cost)>();
        
        // Get component limits for this rarity tier
        var maxPrefixes = _generationRulesService.GetMaxPrefixes(rarityTier);
        var maxSuffixes = _generationRulesService.GetMaxSuffixes(rarityTier);
        var prefixCount = 0;
        var suffixCount = 0;

        var namesData = GetNamesDataForCategory(category);
        if (namesData == null)
            return Task.FromResult(result);

        var components = namesData["components"];
        if (components == null)
            return Task.FromResult(result);

        // Parse pattern tokens (e.g., {prefix}, {suffix}, {descriptive})
        var tokens = ExtractTokens(patternString);

        foreach (var token in tokens)
        {
            // Skip base and quality tokens
            if (token == "base" || token == "quality")
                continue;
            
            // Check if we've hit component limits for this rarity tier
            var isPrefix = token.Contains("prefix");
            var isSuffix = token.Contains("suffix");
            
            if (isPrefix && prefixCount >= maxPrefixes)
            {
                _logger.LogDebug("Skipping prefix - reached limit of {Max} for {Rarity}", maxPrefixes, rarityTier);
                continue;
            }
            if (isSuffix && suffixCount >= maxSuffixes)
            {
                _logger.LogDebug("Skipping suffix - reached limit of {Max} for {Rarity}", maxSuffixes, rarityTier);
                continue;
            }

            var componentArray = components[token];
            if (componentArray == null)
                continue;

            // Try to select affordable component
            var affordableComponents = componentArray
                .Where(c => _budgetCalculator.CanAfford(availableBudget, _budgetCalculator.CalculateComponentCost(c)))
                .ToList();

            if (!affordableComponents.Any())
                continue;

            var selected = SelectWeightedRandomComponent(JToken.FromObject(affordableComponents));
            if (selected != null)
            {
                var cost = _budgetCalculator.CalculateComponentCost(selected);
                result.Add((selected, cost));
                availableBudget -= cost;
                
                // Track component counts
                if (isPrefix) prefixCount++;
                if (isSuffix) suffixCount++;
            }
        }

        return Task.FromResult(result);
    }

    private List<string> ExtractTokens(string patternString)
    {
        var tokens = new List<string>();
        var currentToken = "";
        var inToken = false;

        foreach (var ch in patternString)
        {
            if (ch == '{')
            {
                inToken = true;
                currentToken = "";
            }
            else if (ch == '}')
            {
                if (inToken && !string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                }
                inToken = false;
            }
            else if (inToken)
            {
                currentToken += ch;
            }
        }

        return tokens;
    }

    private JToken? SelectWeightedRandomItem(IEnumerable<JToken> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return null;

        var totalWeight = itemList.Sum(i => GetIntProperty(i, "selectionWeight", 1));
        var randomValue = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var item in itemList)
        {
            cumulative += GetIntProperty(item, "selectionWeight", 1);
            if (randomValue < cumulative)
                return item;
        }

        return itemList.Last();
    }

    private JToken? SelectWeightedRandomPattern(JToken patterns)
    {
        var patternList = patterns.Children().ToList();
        if (!patternList.Any()) return null;

        var totalWeight = patternList.Sum(p => GetIntProperty(p, "rarityWeight", 1));
        var randomValue = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var pattern in patternList)
        {
            cumulative += GetIntProperty(pattern, "rarityWeight", 1);
            if (randomValue < cumulative)
                return pattern;
        }

        return patternList.Last();
    }

    private JToken? SelectWeightedRandomComponent(JToken components)
    {
        var componentList = components.Children().ToList();
        if (!componentList.Any()) return null;

        var totalWeight = componentList.Sum(c => GetIntProperty(c, "rarityWeight", 1));
        var randomValue = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var component in componentList)
        {
            cumulative += GetIntProperty(component, "rarityWeight", 1);
            if (randomValue < cumulative)
                return component;
        }

        return componentList.Last();
    }

    private static IEnumerable<JToken>? GetItemsFromCatalog(JToken catalog)
    {
        var allItems = new List<JToken>();
        
        if (catalog["items"] != null)
        {
            return catalog["items"]?.Children();
        }
        
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name == "metadata") continue;
            
            var typeContainer = property.Value;
            if (typeContainer is JObject typeObj)
            {
                foreach (var typeProperty in typeObj.Children<JProperty>())
                {
                    var items = typeProperty.Value["items"];
                    if (items != null)
                    {
                        foreach (var item in items.Children())
                        {
                            allItems.Add(item);
                        }
                    }
                }
            }
        }

        return allItems.Any() ? allItems : null;
    }

    /// <summary>
    /// Generate sockets for an item based on rarity and item type.
    /// Uses socket-config.json to determine socket counts and types.
    /// </summary>
    private Dictionary<SocketType, List<Socket>> GenerateSocketsForItem(ItemRarity rarity, string itemType)
    {
        var sockets = new Dictionary<SocketType, List<Socket>>();
        
        try
        {
            // Load socket configuration
            var socketConfigPath = "configuration/socket-config.json";
            if (!_dataCache.FileExists(socketConfigPath))
            {
                _logger.LogWarning("Socket config not found at {Path}, returning empty sockets", socketConfigPath);
                return sockets;
            }

            var socketConfig = _dataCache.GetFile(socketConfigPath);
            if (socketConfig?.JsonData == null)
            {
                _logger.LogWarning("Socket config JsonData is null, returning empty sockets");
                return sockets;
            }

            // Get socket count based on rarity
            var socketCounts = socketConfig.JsonData["socketCounts"]?[rarity.ToString()];
            if (socketCounts == null)
            {
                _logger.LogDebug("No socket count configuration for rarity {Rarity}", rarity);
                return sockets;
            }

            var chances = socketCounts["chances"]?.Values<int>().ToArray();
            if (chances == null || chances.Length == 0)
            {
                _logger.LogDebug("No socket chances configured for rarity {Rarity}", rarity);
                return sockets;
            }

            // Randomly determine socket count based on chances
            var totalWeight = chances.Sum();
            var roll = _random.Next(totalWeight);
            var socketCount = 0;
            var cumulative = 0;
            
            for (int i = 0; i < chances.Length; i++)
            {
                cumulative += chances[i];
                if (roll < cumulative)
                {
                    socketCount = i;
                    break;
                }
            }

            if (socketCount == 0)
            {
                _logger.LogDebug("Rolled 0 sockets for rarity {Rarity}", rarity);
                return sockets;
            }

            // Get socket type weights for item type
            var socketTypeWeights = socketConfig.JsonData["socketTypeWeights"]?[itemType];
            if (socketTypeWeights == null)
            {
                _logger.LogWarning("No socket type weights for item type {ItemType}, defaulting to equal weights", itemType);
                socketTypeWeights = new JObject
                {
                    ["Gem"] = 25,
                    ["Rune"] = 25,
                    ["Crystal"] = 25,
                    ["Orb"] = 25
                };
            }

            // Generate sockets
            var gemWeight = socketTypeWeights["Gem"]?.Value<int>() ?? 25;
            var runeWeight = socketTypeWeights["Rune"]?.Value<int>() ?? 25;
            var crystalWeight = socketTypeWeights["Crystal"]?.Value<int>() ?? 25;
            var orbWeight = socketTypeWeights["Orb"]?.Value<int>() ?? 25;
            var typeWeightTotal = gemWeight + runeWeight + crystalWeight + orbWeight;

            var allSockets = new List<(SocketType type, Socket socket)>();

            for (int i = 0; i < socketCount; i++)
            {
                // Randomly select socket type
                var typeRoll = _random.Next(typeWeightTotal);
                SocketType socketType;
                
                if (typeRoll < gemWeight)
                    socketType = SocketType.Gem;
                else if (typeRoll < gemWeight + runeWeight)
                    socketType = SocketType.Rune;
                else if (typeRoll < gemWeight + runeWeight + crystalWeight)
                    socketType = SocketType.Crystal;
                else
                    socketType = SocketType.Orb;

                var socket = new Socket
                {
                    Type = socketType,
                    Content = null,
                    IsLocked = false,
                    LinkGroup = -1 // Assigned below based on link chances
                };

                allSockets.Add((socketType, socket));
            }

            // Apply socket linking based on link chances configuration
            AssignSocketLinks(allSockets, socketCount, rarity, socketConfig);

            // Group sockets by type
            foreach (var (socketType, socket) in allSockets)
            {
                if (!sockets.ContainsKey(socketType))
                {
                    sockets[socketType] = new List<Socket>();
                }
                sockets[socketType].Add(socket);
            }

            _logger.LogDebug("Generated {Count} sockets for {Rarity} {ItemType}", socketCount, rarity, itemType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sockets for {Rarity} {ItemType}", rarity, itemType);
        }

        return sockets;
    }

    /// <summary>
    /// Assigns link groups to sockets based on link chance configuration.
    /// Linked sockets can provide bonus effects when all are filled.
    /// </summary>
    private void AssignSocketLinks(
        List<(SocketType type, Socket socket)> sockets,
        int socketCount,
        ItemRarity rarity,
        CachedJsonFile socketConfig)
    {
        // Get link chances for this rarity
        var linkChances = socketConfig.JsonData["linkChances"]?[rarity.ToString()];
        if (linkChances == null)
        {
            _logger.LogDebug("No link chances configured for rarity {Rarity}, sockets will be unlinked", rarity);
            return;
        }

        // Parse link chances array [2-link%, 3-link%, 4-link%, 5-link%, 6-link%]
        var chances = linkChances["chances"]?.Values<int>().ToArray();
        if (chances == null || chances.Length == 0)
        {
            _logger.LogDebug("Empty link chances for rarity {Rarity}", rarity);
            return;
        }

        // Determine the largest link group possible
        int currentLinkGroup = 0;
        int socketsRemaining = socketCount;
        int socketIndex = 0;

        while (socketsRemaining > 0)
        {
            // Determine link size for this group (starting from largest possible)
            int linkSize = Math.Min(socketsRemaining, Math.Min(chances.Length + 1, 6));
            bool linkedCreated = false;

            // Try to create a link group from largest to smallest
            for (int size = linkSize; size >= 2; size--)
            {
                if (size - 2 >= chances.Length)
                    continue;

                int chance = chances[size - 2]; // Array is 0-indexed for 2-link, 3-link, etc.
                int roll = _random.Next(100);

                if (roll < chance)
                {
                    // Create link group
                    for (int i = 0; i < size && socketIndex < sockets.Count; i++)
                    {
                        sockets[socketIndex].socket.LinkGroup = currentLinkGroup;
                        socketIndex++;
                    }

                    _logger.LogDebug("Created {Size}-link group (group {Group}) for {Rarity} item",
                        size, currentLinkGroup, rarity);

                    socketsRemaining -= size;
                    currentLinkGroup++;
                    linkedCreated = true;
                    break;
                }
            }

            // If no link created, mark next socket as unlinked
            if (!linkedCreated && socketIndex < sockets.Count)
            {
                sockets[socketIndex].socket.LinkGroup = -1;
                socketIndex++;
                socketsRemaining--;
            }
        }
    }

    /// <summary>
    /// Calculate item rarity from budget amount.
    /// Higher budgets indicate rarer items.
    /// </summary>
    private ItemRarity CalculateRarityFromBudget(int budget)
    {
        // Budget thresholds for rarity tiers
        // These values are tuned to match typical item generation budgets
        if (budget >= 500) return ItemRarity.Legendary;
        if (budget >= 300) return ItemRarity.Epic;
        if (budget >= 150) return ItemRarity.Rare;
        if (budget >= 50) return ItemRarity.Uncommon;
        return ItemRarity.Common;
    }

    /// <summary>
    /// Convert item category string to item type for socket generation.
    /// </summary>
    private string GetItemTypeForSockets(string category)
    {
        // Normalize category to socket config format
        return category.ToLowerInvariant() switch
        {
            "weapons" => "Weapon",
            "armor" => "Armor",
            "accessories" => "Accessory",
            _ => "Weapon" // Default to weapon if unknown
        };
    }

    /// <summary>
    /// Calculate rarity tier string from budget for generation rules enforcement.
    /// Uses budget ranges to determine component limits (common, uncommon, rare, epic, legendary).
    /// </summary>
    private static string CalculateRarityTier(int budget)
    {
        return budget switch
        {
            >= 400 => "legendary",
            >= 250 => "epic",
            >= 150 => "rare",
            >= 75 => "uncommon",
            _ => "common"
        };
    }

    private static string GetStringProperty(JToken token, string propertyName)
    {
        return token[propertyName]?.Value<string>() ?? string.Empty;
    }

    private static int GetIntProperty(JToken token, string propertyName, int defaultValue)
    {
        return token[propertyName]?.Value<int>() ?? defaultValue;
    }

    private static double GetDoubleProperty(JToken token, string propertyName, double defaultValue)
    {
        return token[propertyName]?.Value<double>() ?? defaultValue;
    }
}

/// <summary>
/// Request parameters for budget-based item generation.
/// </summary>
public class BudgetItemRequest
{
    /// <summary>Gets or sets the enemy type (humanoid, beast, undead, etc.).</summary>
    public string EnemyType { get; set; } = "default";
    /// <summary>Gets or sets the enemy level.</summary>
    public int EnemyLevel { get; set; } = 1;
    /// <summary>Gets or sets a value indicating whether this is a boss enemy.</summary>
    public bool IsBoss { get; set; } = false;
    /// <summary>Gets or sets a value indicating whether this is an elite enemy.</summary>
    public bool IsElite { get; set; } = false;
    /// <summary>Gets or sets the item category (weapons, armor, accessories, etc.).</summary>
    public string ItemCategory { get; set; } = "weapons";
    /// <summary>Gets or sets a value indicating whether quality modifiers are allowed.</summary>
    public bool AllowQuality { get; set; } = true;
}

/// <summary>
/// Result of budget-based item generation with detailed breakdown.
/// </summary>
public class BudgetItemResult
{
    /// <summary>Gets or sets the base budget before adjustments.</summary>
    public int BaseBudget { get; set; }
    /// <summary>Gets or sets the adjusted budget after multipliers.</summary>
    public int AdjustedBudget { get; set; }
    /// <summary>Gets or sets the budget allocated to materials.</summary>
    public int MaterialBudget { get; set; }
    /// <summary>Gets or sets the budget allocated to components.</summary>
    public int ComponentBudget { get; set; }
    /// <summary>Gets or sets the total budget spent on the item.</summary>
    public int SpentBudget { get; set; }
    
    /// <summary>Gets or sets the selected material data.</summary>
    public JToken? Material { get; set; }
    /// <summary>Gets or sets the material cost.</summary>
    public int MaterialCost { get; set; }
    
    /// <summary>Gets or sets the quality modifier data.</summary>
    public JToken? Quality { get; set; }
    /// <summary>Gets or sets the quality multiplier value.</summary>
    public double QualityModifier { get; set; }
    
    /// <summary>Gets or sets the base item data.</summary>
    public JToken? BaseItem { get; set; }
    /// <summary>Gets or sets the base item cost.</summary>
    public int BaseItemCost { get; set; }
    
    /// <summary>Gets or sets the name pattern for the item.</summary>
    public string Pattern { get; set; } = string.Empty;
    /// <summary>Gets or sets the pattern cost.</summary>
    public int PatternCost { get; set; }
    
    /// <summary>Gets or sets the list of selected components.</summary>
    public List<JToken> Components { get; set; } = new();
    /// <summary>Gets or sets the dictionary of component costs by component type.</summary>
    public Dictionary<string, int> ComponentCosts { get; set; } = new();
    
    /// <summary>Gets or sets the generated sockets for the item.</summary>
    public Dictionary<SocketType, List<Socket>> Sockets { get; set; } = new();
}
