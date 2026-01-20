using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

public class BudgetConfigFactoryTests
{
    private readonly GameDataCache _dataCache;
    private readonly BudgetConfigFactory _configFactory;

    public BudgetConfigFactoryTests()
    {
        // Use absolute path or path relative to test project
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        dataPath = Path.GetFullPath(dataPath); // Normalize path
        
        _dataCache = new GameDataCache(dataPath);
        _dataCache.LoadAllData();
        _configFactory = new BudgetConfigFactory(_dataCache, NullLogger<BudgetConfigFactory>.Instance);
    }

    [Fact]
    public void GetBudgetConfig_ShouldLoadConfiguration()
    {
        // Act
        var config = _configFactory.GetBudgetConfig();

        // Assert
        config.Should().NotBeNull();
        config.Allocation.Should().NotBeNull();
        config.Formulas.Should().NotBeNull();
    }

    [Fact]
    public void GetMaterialPools_ShouldLoadConfiguration()
    {
        // Act
        var pools = _configFactory.GetMaterialPools();

        // Assert
        pools.Should().NotBeNull();
        pools.Pools.Should().NotBeEmpty();
    }

    [Fact]
    public void GetEnemyTypes_ShouldLoadConfiguration()
    {
        // Act
        var enemyTypes = _configFactory.GetEnemyTypes();

        // Assert
        enemyTypes.Should().NotBeNull();
        enemyTypes.Types.Should().NotBeEmpty();
        enemyTypes.Types.Should().ContainKey("goblin");
    }

    [Fact]
    public void DataCache_ShouldHaveConfigFiles()
    {
        // Act - test different path formats
        var budgetConfig1 = _dataCache.FileExists("general/budget_config.json");
        var budgetConfig2 = _dataCache.FileExists("general\\budget_config.json");
        
        var materialPools1 = _dataCache.FileExists("general/material-pools.json");
        var materialPools2 = _dataCache.FileExists("general\\material-pools.json");
        
        var enemyTypes1 = _dataCache.FileExists("enemies/enemy-types.json");
        var enemyTypes2 = _dataCache.FileExists("enemies\\enemy-types.json");

        // Print what files are actually loaded
        var totalFiles = _dataCache.TotalFilesLoaded;

        // Assert - at least one path format should work
        (budgetConfig1 || budgetConfig2).Should().BeTrue($"budget_config.json should exist (tried forward and backslash). Total files loaded: {totalFiles}");
        (materialPools1 || materialPools2).Should().BeTrue($"material-pools.json should exist. Total files loaded: {totalFiles}");
        (enemyTypes1 || enemyTypes2).Should().BeTrue($"enemy-types.json should exist. Total files loaded: {totalFiles}");
    }

    [Fact]
    public void BudgetConfig_ShouldHaveAllRequiredFields()
    {
        // Act
        var config = _configFactory.GetBudgetConfig();

        // Assert - Allocation
        config.Allocation.Should().NotBeNull();
        config.Allocation.MaterialPercentage.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
        config.Allocation.ComponentPercentage.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
        (config.Allocation.MaterialPercentage + config.Allocation.ComponentPercentage).Should().BeApproximately(1.0, 0.01);

        // Assert - Formulas
        config.Formulas.Should().NotBeNull();
        config.Formulas.Material.Should().NotBeNull();
        config.Formulas.Component.Should().NotBeNull();
        config.Formulas.Enchantment.Should().NotBeNull();
        config.Formulas.MaterialQuality.Should().NotBeNull();

        // Assert - Pattern Costs
        config.PatternCosts.Should().NotBeNull().And.NotBeEmpty();
        config.PatternCosts.Should().ContainKey("{base}");

        // Assert - Source Multipliers
        config.SourceMultipliers.Should().NotBeNull();
        config.SourceMultipliers.EnemyLevelMultiplier.Should().BeGreaterThan(0);
        config.SourceMultipliers.BossMultiplier.Should().BeGreaterThan(1);
        config.SourceMultipliers.EliteMultiplier.Should().BeGreaterThan(1);
    }

    [Fact]
    public void MaterialPools_ShouldHaveAllDeclaredPools()
    {
        // Act
        var pools = _configFactory.GetMaterialPools();

        // Assert - Check common pool types exist
        pools.Pools.Should().ContainKey("humanoid_low");
        pools.Pools.Should().ContainKey("humanoid_mid");
        pools.Pools.Should().ContainKey("humanoid_high");
        pools.Pools.Should().ContainKey("dragon_hoard");
        pools.Pools.Should().ContainKey("elemental_fire");
        pools.Pools.Should().ContainKey("elemental_ice");
        pools.Pools.Should().ContainKey("undead");
        pools.Pools.Should().ContainKey("beast");
        pools.Pools.Should().ContainKey("demonic");

        // Assert - Each pool has metals
        foreach (var pool in pools.Pools.Values)
        {
            pool.Metals.Should().NotBeNull();
            pool.Metals.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void EnemyTypes_ShouldHaveValidConfigurations()
    {
        // Act
        var enemyTypes = _configFactory.GetEnemyTypes();

        // Assert - Check common enemy types
        var expectedTypes = new[] { "goblin", "orc", "troll", "knight", "elite_knight", "dragon", 
                                    "fire_elemental", "ice_elemental", "undead", "skeleton", "demon" };
        
        foreach (var typeName in expectedTypes)
        {
            enemyTypes.Types.Should().ContainKey(typeName, $"{typeName} should be defined");
            var type = enemyTypes.Types[typeName];
            
            type.MaterialPool.Should().NotBeNullOrEmpty($"{typeName} should have materialPool");
            
            var multiplier = type.BudgetMultiplier;
            multiplier.Should().BeGreaterThan(0, $"{typeName} budgetMultiplier must be positive");
        }
    }

    [Fact]
    public void EnemyTypes_WithCustomMaterialPercentage_ShouldUseCustomValue()
    {
        // Act
        var enemyTypes = _configFactory.GetEnemyTypes();

        // Assert - Dragon should have custom material percentage
        var dragon = enemyTypes.Types["dragon"];
        dragon.MaterialPercentage.Should().NotBeNull("dragons should have custom material percentage");
        var dragonMaterialPercentage = dragon.MaterialPercentage!.Value;
        dragonMaterialPercentage.Should().Be(0.40, "dragons should allocate 40% to materials");

        // Assert - Fire elemental should have custom material percentage
        var fireElemental = enemyTypes.Types["fire_elemental"];
        fireElemental.MaterialPercentage.Should().NotBeNull();
        var fireMaterialPercentage = fireElemental.MaterialPercentage!.Value;
        fireMaterialPercentage.Should().Be(0.50, "fire elementals should allocate 50% to materials");

        // Assert - Goblin should NOT have custom material percentage (uses default)
        var goblin = enemyTypes.Types["goblin"];
        var goblinMaterialPercentage = goblin.MaterialPercentage;
        goblinMaterialPercentage.Should().BeNull("goblins should use default material percentage");
    }

    [Fact]
    public void MaterialPools_AllReferences_ShouldBeValid()
    {
        // Act
        var pools = _configFactory.GetMaterialPools();

        // Assert - Each material reference should be properly formatted
        foreach (var poolEntry in pools.Pools)
        {
            var poolName = poolEntry.Key;
            var pool = poolEntry.Value;
            var metals = pool.Metals;
            
            if (metals == null) continue;

            foreach (var (materialRef, entry) in metals)
            {
                materialRef.Should().NotBeNullOrEmpty($"pool '{poolName}' should have materialRef");
                // Accept both old and new reference formats
                (materialRef.StartsWith("@items/materials/") || materialRef.StartsWith("@materials/properties/") || materialRef.StartsWith("@properties/materials/"))
                    .Should().BeTrue($"pool '{poolName}' reference '{materialRef}' should use valid format (@items/materials/ or @materials/properties/ or @properties/materials/)");
                
                entry.RarityWeight.Should().BeGreaterThan(0, $"pool '{poolName}' materials should have positive rarityWeight");
            }
        }
    }

    [Fact]
    public void BudgetConfig_FormulasHaveRequiredFields()
    {
        // Act
        var config = _configFactory.GetBudgetConfig();

        // Assert - Material formula (inverse_scaled)
        config.Formulas.Material.Formula.Should().Be("inverse_scaled");
        config.Formulas.Material.Field.Should().Be("rarityWeight");

        // Assert - Component formula (inverse)
        config.Formulas.Component.Formula.Should().Be("inverse");
        config.Formulas.Component.Field.Should().Be("rarityWeight");
        config.Formulas.Component.Numerator.Should().BeGreaterThan(0);

        // Assert - Enchantment formula (inverse, higher cost)
        config.Formulas.Enchantment.Formula.Should().Be("inverse");
        config.Formulas.Enchantment.Numerator.Should().NotBeNull();
        config.Formulas.Enchantment.Numerator!.Value.Should().BeGreaterThan(config.Formulas.Component.Numerator!.Value, 
            "enchantments should be more expensive than regular components");

        // Assert - Material quality formula (inverse, highest cost)
        config.Formulas.MaterialQuality.Formula.Should().Be("inverse");
        config.Formulas.MaterialQuality.Numerator.Should().NotBeNull();
        config.Formulas.MaterialQuality.Numerator!.Value.Should().BeGreaterThan(config.Formulas.Enchantment.Numerator!.Value,
            "material quality should be most expensive");
    }

    [Fact]
    public void BudgetConfig_PatternCosts_SimplePatternsCostLess()
    {
        // Act
        var config = _configFactory.GetBudgetConfig();

        // Assert
        var simplePattern = config.PatternCosts["{base}"];
        var complexPattern = config.PatternCosts["{prefix} {descriptive} {base} {suffix}"];

        simplePattern.Should().BeLessThan(complexPattern, "complex patterns should cost more budget");
    }

    [Theory]
    [InlineData("goblin", "humanoid_low", 1.0)]
    [InlineData("dragon", "dragon_hoard", 2.5)]
    [InlineData("fire_elemental", "elemental_fire", 1.5)]
    [InlineData("knight", "humanoid_mid", 1.3)]
    public void EnemyTypes_SpecificTypes_HaveCorrectConfiguration(string enemyType, string expectedPool, double expectedMultiplier)
    {
        // Act
        var enemyTypes = _configFactory.GetEnemyTypes();

        // Assert
        enemyTypes.Types.Should().ContainKey(enemyType);
        var type = enemyTypes.Types[enemyType];
        
        type.MaterialPool.Should().Be(expectedPool);
        type.BudgetMultiplier.Should().Be(expectedMultiplier);
    }

    [Theory]
    [InlineData("humanoid_low", "iron", "bronze", "steel")]
    [InlineData("dragon_hoard", "mithril", "adamantine", "dragonbone", "celestial-ore")]
    [InlineData("elemental_fire", "obsidian", "steel", "mithril")]
    public void MaterialPools_SpecificPools_ContainExpectedMaterials(string poolName, params string[] expectedMaterials)
    {
        // Act
        var pools = _configFactory.GetMaterialPools();

        // Assert
        pools.Pools.Should().ContainKey(poolName);
        var pool = pools.Pools[poolName];
        var metals = pool.Metals;

        foreach (var expectedMaterial in expectedMaterials)
        {
            metals.Keys.Should().Contain(materialRef => 
                materialRef.Contains($":{expectedMaterial}") ||
                materialRef.Contains($":{expectedMaterial.Replace("-", "")}"),
                $"pool '{poolName}' should contain material '{expectedMaterial}'");
        }
    }
}
