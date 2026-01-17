using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using RealmEngine.Core.Services.Budget;
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
                Material = new CostFormula { Formula = "direct", Field = "budgetCost" },
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
    public void CalculateMaterialCost_DirectFormula_ReturnsBudgetCost()
    {
        // Arrange
        var material = JToken.Parse(@"{
            ""name"": ""Iron"",
            ""budgetCost"": 25
        }");

        // Act
        var result = _calculator.CalculateMaterialCost(material);

        // Assert
        result.Should().Be(25);
    }

    [Fact]
    public void CalculateComponentCost_InverseFormula_ReturnsInverseCost()
    {
        // Arrange
        var component = JToken.Parse(@"{
            ""value"": ""Flaming"",
            ""rarityWeight"": 20
        }");

        // Act
        var result = _calculator.CalculateComponentCost(component);

        // Assert
        result.Should().Be(5); // 100 / 20
    }

    [Fact]
    public void CalculateEnchantmentCost_PremiumFormula_ReturnsHigherCost()
    {
        // Arrange
        var enchantment = JToken.Parse(@"{
            ""value"": ""of Power"",
            ""selectionWeight"": 10
        }");

        // Act
        var result = _calculator.CalculateEnchantmentCost(enchantment);

        // Assert
        result.Should().Be(13); // 130 / 10
    }

    [Fact]
    public void CalculateQualityCost_InverseFormula_ReturnsQualityCost()
    {
        // Arrange
        var quality = JToken.Parse(@"{
            ""value"": ""Fine"",
            ""selectionWeight"": 20
        }");

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
    public void CalculateComponentCost_HighSelectionWeight_ReturnsLowCost()
    {
        // Arrange - Common component
        var component = JToken.Parse(@"{
            ""value"": ""Common Prefix"",
            ""selectionWeight"": 50
        }");

        // Act
        var result = _calculator.CalculateComponentCost(component);

        // Assert
        result.Should().Be(2); // 100 / 50 = 2 (low cost for common)
    }

    [Fact]
    public void CalculateComponentCost_LowSelectionWeight_ReturnsHighCost()
    {
        // Arrange - Rare component
        var component = JToken.Parse(@"{
            ""value"": ""Rare Prefix"",
            ""selectionWeight"": 5
        }");

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
    public void CanAffordComponent_InverseFormula_WorksCorrectly(int selectionWeight, int budget, bool canAfford)
    {
        // Arrange
        var component = JToken.Parse($@"{{
            ""value"": ""Test Component"",
            ""selectionWeight"": {selectionWeight}
        }}");

        var cost = _calculator.CalculateComponentCost(component);

        // Act
        var result = _calculator.CanAfford(budget, cost);

        // Assert
        result.Should().Be(canAfford);
    }

    [Fact]
    public void MaterialCost_DirectFormula_UsesBudgetCostDirectly()
    {
        // Arrange
        var cheapMaterial = JToken.Parse(@"{ ""budgetCost"": 10 }");
        var expensiveMaterial = JToken.Parse(@"{ ""budgetCost"": 100 }");

        // Act
        var cheapCost = _calculator.CalculateMaterialCost(cheapMaterial);
        var expensiveCost = _calculator.CalculateMaterialCost(expensiveMaterial);

        // Assert
        cheapCost.Should().Be(10);
        expensiveCost.Should().Be(100);
        expensiveCost.Should().BeGreaterThan(cheapCost);
    }

    [Fact]
    public void ComponentCost_LowerThanEnchantmentCost_ForSameWeight()
    {
        // Arrange - Same selection weight for both
        var component = JToken.Parse(@"{ ""selectionWeight"": 10 }");
        var enchantment = JToken.Parse(@"{ ""selectionWeight"": 10 }");

        // Act
        var componentCost = _calculator.CalculateComponentCost(component);
        var enchantmentCost = _calculator.CalculateEnchantmentCost(enchantment);

        // Assert
        enchantmentCost.Should().BeGreaterThan(componentCost, 
            "enchantments should be more expensive than components with same rarity");
    }

    [Fact]
    public void QualityCost_HighestCostTier_ForSameWeight()
    {
        // Arrange - Same selection weight for all
        var component = JToken.Parse(@"{ ""rarityWeight"": 10 }");
        var enchantment = JToken.Parse(@"{ ""rarityWeight"": 10 }");
        var quality = JToken.Parse(@"{ ""rarityWeight"": 10 }");

        // Act
        var componentCost = _calculator.CalculateComponentCost(component);
        var enchantmentCost = _calculator.CalculateEnchantmentCost(enchantment);
        var qualityCost = _calculator.CalculateQualityCost(quality);

        // Assert
        qualityCost.Should().BeGreaterThan(enchantmentCost, "quality should be most expensive");
        enchantmentCost.Should().BeGreaterThan(componentCost, "enchantments should be more expensive than components");
    }
}
