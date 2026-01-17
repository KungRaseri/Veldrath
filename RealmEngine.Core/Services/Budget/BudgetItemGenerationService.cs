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
    private readonly ILogger<BudgetItemGenerationService> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetItemGenerationService"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="budgetCalculator">The budget calculator.</param>
    /// <param name="materialPoolService">The material pool service.</param>
    /// <param name="logger">The logger instance.</param>
    public BudgetItemGenerationService(
        GameDataCache dataCache,
        ReferenceResolverService referenceResolver,
        BudgetCalculator budgetCalculator,
        MaterialPoolService materialPoolService,
        ILogger<BudgetItemGenerationService> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _budgetCalculator = budgetCalculator ?? throw new ArgumentNullException(nameof(budgetCalculator));
        _materialPoolService = materialPoolService ?? throw new ArgumentNullException(nameof(materialPoolService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Generate an item with budget-based forward building.
    /// </summary>
    public async Task<BudgetItemResult?> GenerateItemAsync(BudgetItemRequest request)
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

            // Step 2: Select material quality (optional, affects total budget)
            JToken? qualityComponent = null;
            var qualityModifier = 0.0;

            if (request.AllowQuality)
            {
                qualityComponent = await SelectQualityAsync();
                if (qualityComponent != null)
                {
                    qualityModifier = GetDoubleProperty(qualityComponent, "budgetModifier", 0.0);
                }
            }

            // Step 3: Apply quality modifier to budget (BEFORE split)
            var adjustedBudget = _budgetCalculator.ApplyQualityModifier(baseBudget, qualityModifier);

            _logger.LogDebug("Quality modifier applied: {Modifier} -> Adjusted budget: {Budget}", 
                qualityModifier, adjustedBudget);

            // Step 4: Split budget into material and component budgets
            var materialPercentageOverride = _materialPoolService.GetMaterialPercentage(request.EnemyType);
            var materialBudget = _budgetCalculator.CalculateMaterialBudget(adjustedBudget, materialPercentageOverride);
            var componentBudget = _budgetCalculator.CalculateComponentBudget(adjustedBudget, materialBudget);

            _logger.LogDebug("Budget split: Material={MaterialBudget}, Components={ComponentBudget}", 
                materialBudget, componentBudget);

            // Step 5: Select material from enemy-specific pool
            var material = await _materialPoolService.SelectMaterialAsync(request.EnemyType, materialBudget);
            if (material == null)
            {
                _logger.LogError("GENERATION FAILED at Step 5: No material found for enemy type '{EnemyType}' with material budget {MaterialBudget}. Check material-pools.json and materials catalog.", 
                    request.EnemyType, materialBudget);
                return null;
            }

            var materialCost = _budgetCalculator.CalculateMaterialCost(material);
            var remainingBudget = componentBudget; // Components use their allocated budget

            _logger.LogDebug("Material selected: {MaterialName} (cost={Cost})", 
                GetStringProperty(material, "name"), materialCost);

            // Step 6: Select base item (weapon/armor type) - uses component budget
            var baseItem = await SelectBaseItemAsync(request.ItemCategory, remainingBudget);
            if (baseItem == null)
            {
                _logger.LogError("GENERATION FAILED at Step 6: No affordable base item in category '{Category}' with component budget {Budget}. Path checked: items/{Category}/catalog.json", 
                    request.ItemCategory, remainingBudget, request.ItemCategory);
                return null;
            }

            var baseItemCost = _budgetCalculator.CalculateMaterialCost(baseItem);
            remainingBudget -= baseItemCost;

            _logger.LogDebug("Base item selected: {ItemName} (cost={Cost}, remaining={Remaining})", 
                GetStringProperty(baseItem, "name"), baseItemCost, remainingBudget);

            // Step 7: Select pattern from names.json
            var pattern = await SelectPatternAsync(request.ItemCategory);
            if (pattern == null)
            {
                _logger.LogError("GENERATION FAILED at Step 7: No pattern found in category '{Category}'. Path checked: items/{Category}/names.json. Verify 'patterns' array exists.", 
                    request.ItemCategory, request.ItemCategory);
                return null;
            }

            var patternString = GetStringProperty(pattern, "pattern");
            var patternCost = _budgetCalculator.GetPatternCost(patternString);
            remainingBudget -= patternCost;

            // Step 8: Forward-build components that fit budget
            var components = await SelectComponentsAsync(
                request.ItemCategory, 
                patternString, 
                remainingBudget);

            // Step 9: Build result
            var result = new BudgetItemResult
            {
                BaseBudget = baseBudget,
                AdjustedBudget = adjustedBudget,
                MaterialBudget = materialBudget,
                ComponentBudget = componentBudget,
                SpentBudget = materialCost + baseItemCost + patternCost + components.Sum(c => c.Cost),
                Material = material,
                MaterialCost = materialCost,
                Quality = qualityComponent,
                QualityModifier = qualityModifier,
                BaseItem = baseItem,
                BaseItemCost = baseItemCost,
                Pattern = patternString,
                PatternCost = patternCost,
                Components = components.Select(c => c.Component).ToList(),
                ComponentCosts = components.ToDictionary(c => GetStringProperty(c.Component, "value"), c => c.Cost)
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GENERATION FAILED with EXCEPTION for {EnemyType} level {Level} {Category}: {Message}. Stack trace logged above.", 
                request.EnemyType, request.EnemyLevel, request.ItemCategory, ex.Message);
            return null;
        }
    }

    private Task<JToken?> SelectQualityAsync()
    {
        var namesPath = "items/materials/names.json";
        if (!_dataCache.FileExists(namesPath))
            return Task.FromResult<JToken?>(null);

        var namesFile = _dataCache.GetFile(namesPath);
        if (namesFile?.JsonData == null)
            return Task.FromResult<JToken?>(null);

        var qualityComponents = namesFile.JsonData["components"]?["quality"];
        if (qualityComponents == null)
            return Task.FromResult<JToken?>(null);

        // 50% chance to not have quality modifier
        if (_random.Next(100) < 50)
            return Task.FromResult<JToken?>(null);

        return Task.FromResult(SelectWeightedRandomComponent(qualityComponents));
    }

    private Task<JToken?> SelectBaseItemAsync(string category, int availableBudget)
    {
        var catalogPath = $"items/{category}/catalog.json";
        if (!_dataCache.FileExists(catalogPath))
        {
            _logger.LogError("Base item selection failed: File not found at {Path}", catalogPath);
            return Task.FromResult<JToken?>(null);
        }

        var catalogFile = _dataCache.GetFile(catalogPath);
        if (catalogFile?.JsonData == null)
        {
            _logger.LogError("Base item selection failed: File exists but JsonData is null at {Path}", catalogPath);
            return Task.FromResult<JToken?>(null);
        }

        var items = GetItemsFromCatalog(catalogFile.JsonData);
        if (items == null || !items.Any())
        {
            _logger.LogError("Base item selection failed: No items found in catalog {Path}. Check file structure.", catalogPath);
            return Task.FromResult<JToken?>(null);
        }

        // Filter by budget
        var affordableItems = items.Where(item =>
        {
            var cost = _budgetCalculator.CalculateMaterialCost(item);
            return _budgetCalculator.CanAfford(availableBudget, cost);
        }).ToList();

        if (!affordableItems.Any())
        {
            _logger.LogError("Base item selection failed: Found {TotalItems} items but none affordable with budget {Budget}. Cheapest item may cost more than budget.", 
                items.Count(), availableBudget);
            return Task.FromResult<JToken?>(null);
        }

        _logger.LogDebug("Base item selection: {AffordableCount}/{TotalCount} items affordable with budget {Budget}", 
            affordableItems.Count, items.Count(), availableBudget);
        return Task.FromResult(SelectWeightedRandomItem(affordableItems));
    }

    private Task<JToken?> SelectPatternAsync(string category)
    {
        var namesPath = $"items/{category}/names.json";
        if (!_dataCache.FileExists(namesPath))
        {
            _logger.LogError("Pattern selection failed: File not found at {Path}", namesPath);
            return Task.FromResult<JToken?>(null);
        }

        var namesFile = _dataCache.GetFile(namesPath);
        if (namesFile?.JsonData == null)
        {
            _logger.LogError("Pattern selection failed: File exists but JsonData is null at {Path}", namesPath);
            return Task.FromResult<JToken?>(null);
        }

        var patterns = namesFile.JsonData["patterns"];
        if (patterns == null)
        {
            _logger.LogError("Pattern selection failed: 'patterns' array not found in {Path}. Check JSON structure.", namesPath);
            return Task.FromResult<JToken?>(null);
        }

        var patternCount = patterns.Children().Count();
        if (patternCount == 0)
        {
            _logger.LogError("Pattern selection failed: 'patterns' array is empty in {Path}", namesPath);
            return Task.FromResult<JToken?>(null);
        }

        _logger.LogDebug("Pattern selection: Found {Count} patterns in {Path}", patternCount, namesPath);
        return Task.FromResult(SelectWeightedRandomPattern(patterns));
    }

    private Task<List<(JToken Component, int Cost)>> SelectComponentsAsync(
        string category,
        string patternString,
        int availableBudget)
    {
        var result = new List<(JToken Component, int Cost)>();

        var namesPath = $"items/{category}/names.json";
        if (!_dataCache.FileExists(namesPath))
            return Task.FromResult(result);

        var namesFile = _dataCache.GetFile(namesPath);
        if (namesFile?.JsonData == null)
            return Task.FromResult(result);

        var components = namesFile.JsonData["components"];
        if (components == null)
            return Task.FromResult(result);

        // Parse pattern tokens (e.g., {prefix}, {suffix}, {descriptive})
        var tokens = ExtractTokens(patternString);

        foreach (var token in tokens)
        {
            // Skip base and quality tokens
            if (token == "base" || token == "quality")
                continue;

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
}
