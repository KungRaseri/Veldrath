using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Entities;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

public class BudgetCalculatorTests
{
    private readonly BudgetCalculator _calculator;
    private readonly BudgetConfig _config;

    public BudgetCalculatorTests()
    {
        _config = new BudgetConfig
        {
            Allocation = new BudgetAllocation
            {
                MaterialPercentage = 0.30,
                ComponentPercentage = 0.70
            },
            Formulas = new CostFormulas
            {
                Material = new CostFormula { Formula = "inverse_scaled", Numerator = 60, Field = "rarityWeight", ScaleField = "costScale" },
                Component = new CostFormula { Formula = "inverse", Numerator = 100, Field = "rarityWeight" },
                Enchantment = new CostFormula { Formula = "inverse", Numerator = 130, Field = "rarityWeight" },
                MaterialQuality = new CostFormula { Formula = "inverse", Numerator = 150, Field = "rarityWeight" }
            },
            PatternCosts = new Dictionary<string, int>
            {
                ["{base}"] = 0,
                ["{prefix} {base} {suffix}"] = 5
            },
            SourceMultipliers = new SourceMultipliers
            {
                EnemyLevelMultiplier = 5.0,
                BossMultiplier = 2.5,
                EliteMultiplier = 1.5
            }
        };

        _calculator = new BudgetCalculator(_config, NullLogger<BudgetCalculator>.Instance);
    }

    [Fact]
    public void CalculateBaseBudget_Level10_Returns50()
    {
        // Arrange & Act
        var result = _calculator.CalculateBaseBudget(10);

        // Assert
        result.Should().Be(50); // 10 * 5.0
    }

    [Fact]
    public void CalculateBaseBudget_Level10Boss_Returns125()
    {
        // Arrange & Act
        var result = _calculator.CalculateBaseBudget(10, isBoss: true);

        // Assert
        result.Should().Be(125); // 10 * 5.0 * 2.5
    }

    [Fact]
    public void CalculateBaseBudget_Level10Elite_Returns75()
    {
        // Arrange & Act
        var result = _calculator.CalculateBaseBudget(10, isElite: true);

        // Assert
        result.Should().Be(75); // 10 * 5.0 * 1.5
    }

    [Fact]
    public void ApplyQualityModifier_PositiveModifier_IncreaseBudget()
    {
        // Arrange
        var baseBudget = 100;
        var qualityModifier = 0.50; // +50%

        // Act
        var result = _calculator.ApplyQualityModifier(baseBudget, qualityModifier);

        // Assert
        result.Should().Be(150); // 100 * (1.0 + 0.5)
    }

    [Fact]
    public void ApplyQualityModifier_NegativeModifier_DecreaseBudget()
    {
        // Arrange
        var baseBudget = 100;
        var qualityModifier = -0.40; // -40%

        // Act
        var result = _calculator.ApplyQualityModifier(baseBudget, qualityModifier);

        // Assert
        result.Should().Be(60); // 100 * (1.0 - 0.4)
    }

    [Fact]
    public void CalculateMaterialBudget_DefaultPercentage_Returns30Percent()
    {
        // Arrange
        var totalBudget = 100;

        // Act
        var result = _calculator.CalculateMaterialBudget(totalBudget);

        // Assert
        result.Should().Be(30); // 100 * 0.30
    }

    [Fact]
    public void CalculateMaterialBudget_CustomPercentage_ReturnsCustomAmount()
    {
        // Arrange
        var totalBudget = 100;

        // Act
        var result = _calculator.CalculateMaterialBudget(totalBudget, 0.40);

        // Assert
        result.Should().Be(40); // 100 * 0.40
    }

    [Fact]
    public void CalculateComponentBudget_ReturnsRemainder()
    {
        // Arrange
        var totalBudget = 100;
        var materialBudget = 30;

        // Act
        var result = _calculator.CalculateComponentBudget(totalBudget, materialBudget);

        // Assert
        result.Should().Be(70); // 100 - 30
    }

    [Fact]
    public void CalculateMaterialCost_InverseFormula_ReturnsInverseCost()
    {
        // Arrange
        var material = new Material { RarityWeight = 60, CostScale = 1.0f };

        // Act
        var result = _calculator.CalculateMaterialCost(material);

        // Assert
        result.Should().Be(1); // 60 / 60 = 1
    }

    [Fact]
    public void CalculateComponentCost_InverseFormula_ReturnsInverseCost()
    {
        // Arrange
        var component = new NameComponent { RarityWeight = 20 };

        // Act
        var result = _calculator.CalculateComponentCost(component);

        // Assert
        result.Should().Be(5); // 100 / 20
    }


    [Fact]
    public void CalculateQualityCost_InverseFormula_ReturnsQualityCost()
    {
        // Arrange
        var quality = new NameComponent { RarityWeight = 20 };

        // Act
        var result = _calculator.CalculateQualityCost(quality);

        // Assert
        result.Should().Be(8); // 150 / 20 = 7.5, rounded to 8
    }

    [Fact]
    public void GetPatternCost_KnownPattern_ReturnsCost()
    {
        // Arrange & Act
        var result = _calculator.GetPatternCost("{prefix} {base} {suffix}");

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void GetPatternCost_UnknownPattern_ReturnsZero()
    {
        // Arrange & Act
        var result = _calculator.GetPatternCost("{unknown} {pattern}");

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, false)]
    [InlineData(50, 51, false)]
    public void CanAfford_VariousBudgets_ReturnsCorrectly(int budget, int cost, bool expected)
    {
        // Act
        var result = _calculator.CanAfford(budget, cost);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateComponentCost_HighRarityWeight_ReturnsLowCost()
    {
        // Arrange - Common component
        var component = new NameComponent { RarityWeight = 50 };

        // Act
        var result = _calculator.CalculateComponentCost(component);

        // Assert
        result.Should().Be(2); // 100 / 50 = 2 (low cost for common)
    }

    [Fact]
    public void CalculateComponentCost_LowRarityWeight_ReturnsHighCost()
    {
        // Arrange - Rare component
        var component = new NameComponent { RarityWeight = 5 };

        // Act
        var result = _calculator.CalculateComponentCost(component);

        // Assert
        result.Should().Be(20); // 100 / 5 = 20 (high cost for rare)
    }

    [Theory]
    [InlineData(1, 5)]    // Level 1 * 5.0 = 5
    [InlineData(5, 25)]   // Level 5 * 5.0 = 25
    [InlineData(10, 50)]  // Level 10 * 5.0 = 50
    [InlineData(20, 100)] // Level 20 * 5.0 = 100
    [InlineData(50, 250)] // Level 50 * 5.0 = 250
    public void CalculateBaseBudget_VariousLevels_ReturnsCorrectBudget(int level, int expectedBudget)
    {
        // Act
        var result = _calculator.CalculateBaseBudget(level);

        // Assert
        result.Should().Be(expectedBudget);
    }

    [Theory]
    [InlineData(0.0, 100)]   // No modifier
    [InlineData(0.25, 125)]  // +25%
    [InlineData(0.50, 150)]  // +50%
    [InlineData(-0.25, 75)]  // -25%
    [InlineData(-0.50, 50)]  // -50%
    public void ApplyQualityModifier_VariousModifiers_CalculatesCorrectly(double modifier, int expectedResult)
    {
        // Arrange
        var baseBudget = 100;

        // Act
        var result = _calculator.ApplyQualityModifier(baseBudget, modifier);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void CalculateMaterialBudget_ZeroPercentage_ReturnsZero()
    {
        // Arrange
        var totalBudget = 100;

        // Act
        var result = _calculator.CalculateMaterialBudget(totalBudget, 0.0);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateMaterialBudget_FullPercentage_ReturnsFullBudget()
    {
        // Arrange
        var totalBudget = 100;

        // Act
        var result = _calculator.CalculateMaterialBudget(totalBudget, 1.0);

        // Assert
        result.Should().Be(100);
    }

    [Theory]
    [InlineData(10, 125)]   // Boss: 10 * 5.0 * 2.5 = 125
    [InlineData(20, 250)]  // Boss: 20 * 5.0 * 2.5 = 250
    public void CalculateBaseBudget_BossEnemy_AppliesBossMultiplier(int level, int expectedBudget)
    {
        // Act
        var result = _calculator.CalculateBaseBudget(level, isBoss: true);

        // Assert - Boss multiplier is 2.5
        result.Should().Be(expectedBudget);
    }

    [Theory]
    [InlineData(10, 75)]   // Elite: 10 * 5.0 * 1.5 = 75
    [InlineData(20, 150)]  // Elite: 20 * 5.0 * 1.5 = 150
    public void CalculateBaseBudget_EliteEnemy_AppliesEliteMultiplier(int level, int expectedBudget)
    {
        // Act
        var result = _calculator.CalculateBaseBudget(level, isElite: true);

        // Assert - Elite multiplier is 1.5
        result.Should().Be(expectedBudget);
    }

    [Fact]
    public void CalculateBaseBudget_BossAndElite_OnlyAppliesBossMultiplier()
    {
        // Act
        var result = _calculator.CalculateBaseBudget(10, isBoss: true, isElite: true);

        // Assert - Should only apply boss multiplier (2.5), not both
        result.Should().Be(125); // 10 * 5.0 * 2.5
    }

    [Theory]
    [InlineData(10, 10, true)]   // 100 / 10 = 10, budget 10 can afford
    [InlineData(20, 10, true)]   // 100 / 20 = 5, budget 10 can afford  
    [InlineData(50, 10, true)]   // 100 / 50 = 2, budget 10 can afford
    [InlineData(1, 5, false)]    // 100 / 1 = 100, budget 5 can't afford
    public void CanAffordComponent_InverseFormula_WorksCorrectly(int rarityWeight, int budget, bool canAfford)
    {
        // Arrange
        var component = new NameComponent { RarityWeight = rarityWeight };
        var cost = _calculator.CalculateComponentCost(component);

        // Act
        var result = _calculator.CanAfford(budget, cost);

        // Assert
        result.Should().Be(canAfford);
    }

    [Fact]
    public void MaterialCost_InverseFormula_HigherWeightLowerCost()
    {
        // Arrange
        var commonMaterial = new Material { RarityWeight = 60, CostScale = 1.0f }; // Common (high weight)
        var rareMaterial = new Material { RarityWeight = 10, CostScale = 1.0f };   // Rare (low weight)

        // Act
        var commonCost = _calculator.CalculateMaterialCost(commonMaterial);
        var rareCost = _calculator.CalculateMaterialCost(rareMaterial);

        // Assert
        commonCost.Should().Be(1);  // 60 / 60 = 1
        rareCost.Should().Be(6);    // 60 / 10 = 6
        rareCost.Should().BeGreaterThan(commonCost, "rare materials should be more expensive");
    }


    [Fact]
    public void QualityCost_HigherThanComponentCost_ForSameWeight()
    {
        // Arrange - Same selection weight for both
        var component = new NameComponent { RarityWeight = 10 };
        var quality = new NameComponent { RarityWeight = 10 };

        // Act
        var componentCost = _calculator.CalculateComponentCost(component);
        var qualityCost = _calculator.CalculateQualityCost(quality);

        // Assert
        qualityCost.Should().BeGreaterThan(componentCost, "quality should be more expensive than components");
    }
}
