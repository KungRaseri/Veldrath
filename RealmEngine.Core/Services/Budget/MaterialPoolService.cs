using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Service for selecting materials from enemy-specific pools with budget constraints.
/// </summary>
public class MaterialPoolService
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly BudgetCalculator _budgetCalculator;
    private readonly MaterialPools _materialPools;
    private readonly EnemyTypes _enemyTypes;
    private readonly ILogger<MaterialPoolService> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialPoolService"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="budgetCalculator">The budget calculator.</param>
    /// <param name="materialPools">The material pools configuration.</param>
    /// <param name="enemyTypes">The enemy types configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public MaterialPoolService(
        GameDataCache dataCache,
        ReferenceResolverService referenceResolver,
        BudgetCalculator budgetCalculator,
        MaterialPools materialPools,
        EnemyTypes enemyTypes,
        ILogger<MaterialPoolService> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _budgetCalculator = budgetCalculator ?? throw new ArgumentNullException(nameof(budgetCalculator));
        _materialPools = materialPools ?? throw new ArgumentNullException(nameof(materialPools));
        _enemyTypes = enemyTypes ?? throw new ArgumentNullException(nameof(enemyTypes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Selects a material from the specified material type pool within the budget.
    /// Material type should be: metals, fabrics, leathers, woods, gems, bones, scales, chitin, or crystals.
    /// Returns a material JToken on success, null on failure.
    /// </summary>
    public async Task<JToken?> SelectMaterialAsync(string materialType, int availableBudget)
    {
        try
        {
            // Get material pool directly by type (metals, fabrics, leathers, etc.)
            if (!_materialPools.Pools.TryGetValue(materialType, out var pool))
            {
                _logger.LogError("Material type pool {MaterialType} not found. Valid types: metals, fabrics, leathers, woods, gems, bones, scales, chitin, crystals", materialType);
                return null;
            }

            // Resolve all materials in pool and filter by budget
            var allMaterials = new List<(JToken Material, int Cost, int Weight)>();
            var affordableMaterials = new List<(JToken Material, int Cost, int Weight)>();

            // Support both old (Metals dictionary) and new (rarity tier) structures
            var materialRefs = new List<(string Ref, int Weight)>();
            
            if (pool.Metals != null && pool.Metals.Any())
            {
                // LEGACY: Old structure with Metals dictionary
                foreach (var (materialRef, entry) in pool.Metals)
                {
                    materialRefs.Add((materialRef, entry.RarityWeight));
                }
            }
            else
            {
                // NEW: Rarity tier structure - get all materials across all tiers
                foreach (var matRef in pool.GetAllMaterials())
                {
                    materialRefs.Add((matRef.ItemRef, matRef.RarityWeight));
                }
            }

            // Resolve and evaluate all material references
            foreach (var (materialRef, weight) in materialRefs)
            {
                var resolved = await _referenceResolver.ResolveToObjectAsync(materialRef);
                if (resolved == null)
                {
                    _logger.LogWarning("Failed to resolve material reference: {Ref}", materialRef);
                    continue;
                }

                // Check if this is a material item (with propertyRef) - resolve to property instead
                var propertyRef = GetStringProperty(resolved, "propertyRef");
                if (!string.IsNullOrEmpty(propertyRef))
                {
                    _logger.LogDebug("Material item {ItemName} has propertyRef {PropertyRef}, resolving to property",
                        GetStringProperty(resolved, "name"), propertyRef);
                    var propertyResolved = await _referenceResolver.ResolveToObjectAsync(propertyRef);
                    if (propertyResolved != null)
                    {
                        resolved = propertyResolved;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to resolve propertyRef {PropertyRef} for material {MaterialRef}, using item instead",
                            propertyRef, materialRef);
                    }
                }

                var cost = _budgetCalculator.CalculateMaterialCost(resolved);
                var materialEntry = (resolved, cost, weight);
                allMaterials.Add(materialEntry);
                
                if (_budgetCalculator.CanAfford(availableBudget, cost))
                {
                    affordableMaterials.Add(materialEntry);
                }
            }

            // If no affordable materials, use the cheapest material available
            if (!affordableMaterials.Any())
            {
                if (!allMaterials.Any())
                {
                    _logger.LogError("No materials found in pool {MaterialType}", materialType);
                    return null;
                }
                
                var cheapest = allMaterials.OrderBy(m => m.Cost).First();
                _logger.LogInformation("Budget {Budget} too low for {MaterialType}, using cheapest material {MaterialName} (cost={Cost})", 
                    availableBudget, materialType, GetStringProperty(cheapest.Material, "name"), cheapest.Cost);
                return cheapest.Material;
            }

            // Select random material based on selection weights
            var selectedMaterial = SelectWeightedRandom(affordableMaterials);
            return selectedMaterial.Material;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting material from {MaterialType} pool", materialType);
            return null;
        }
    }

    /// <summary>
    /// Get the material percentage override for an enemy type (if any).
    /// </summary>
    public double? GetMaterialPercentage(string enemyType)
    {
        if (_enemyTypes.Types.TryGetValue(enemyType, out var config))
        {
            return config.MaterialPercentage;
        }
        return null;
    }

    /// <summary>
    /// Get the budget multiplier for an enemy type.
    /// </summary>
    public double GetBudgetMultiplier(string enemyType)
    {
        if (_enemyTypes.Types.TryGetValue(enemyType, out var config))
        {
            return config.BudgetMultiplier;
        }
        return 1.0;
    }

    private (JToken Material, int Cost, int Weight) SelectWeightedRandom(List<(JToken Material, int Cost, int Weight)> items)
    {
        var totalWeight = items.Sum(i => i.Weight);
        var randomValue = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var item in items)
        {
            cumulative += item.Weight;
            if (randomValue < cumulative)
            {
                return item;
            }
        }

        // Fallback to last item
        return items.Last();
    }
    
    private static string GetStringProperty(JToken token, string propertyName, string defaultValue = "")
    {
        try
        {
            return token[propertyName]?.Value<string>() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
