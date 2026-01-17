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
    /// Select a material from the appropriate pool for an enemy type, constrained by budget.
    /// </summary>
    public async Task<JToken?> SelectMaterialAsync(string enemyType, int availableBudget)
    {
        try
        {
            // Get enemy type config
            if (!_enemyTypes.Types.TryGetValue(enemyType, out var enemyConfig))
            {
                _logger.LogWarning("Enemy type {EnemyType} not found, using default pool", enemyType);
                enemyConfig = new EnemyTypeConfig { MaterialPool = "default" };
            }

            // Get material pool
            if (!_materialPools.Pools.TryGetValue(enemyConfig.MaterialPool, out var pool))
            {
                _logger.LogError("Material pool {PoolName} not found", enemyConfig.MaterialPool);
                return null;
            }

            // Resolve all materials in pool and filter by budget
            var affordableMaterials = new List<(JToken Material, int Cost, int Weight)>();

            foreach (var (materialRef, entry) in pool.Metals)
            {
                var resolved = await _referenceResolver.ResolveToObjectAsync(materialRef);
                if (resolved == null)
                {
                    _logger.LogWarning("Failed to resolve material reference: {Ref}", materialRef);
                    continue;
                }

                var cost = _budgetCalculator.CalculateMaterialCost(resolved);
                if (_budgetCalculator.CanAfford(availableBudget, cost))
                {
                    affordableMaterials.Add((resolved, cost, entry.RarityWeight));
                }
            }

            if (!affordableMaterials.Any())
            {
                _logger.LogWarning("No affordable materials found for enemy type {EnemyType} with budget {Budget}", 
                    enemyType, availableBudget);
                return null;
            }

            // Select random material based on selection weights
            var selectedMaterial = SelectWeightedRandom(affordableMaterials);
            return selectedMaterial.Material;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting material for enemy type {EnemyType}", enemyType);
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
}
