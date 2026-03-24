using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Data.Entities;
using RealmEngine.Shared.Models;
using EntityNameComponent = RealmEngine.Data.Entities.NameComponent;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Budget-based item generation service implementing forward-building approach.
/// Generates items by allocating budget across materials and components.
/// All content data is sourced from EF Core repositories — no JSON files or DB config blobs.
/// </summary>
public class BudgetItemGenerationService
{
    private readonly BudgetCalculator _budgetCalculator;
    private readonly MaterialPoolService _materialPoolService;
    private readonly BudgetConfigFactory _configFactory;
    private readonly NamePatternService _namePatternService;
    private readonly ItemDataService _itemCatalogLoader;
    private readonly ILogger<BudgetItemGenerationService> _logger;
    private readonly Random _random;

    public BudgetItemGenerationService(
        BudgetCalculator budgetCalculator,
        MaterialPoolService materialPoolService,
        BudgetConfigFactory configFactory,
        NamePatternService namePatternService,
        ItemDataService itemCatalogLoader,
        ILogger<BudgetItemGenerationService> logger)
    {
        _budgetCalculator = budgetCalculator ?? throw new ArgumentNullException(nameof(budgetCalculator));
        _materialPoolService = materialPoolService ?? throw new ArgumentNullException(nameof(materialPoolService));
        _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
        _namePatternService = namePatternService ?? throw new ArgumentNullException(nameof(namePatternService));
        _itemCatalogLoader = itemCatalogLoader ?? throw new ArgumentNullException(nameof(itemCatalogLoader));
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

            var typeMultiplier = _materialPoolService.GetBudgetMultiplier(request.EnemyType);
            baseBudget = (int)Math.Round(baseBudget * typeMultiplier);

            _logger.LogDebug("Base budget calculated: {Budget} (level={Level}, type={Type}, multiplier={Multiplier})",
                baseBudget, request.EnemyLevel, request.EnemyType, typeMultiplier);

            // Step 2: Split budget
            var materialPercentageOverride = _materialPoolService.GetMaterialPercentage(request.EnemyType);
            var materialBudget = _budgetCalculator.CalculateMaterialBudget(baseBudget, materialPercentageOverride);
            var componentBudget = _budgetCalculator.CalculateComponentBudget(baseBudget, materialBudget);

            _logger.LogDebug("Budget split: Material={MaterialBudget}, Components={ComponentBudget}",
                materialBudget, componentBudget);

            // Step 3: Select cheapest base item
            var baseItem = SelectCheapestBaseItem(request.ItemCategory);
            var baseItemCost = _budgetCalculator.CalculateMaterialCost(baseItem);

            _logger.LogDebug("Base item selected: {ItemName} (cost={Cost})", baseItem.Name, baseItemCost);

            // Step 4: Select material
            var materialTypes = GetMaterialTypesForItem(baseItem, request.ItemCategory);
            Material? material = null;
            int materialCost = 0;

            if (materialTypes.Count > 0)
            {
                foreach (var materialType in materialTypes)
                {
                    material = await _materialPoolService.SelectMaterialAsync(materialType, materialBudget);
                    if (material != null) break;
                }

                if (material == null)
                {
                    _logger.LogWarning("No affordable material found, using fallback for category '{Category}'", request.ItemCategory);
                    material = await GetFallbackMaterialAsync(materialTypes[0]);
                }

                if (material is not null)
                {
                    materialCost = _budgetCalculator.CalculateMaterialCost(material);
                    _logger.LogDebug("Material selected: {Name} (cost={Cost})", material.DisplayName ?? material.Slug, materialCost);
                }
            }

            // Step 5: Remaining budget for enhancements
            var remainingBudget = Math.Max(0, componentBudget - baseItemCost);

            // Step 6: Quality (optional)
            EntityNameComponent? qualityComponent = null;
            double qualityModifier = 0.0;
            int qualityCost = 0;

            if (request.AllowQuality && remainingBudget > 0)
            {
                (qualityComponent, qualityModifier, qualityCost) = SelectAffordableQuality(remainingBudget);
                remainingBudget -= qualityCost;

                if (qualityComponent != null)
                    _logger.LogDebug("Quality selected: {Quality} (modifier={Modifier}, cost={Cost})",
                        qualityComponent.Value, qualityModifier, qualityCost);
            }

            // Step 7: Select name pattern
            var patternSet = _namePatternService.GetPatternSet($"items/{request.ItemCategory}");
            var patternTemplate = SelectPattern(patternSet);
            var patternCost = _budgetCalculator.GetPatternCost(patternTemplate);

            // Step 8: Fill remaining budget with affordable components
            var rarityTier = CalculateRarityTier(baseBudget);
            var components = SelectAffordableComponents(patternSet, patternTemplate, remainingBudget, rarityTier);

            // Step 9: Build result
            var totalSpent = materialCost + baseItemCost + qualityCost + patternCost
                + components.Sum(c => c.Cost);

            var result = new BudgetItemResult
            {
                BaseBudget = baseBudget,
                AdjustedBudget = baseBudget,
                MaterialBudget = materialBudget,
                ComponentBudget = componentBudget,
                SpentBudget = totalSpent,
                Material = material,
                MaterialCost = materialCost,
                Quality = qualityComponent,
                QualityModifier = qualityModifier,
                BaseItem = baseItem,
                BaseItemCost = baseItemCost,
                Pattern = patternTemplate,
                PatternCost = patternCost,
                Components = components.Select(c => c.Component).ToList(),
                ComponentCosts = components
                    .GroupBy(c => c.Component.Value)
                    .ToDictionary(g => g.Key, g => g.First().Cost),
                Sockets = new Dictionary<SocketType, List<Socket>>()
            };

            _logger.LogInformation("Item generation completed: Spent {Spent}/{Budget} budget ({Utilization:F1}%)",
                totalSpent, baseBudget, (totalSpent / (double)baseBudget) * 100);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GENERATION FAILED for {EnemyType} level {Level} {Category}: {Message}",
                request.EnemyType, request.EnemyLevel, request.ItemCategory, ex.Message);
            return CreateMinimalItem(request);
        }
    }

    // Private helpers
    private Task<Material?> GetFallbackMaterialAsync(string materialType)
        => _materialPoolService.SelectMaterialAsync(materialType, int.MaxValue);

    private List<string> GetMaterialTypesForItem(ItemTemplate item, string category)
    {
        var config = _configFactory.GetMaterialFilters();
        var categoryKey = category?.ToLower() ?? "unknown";

        if (config.Categories.TryGetValue(categoryKey, out var categoryFilter))
        {
            // Try slug / type-key matching first
            if (!string.IsNullOrEmpty(item.Slug) && categoryFilter.Types.TryGetValue(item.Slug, out var bySlug))
                return bySlug.AllowedMaterials.ToList();

            if (!string.IsNullOrEmpty(item.Type) && categoryFilter.Types.TryGetValue(item.Type, out var byType))
                return byType.AllowedMaterials.ToList();

            if (categoryFilter.DefaultMaterials.Count > 0)
                return categoryFilter.DefaultMaterials.ToList();
        }

        if (config.Defaults.TryGetValue("unknown", out var def) && def.AllowedMaterials.Count > 0)
            return def.AllowedMaterials.ToList();

        return [];
    }

    private ItemTemplate SelectCheapestBaseItem(string category)
    {
        var items = _itemCatalogLoader.LoadCatalog(category);
        if (items.Count == 0)
        {
            _logger.LogWarning("No items found for category {Category}, using fallback", category);
            return new ItemTemplate { Name = "Basic Item", Slug = "basic-item", RarityWeight = 100, Category = category };
        }

        return items.MinBy(i => _budgetCalculator.CalculateMaterialCost(i))!;
    }

    private (EntityNameComponent? Component, double Modifier, int Cost) SelectAffordableQuality(int availableBudget)
    {
        // Quality components live in the "items/materials" name pattern set, key = "quality"
        var patternSet = _namePatternService.GetPatternSet("items/materials");
        if (patternSet is null) return (null, 0.0, 0);

        var qualityPool = patternSet.Components
            .Where(c => c.ComponentKey.Equals("quality", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (qualityPool.Count == 0) return (null, 0.0, 0);

        var affordable = qualityPool
            .Select(c => (Component: c, Cost: _budgetCalculator.CalculateQualityCost(c)))
            .Where(x => x.Cost <= availableBudget)
            .OrderByDescending(x => x.Component.BudgetModifier)
            .ToList();

        if (affordable.Count == 0) return (null, 0.0, 0);

        var best = affordable[0];
        return (best.Component, best.Component.BudgetModifier, best.Cost);
    }

    private string SelectPattern(NamePatternSet? patternSet)
    {
        if (patternSet is null || patternSet.Patterns.Count == 0)
            return "{base}";

        var total = patternSet.Patterns.Sum(p => p.RarityWeight > 0 ? p.RarityWeight : 1);
        var roll = _random.Next(total);
        var cumulative = 0;
        foreach (var p in patternSet.Patterns)
        {
            cumulative += p.RarityWeight > 0 ? p.RarityWeight : 1;
            if (roll < cumulative) return p.Template;
        }
        return patternSet.Patterns.Last().Template;
    }

    private List<(EntityNameComponent Component, int Cost)> SelectAffordableComponents(
        NamePatternSet? patternSet,
        string patternTemplate,
        int availableBudget,
        string rarityTier)
    {
        var result = new List<(EntityNameComponent, int)>();
        if (availableBudget <= 0 || patternSet is null) return result;

        var maxPrefixes = GetMaxComponentsByRarity(rarityTier, "prefixes");
        var maxSuffixes = GetMaxComponentsByRarity(rarityTier, "suffixes");
        int prefixCount = 0, suffixCount = 0;

        var grouped = patternSet.Components
            .GroupBy(c => c.ComponentKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var token in ExtractTokens(patternTemplate))
        {
            if (token is "base" or "quality") continue;

            bool isPrefix = token.Contains("prefix", StringComparison.OrdinalIgnoreCase);
            bool isSuffix = token.Contains("suffix", StringComparison.OrdinalIgnoreCase);

            if (isPrefix && prefixCount >= maxPrefixes) continue;
            if (isSuffix && suffixCount >= maxSuffixes) continue;

            if (!grouped.TryGetValue(token, out var pool)) continue;

            var affordable = pool
                .Select(c => (Component: c, Cost: _budgetCalculator.CalculateComponentCost(c)))
                .Where(x => x.Cost <= availableBudget)
                .OrderBy(x => x.Cost)
                .ToList();

            if (affordable.Count == 0)
            {
                _logger.LogDebug("No affordable {Token} components with budget {Budget}", token, availableBudget);
                continue;
            }

            var selected = affordable[0];
            result.Add(selected);
            availableBudget -= selected.Cost;

            _logger.LogDebug("Selected {Token}: {Name} (cost={Cost}, remaining={Remaining})",
                token, selected.Component.Value, selected.Cost, availableBudget);

            if (isPrefix) prefixCount++;
            if (isSuffix) suffixCount++;
            if (availableBudget <= 0) break;
        }

        return result;
    }

    private BudgetItemResult CreateMinimalItem(BudgetItemRequest request)
    {
        _logger.LogWarning("Creating minimal fallback item for {Category}", request.ItemCategory);
        const int baseBudget = 10;
        return new BudgetItemResult
        {
            BaseBudget = baseBudget,
            AdjustedBudget = baseBudget,
            MaterialBudget = 5,
            ComponentBudget = 5,
            SpentBudget = 10,
            Material = null,
            MaterialCost = 5,
            Quality = null,
            QualityModifier = 0,
            BaseItem = new ItemTemplate { Name = "Sword", Slug = "sword", RarityWeight = 50, Category = request.ItemCategory },
            BaseItemCost = 5,
            Pattern = "{base}",
            PatternCost = 0,
            Components = [],
            ComponentCosts = [],
            Sockets = []
        };
    }

    private static List<string> ExtractTokens(string pattern)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inToken = false;
        foreach (var ch in pattern)
        {
            if (ch == '{') { inToken = true; current.Clear(); }
            else if (ch == '}') { if (inToken && current.Length > 0) tokens.Add(current.ToString()); inToken = false; }
            else if (inToken) current.Append(ch);
        }
        return tokens;
    }

    private static int GetMaxComponentsByRarity(string rarityTier, string componentType) =>
        rarityTier.ToLowerInvariant() switch
        {
            "legendary" or "epic" => 2,
            "rare" => 1,
            _ => 0
        };

    private static string CalculateRarityTier(int budget) => budget switch
    {
        >= 400 => "legendary",
        >= 250 => "epic",
        >= 150 => "rare",
        >= 75 => "uncommon",
        _ => "common"
    };
}

/// <summary>Request parameters for budget-based item generation.</summary>
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

/// <summary>Result of budget-based item generation with detailed breakdown.</summary>
public class BudgetItemResult
{
    public int BaseBudget { get; set; }
    public int AdjustedBudget { get; set; }
    public int MaterialBudget { get; set; }
    public int ComponentBudget { get; set; }
    public int SpentBudget { get; set; }
    public Material? Material { get; set; }
    public int MaterialCost { get; set; }
    public EntityNameComponent? Quality { get; set; }
    public double QualityModifier { get; set; }
    public ItemTemplate? BaseItem { get; set; }
    public int BaseItemCost { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public int PatternCost { get; set; }
    public List<EntityNameComponent> Components { get; set; } = [];
    public Dictionary<string, int> ComponentCosts { get; set; } = [];
    public Dictionary<SocketType, List<Socket>> Sockets { get; set; } = [];
}
