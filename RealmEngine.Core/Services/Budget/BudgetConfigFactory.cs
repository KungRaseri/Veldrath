using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Factory for loading and caching budget system configuration files.
/// </summary>
public class BudgetConfigFactory
{
    private readonly GameConfigService _configService;
    private readonly ILogger<BudgetConfigFactory> _logger;

    private BudgetConfig? _budgetConfig;
    private MaterialPools? _materialPools;
    private EnemyTypes? _enemyTypes;
    private MaterialFilterConfig? _materialFilters;

    public BudgetConfigFactory(GameConfigService configService, ILogger<BudgetConfigFactory> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Load or get cached budget configuration.
    /// </summary>
    public BudgetConfig GetBudgetConfig()
    {
        if (_budgetConfig != null)
            return _budgetConfig;

        try
        {
            var json = _configService.GetData("budget-config");
            if (json == null)
            {
                _logger.LogWarning("budget-config not found in database, using defaults");
                return CreateDefaultBudgetConfig();
            }

            _budgetConfig = JsonConvert.DeserializeObject<BudgetConfig>(json);
            if (_budgetConfig == null)
            {
                _logger.LogError("Failed to deserialize budget-config");
                return CreateDefaultBudgetConfig();
            }

            _logger.LogInformation("Budget configuration loaded successfully");
            return _budgetConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading budget configuration");
            return CreateDefaultBudgetConfig();
        }
    }

    /// <summary>
    /// Load or get cached material pools configuration.
    /// </summary>
    public MaterialPools GetMaterialPools()
    {
        if (_materialPools != null)
            return _materialPools;

        try
        {
            var json = _configService.GetData("material-pools");
            if (json == null)
            {
                _logger.LogWarning("material-pools not found in database, using defaults");
                return CreateDefaultMaterialPools();
            }

            _materialPools = JsonConvert.DeserializeObject<MaterialPools>(json);
            if (_materialPools == null)
            {
                _logger.LogError("Failed to deserialize material-pools");
                return CreateDefaultMaterialPools();
            }

            _logger.LogInformation("Material pools configuration loaded successfully");
            return _materialPools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading material pools configuration");
            return CreateDefaultMaterialPools();
        }
    }

    /// <summary>
    /// Load or get cached enemy types configuration.
    /// </summary>
    public EnemyTypes GetEnemyTypes()
    {
        if (_enemyTypes != null)
            return _enemyTypes;

        try
        {
            var json = _configService.GetData("enemy-types");
            if (json == null)
            {
                _logger.LogWarning("enemy-types not found in database, using defaults");
                return CreateDefaultEnemyTypes();
            }

            _enemyTypes = JsonConvert.DeserializeObject<EnemyTypes>(json);
            if (_enemyTypes == null)
            {
                _logger.LogError("Failed to deserialize enemy-types");
                return CreateDefaultEnemyTypes();
            }

            _logger.LogInformation("Enemy types configuration loaded successfully");
            return _enemyTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading enemy types configuration");
            return CreateDefaultEnemyTypes();
        }
    }

    /// <summary>
    /// Reload all configurations from disk.
    /// </summary>
    public void ReloadConfigurations()
    {
        _budgetConfig = null;
        _materialPools = null;
        _enemyTypes = null;
        _materialFilters = null;
        
        _logger.LogInformation("Budget system configurations cleared, will reload on next access");
    }

    /// <summary>
    /// Load or get cached material filter configuration.
    /// </summary>
    public MaterialFilterConfig GetMaterialFilters()
    {
        if (_materialFilters != null)
            return _materialFilters;

        try
        {
            var json = _configService.GetData("material-filters");
            if (json == null)
            {
                _logger.LogWarning("material-filters not found in database, using defaults");
                return CreateDefaultMaterialFilters();
            }

            _materialFilters = JsonConvert.DeserializeObject<MaterialFilterConfig>(json);
            if (_materialFilters == null)
            {
                _logger.LogError("Failed to deserialize material-filters");
                return CreateDefaultMaterialFilters();
            }

            _logger.LogInformation("Material filter configuration loaded successfully");
            return _materialFilters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading material filter configuration");
            return CreateDefaultMaterialFilters();
        }
    }

    private MaterialFilterConfig CreateDefaultMaterialFilters()
    {
        _logger.LogWarning("Using default material filter configuration");
        return new MaterialFilterConfig
        {
            Defaults = new Dictionary<string, DefaultMaterialFilter>
            {
                ["unknown"] = new DefaultMaterialFilter { AllowedMaterials = new List<string>() }
            },
            Categories = new Dictionary<string, CategoryMaterialFilter>
            {
                ["weapons"] = new CategoryMaterialFilter
                {
                    DefaultMaterials = new List<string> { "metals" },
                    Types = new Dictionary<string, TypeMaterialFilter>()
                },
                ["armor"] = new CategoryMaterialFilter
                {
                    DefaultMaterials = new List<string> { "metals" },
                    Types = new Dictionary<string, TypeMaterialFilter>()
                }
            }
        };
    }

    private BudgetConfig CreateDefaultBudgetConfig()
    {
        _logger.LogWarning("Using default budget configuration");
        return new BudgetConfig
        {
            Allocation = new BudgetAllocation
            {
                MaterialPercentage = 0.30,
                ComponentPercentage = 0.70
            },
            Formulas = new CostFormulas
            {
                Material = new CostFormula { Formula = "inverse_scaled", Numerator = 6000, Field = "rarityWeight", ScaleField = "costScale" },
                Component = new CostFormula { Formula = "inverse", Numerator = 100, Field = "rarityWeight" },
                Enchantment = new CostFormula { Formula = "inverse", Numerator = 130, Field = "rarityWeight" },
                MaterialQuality = new CostFormula { Formula = "inverse", Numerator = 150, Field = "rarityWeight" }
            },
            MinimumCosts = new MinimumCosts
            {
                MaterialQuality = 5,
                Prefix = 3,
                Suffix = 3,
                Descriptive = 3,
                Enchantment = 15,
                Socket = 10
            },
            SourceMultipliers = new SourceMultipliers
            {
                EnemyLevelMultiplier = 5.0,
                ShopTierBase = 30,
                BossMultiplier = 2.5,
                EliteMultiplier = 1.5
            }
        };
    }

    private MaterialPools CreateDefaultMaterialPools()
    {
        _logger.LogWarning("Using default material pools configuration");
        return new MaterialPools
        {
            Pools = new Dictionary<string, MaterialPool>
            {
                ["default"] = new MaterialPool
                {
                    Description = "Default fallback pool",
                    Metals = new Dictionary<string, MaterialPoolEntry>
                    {
                        ["@items/materials:Iron"] = new() { RarityWeight = 40 },
                        ["@items/materials:Steel"] = new() { RarityWeight = 30 },
                        ["@items/materials:Mithril"] = new() { RarityWeight = 20 },
                        ["@items/materials:Adamantine"] = new() { RarityWeight = 10 }
                    }
                }
            }
        };
    }

    private EnemyTypes CreateDefaultEnemyTypes()
    {
        _logger.LogWarning("Using default enemy types configuration");
        return new EnemyTypes
        {
            Types = new Dictionary<string, EnemyTypeConfig>
            {
                ["default"] = new EnemyTypeConfig
                {
                    BudgetMultiplier = 1.0,
                    Description = "Default enemy type"
                }
            }
        };
    }
}
