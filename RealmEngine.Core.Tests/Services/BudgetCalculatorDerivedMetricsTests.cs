using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for the derived/static members of <see cref="BudgetCalculator"/> that the wave-1 suite left
/// uncovered: the breakdown dictionary, affordability helpers, utilization, affordable weight ranges,
/// budget→tier mapping, multi-item cost roll-ups and the zero-weight cost guards.
/// </summary>
[Trait("Category", "Services")]
public class BudgetCalculatorDerivedMetricsTests
{
    private static BudgetCalculator CreateCalculator() => new(
        new BudgetConfig
        {
            Allocation = new BudgetAllocation { MaterialPercentage = 0.30, ComponentPercentage = 0.70 },
            Formulas = new CostFormulas
            {
                Material = new CostFormula { Numerator = 6000 },
                Component = new CostFormula { Numerator = 100 },
                MaterialQuality = new CostFormula { Numerator = 150 }
            },
            PatternCosts = new Dictionary<string, int> { ["{base}"] = 0 },
            SourceMultipliers = new SourceMultipliers
            {
                EnemyLevelMultiplier = 5.0,
                BossMultiplier = 2.5,
                EliteMultiplier = 1.5
            }
        },
        NullLogger<BudgetCalculator>.Instance);

    // ── GetBudgetBreakdown ────────────────────────────────────────────────────

    [Fact]
    public void GetBudgetBreakdown_Splits_The_Budget_Using_The_Configured_Allocation()
    {
        var breakdown = CreateCalculator().GetBudgetBreakdown(100);

        breakdown["Total"].Should().Be(100);
        breakdown["Materials"].Should().Be(30);
        breakdown["Components"].Should().Be(70);
        breakdown["MaterialPercentage"].Should().Be(30);
        breakdown.Should().HaveCount(4);
    }

    [Fact]
    public void GetBudgetBreakdown_Honours_A_Material_Percentage_Override()
    {
        var breakdown = CreateCalculator().GetBudgetBreakdown(250, materialPercentageOverride: 0.4);

        breakdown["Materials"].Should().Be(100);
        breakdown["Components"].Should().Be(150);
        breakdown["MaterialPercentage"].Should().Be(40);
    }

    [Fact]
    public void GetBudgetBreakdown_Rounds_The_Material_Share()
    {
        // 333 * 0.30 = 99.9 → 100 material credits, 233 component credits (rounded to the nearest credit).
        var breakdown = CreateCalculator().GetBudgetBreakdown(333);

        breakdown["Materials"].Should().Be(100);
        breakdown["Components"].Should().Be(233);
        breakdown["MaterialPercentage"].Should().Be(30);
        (breakdown["Materials"] + breakdown["Components"]).Should().Be(breakdown["Total"]);
    }

    // ── Affordability helpers ─────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(9, 10, true)]
    [InlineData(11, 10, false)]
    [InlineData(0, 0, true)]
    public void IsAffordable_Compares_Cost_To_The_Available_Budget(int cost, int budget, bool expected)
    {
        BudgetCalculator.IsAffordable(cost, budget).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 100, "Perfect fit")]
    [InlineData(80, 100, "Affordable (20 remaining)")]
    [InlineData(130, 100, "30 over budget")]
    public void GetAffordabilityDisplay_Describes_The_Budget_Position(int cost, int budget, string expected)
    {
        BudgetCalculator.GetAffordabilityDisplay(cost, budget).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 30, 70)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 150, 0)]
    [InlineData(0, 0, 0)]
    public void GetRemainingBudget_Never_Goes_Below_Zero(int total, int cost, int expected)
    {
        BudgetCalculator.GetRemainingBudget(total, cost).Should().Be(expected);
    }

    [Theory]
    [InlineData(25, 200, 12.5)]
    [InlineData(200, 200, 100.0)]
    [InlineData(300, 200, 150.0)]
    [InlineData(50, 0, 0.0)]
    [InlineData(50, -10, 0.0)]
    public void GetBudgetUtilization_Reports_Spent_Percentage(int spent, int total, double expected)
    {
        BudgetCalculator.GetBudgetUtilization(spent, total).Should().Be(expected);
    }

    // ── Affordability → weight range ──────────────────────────────────────────

    [Theory]
    [InlineData(0, 6000, 100, 100)]
    [InlineData(-5, 6000, 100, 100)]
    [InlineData(500, 6000, 12, 100)]
    [InlineData(120, 6000, 50, 100)]
    [InlineData(10_000, 6000, 1, 100)]
    [InlineData(50, 100, 2, 100)]
    public void GetAffordableWeightRange_Inverts_The_Cost_Formula(
        int budget, int numerator, int expectedMin, int expectedMax)
    {
        var (minWeight, maxWeight) = BudgetCalculator.GetAffordableWeightRange(budget, numerator);

        minWeight.Should().Be(expectedMin);
        maxWeight.Should().Be(expectedMax);
    }

    // ── Budget → quality tier ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "Common")]
    [InlineData(299, "Common")]
    [InlineData(300, "Uncommon")]
    [InlineData(799, "Uncommon")]
    [InlineData(800, "Rare")]
    [InlineData(1999, "Rare")]
    [InlineData(2000, "Epic")]
    [InlineData(4999, "Epic")]
    [InlineData(5000, "Legendary")]
    [InlineData(25_000, "Legendary")]
    public void GetQualityTierFromBudget_Maps_Budget_Thresholds(int budget, string expected)
    {
        BudgetCalculator.GetQualityTierFromBudget(budget).Should().Be(expected);
    }

    // ── Multi-item roll-up ────────────────────────────────────────────────────

    [Fact]
    public void GetMultiItemCostBreakdown_Sums_Per_Item_Costs()
    {
        var breakdown = BudgetCalculator.GetMultiItemCostBreakdown(
            [("iron-sword", 60), ("oak-staff", 25)]);

        breakdown.Should().HaveCount(3);
        breakdown["iron-sword"].Should().Be(60);
        breakdown["oak-staff"].Should().Be(25);
        breakdown["Total"].Should().Be(85);
    }

    [Fact]
    public void GetMultiItemCostBreakdown_Rejects_Duplicate_Item_Names()
    {
        // The breakdown is a dictionary, so the same item listed twice is a caller error.
        var act = () => BudgetCalculator.GetMultiItemCostBreakdown([("iron-sword", 60), ("iron-sword", 60)]);

        act.Should().Throw<ArgumentException>().WithMessage("*iron-sword*");
    }

    [Fact]
    public void GetMultiItemCostBreakdown_Returns_Just_The_Total_For_An_Empty_List()
    {
        var breakdown = BudgetCalculator.GetMultiItemCostBreakdown([]);

        breakdown.Should().ContainSingle().Which.Key.Should().Be("Total");
        breakdown["Total"].Should().Be(0);
    }

    // ── Cost guards ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 120)]
    [InlineData(200, 30)]
    public void CalculateMaterialCost_For_An_Item_Template_Uses_The_Inverse_Formula(int rarityWeight, int expected)
    {
        var template = new ItemTemplate { Slug = "iron-sword", Name = "Iron Sword", RarityWeight = rarityWeight };

        CreateCalculator().CalculateMaterialCost(template).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CalculateMaterialCost_For_An_Item_Template_Rejects_Non_Positive_Weights(int rarityWeight)
    {
        var template = new ItemTemplate { Slug = "broken", Name = "Broken", RarityWeight = rarityWeight };

        CreateCalculator().CalculateMaterialCost(template).Should().Be(999999);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CalculateMaterialCost_For_A_Material_Rejects_Non_Positive_Weights(int rarityWeight)
    {
        var material = new Material { Slug = "broken", RarityWeight = rarityWeight, CostScale = 1.0f };

        CreateCalculator().CalculateMaterialCost(material).Should().Be(999999);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Component_And_Quality_Costs_Reject_Non_Positive_Weights(int rarityWeight)
    {
        var component = new RealmEngine.Data.Entities.NameComponent { ComponentKey = "prefix", RarityWeight = rarityWeight };
        var calculator = CreateCalculator();

        calculator.CalculateComponentCost(component).Should().Be(999999);
        calculator.CalculateQualityCost(component).Should().Be(999999);
    }

    [Theory]
    [InlineData(120, 50)]
    [InlineData(1200, 5)]
    public void CalculateMaterialCost_For_A_Material_Applies_The_Cost_Scale(int rarityWeight, int expected)
    {
        var material = new Material { Slug = "iron", RarityWeight = rarityWeight, CostScale = 1.0f };

        CreateCalculator().CalculateMaterialCost(material).Should().Be(expected);
    }
}
