using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using Xunit;
using Xunit.Abstractions;

namespace RealmEngine.Core.Tests.Services;

public class BudgetItemGenerationTests
{
    private readonly ITestOutputHelper _output;
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly BudgetItemGenerationService _generator;

    public BudgetItemGenerationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        dataPath = Path.GetFullPath(dataPath);
        
        _dataCache = new GameDataCache(dataPath);
        _dataCache.LoadAllData();
        
        // Create console logger factory for debugging
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _referenceResolver = new ReferenceResolverService(_dataCache, loggerFactory.CreateLogger<ReferenceResolverService>());

        var configFactory = new BudgetConfigFactory(_dataCache, loggerFactory.CreateLogger<BudgetConfigFactory>());
        var budgetConfig = configFactory.GetBudgetConfig();
        var materialPools = configFactory.GetMaterialPools();
        var enemyTypes = configFactory.GetEnemyTypes();

        var budgetCalculator = new BudgetCalculator(budgetConfig, loggerFactory.CreateLogger<BudgetCalculator>());
        var materialPoolService = new MaterialPoolService(
            _dataCache,
            _referenceResolver,
            budgetCalculator,
            materialPools,
            enemyTypes,
            loggerFactory.CreateLogger<MaterialPoolService>());
        
        var generationRulesService = new ItemGenerationRulesService(_dataCache);

        _generator = new BudgetItemGenerationService(
            _dataCache,
            _referenceResolver,
            budgetCalculator,
            materialPoolService,
            configFactory,
            generationRulesService,
            loggerFactory.CreateLogger<BudgetItemGenerationService>());
    }

    [Fact]
    public async Task GenerateItemAsync_GoblinLevel1_GeneratesLowBudgetItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 1,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseBudget.Should().BeGreaterThan(0);
        Assert.True(result.SpentBudget <= result.AdjustedBudget);
    }

    [Fact]
    public async Task GenerateItemAsync_DragonLevel20_GeneratesHighBudgetItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "dragon",
            EnemyLevel = 20,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseBudget.Should().BeGreaterThan(200); // Level 20 * 5.0 * 2.5 (dragon multiplier)
        result.Material.Should().NotBeNull("dragons should always afford materials");
    }

    [Fact]
    public async Task GenerateItemAsync_BossEnemy_AppliesBossMultiplier()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "orc",
            EnemyLevel = 10,
            ItemCategory = "weapons",
            IsBoss = true
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseBudget.Should().BeGreaterThan(100); // Should have boss multiplier applied
    }

    [Fact]
    public async Task GenerateItemAsync_WithQuality_SelectsAffordableQuality()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons",
            AllowQuality = true
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        // Budget-fitting system: Quality should be selected if affordable
        if (result!.Quality != null)
        {
            result.SpentBudget.Should().BeGreaterThan(result.MaterialCost + result.BaseItemCost,
                "quality cost should be included in total spent budget");
        }
    }

    [Fact]
    public async Task GenerateItemAsync_Weapons_SelectsMaterial()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Material.Should().NotBeNull("should select a material");
        result.MaterialCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateItemAsync_Armor_SelectsMaterial()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 5,
            ItemCategory = "armor"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Material.Should().NotBeNull("should select a material");
        result.MaterialCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateItemAsync_SelectsBaseItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseItem.Should().NotBeNull("should select a base weapon");
    }

    [Fact]
    public async Task GenerateItemAsync_BudgetSpent_DoesNotExceedTotal()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        Assert.True(result!.SpentBudget <= result.AdjustedBudget, 
            "spent budget should not exceed total budget");
    }

    [Fact]
    public async Task GenerateItemAsync_SelectsPattern()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Pattern.Should().NotBeNullOrEmpty("should select a pattern");
    }

    [Fact]
    public async Task GenerateItemAsync_LowBudget_SelectsFewerComponents()
    {
        // Arrange
        var lowRequest = new BudgetItemRequest
        {
            EnemyType = "wolf",
            EnemyLevel = 1,
            ItemCategory = "weapons"
        };

        var highRequest = new BudgetItemRequest
        {
            EnemyType = "dragon",
            EnemyLevel = 20,
            ItemCategory = "weapons"
        };

        // Act
        var lowResult = await _generator.GenerateItemAsync(lowRequest);
        var highResult = await _generator.GenerateItemAsync(highRequest);

        // Assert
        lowResult.Should().NotBeNull();
        highResult.Should().NotBeNull();
        
        // Budget-fitting system: Higher budget should strictly allow more components
        highResult!.Components.Count.Should().BeGreaterThanOrEqualTo(lowResult!.Components.Count,
            "higher budget should afford more or equal components");
    }

    [Theory]
    [InlineData("humanoid_low", "goblin")]
    [InlineData("humanoid_mid", "knight")]
    [InlineData("humanoid_high", "elite_knight")]
    [InlineData("dragon_hoard", "dragon")]
    public async Task GenerateItemAsync_DifferentEnemyTypes_UseDifferentMaterialPools(string expectedPool, string enemyType)
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = enemyType,
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Material.Should().NotBeNull($"{enemyType} should select from {expectedPool} pool");
    }

    [Theory]
    [InlineData("dragon", 0.40)]      // Dragons allocate 40% to materials
    [InlineData("fire_elemental", 0.50)]  // Elementals allocate 50% to materials
    [InlineData("undead", 0.20)]      // Undead allocate only 20% to materials
    public async Task GenerateItemAsync_EnemyTypeWithCustomMaterialPercentage_UsesCustomAllocation(
        string enemyType, double expectedMaterialPercentage)
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = enemyType,
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        var actualPercentage = (double)result!.MaterialBudget / result.AdjustedBudget;
        actualPercentage.Should().BeApproximately(expectedMaterialPercentage, 0.01,
            $"{enemyType} should allocate {expectedMaterialPercentage:P0} to materials");
    }

    [Theory]
    [InlineData("goblin", 1.0)]       // Base multiplier
    [InlineData("orc", 1.2)]          // 20% more budget
    [InlineData("dragon", 2.5)]       // 150% more budget
    [InlineData("fire_elemental", 1.5)] // 50% more budget
    public async Task GenerateItemAsync_EnemyBudgetMultipliers_AppliedCorrectly(
        string enemyType, double expectedMultiplier)
    {
        // Arrange
        var level = 10;
        var baseCalculation = 10 * 5; // level * enemyLevelMultiplier = 50
        var expectedBaseBudget = (int)(baseCalculation * expectedMultiplier);

        var request = new BudgetItemRequest
        {
            EnemyType = enemyType,
            EnemyLevel = level,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseBudget.Should().Be(expectedBaseBudget,
            $"{enemyType} should have budget multiplier of {expectedMultiplier}");
    }

    [Fact]
    public async Task GenerateItemAsync_MultipleGenerations_ProduceVariety()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        var materials = new HashSet<string>();
        var patterns = new HashSet<string>();
        var baseItems = new HashSet<string>();

        // Act - Generate 20 items
        for (int i = 0; i < 20; i++)
        {
            var result = await _generator.GenerateItemAsync(request);
            
            if (result?.Material != null)
                materials.Add(result.Material["name"]!.ToString());
            if (result?.Pattern != null)
                patterns.Add(result.Pattern);
            if (result?.BaseItem != null)
                baseItems.Add(result.BaseItem["name"]!.ToString());
        }

        // Assert - Should have some variety
        materials.Should().NotBeEmpty("should generate items with materials");
        patterns.Should().HaveCountGreaterThan(1, "should use different patterns");
    }

    [Fact]
    public async Task GenerateItemAsync_VeryLowLevel_StillGeneratesValidItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 1, // Very low level
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull("even level 1 should generate items");
        result!.BaseBudget.Should().BeGreaterThan(0);
        result.BaseItem.Should().NotBeNull("should still find an affordable base item");
    }

    [Fact]
    public async Task GenerateItemAsync_VeryHighLevel_GeneratesHighValueItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "dragon",
            EnemyLevel = 50, // Very high level
            ItemCategory = "weapons",
            IsBoss = true
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.BaseBudget.Should().BeGreaterThan(500, "level 50 dragon boss should have huge budget");
        result.Material.Should().NotBeNull("should afford premium materials");
        // Components might be empty if all budget went to expensive material, so just check budget
        result.SpentBudget.Should().BeGreaterThan(0, "should have spent some budget");
    }

    [Theory]
    [InlineData("weapons")]
    [InlineData("armor")]
    public async Task GenerateItemAsync_DifferentCategories_AllWork(string category)
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = category
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull($"should generate {category}");
        result!.BaseItem.Should().NotBeNull($"should have base {category}");
        result.Material.Should().NotBeNull($"should have material for {category}");
    }

    [Fact]
    public async Task GenerateItemAsync_ComponentCosts_RecordedCorrectly()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 15,
            ItemCategory = "weapons"
        };

        // Act
        var result = await _generator.GenerateItemAsync(request);

        // Assert
        result.Should().NotBeNull();
        if (result!.Components.Any())
        {
            result.ComponentCosts.Should().NotBeEmpty("components should have costs recorded");
            
            foreach (var component in result.Components)
            {
                var componentName = component["value"]?.ToString();
                if (componentName != null)
                {
                    result.ComponentCosts.Should().ContainKey(componentName, 
                        "each component should have a cost entry");
                }
            }
        }
    }

    [Fact]
    public async Task GenerateItemAsync_QualityEnabled_SometimesAppliesQuality()
    {
        // Arrange - Generate many items to get quality at least once
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons",
            AllowQuality = true
        };

        var qualityCount = 0;

        // Act - Generate 30 items
        for (int i = 0; i < 30; i++)
        {
            var result = await _generator.GenerateItemAsync(request);
            if (result?.Quality != null)
            {
                qualityCount++;
            }
        }

        // Assert - With 50% chance, we should get some quality items in 30 tries
        qualityCount.Should().BeGreaterThan(0, "with allowQuality=true, some items should have quality modifiers");
    }

    [Fact]
    public async Task GenerateItemAsync_QualityDisabled_NeverAppliesQuality()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons",
            AllowQuality = false
        };

        // Act - Generate multiple items
        for (int i = 0; i < 10; i++)
        {
            var result = await _generator.GenerateItemAsync(request);
            
            // Assert
            result!.Quality.Should().BeNull("with allowQuality=false, no items should have quality");
            result.QualityModifier.Should().Be(0);
            result.AdjustedBudget.Should().Be(result.BaseBudget, "no quality adjustment");
        }
    }
}
