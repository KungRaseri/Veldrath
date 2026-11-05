using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Utilities;

/// <summary>
/// Behaviour tests for the tier tables, RGB projections, weight ranges, comparisons and
/// drop-chance helpers on <see cref="RarityCalculator"/>.
/// </summary>
[Trait("Category", "Utilities")]
public class RarityCalculatorBehaviourTests
{
    // ── RGB ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RarityTier.Common, 1.0f, 1.0f, 1.0f)]
    [InlineData(RarityTier.Uncommon, 0.118f, 1.0f, 0.0f)]
    [InlineData(RarityTier.Rare, 0.0f, 0.439f, 0.867f)]
    [InlineData(RarityTier.Epic, 0.639f, 0.208f, 0.933f)]
    [InlineData(RarityTier.Legendary, 1.0f, 0.502f, 0.0f)]
    public void GetRarityColorRGB_Tier_ReturnsNormalisedComponents(RarityTier tier, float r, float g, float b)
    {
        // Act
        var (actualR, actualG, actualB) = RarityCalculator.GetRarityColorRGB(tier);

        // Assert
        actualR.Should().Be(r);
        actualG.Should().Be(g);
        actualB.Should().Be(b);
    }

    [Fact]
    public void GetRarityColorRGB_FromWeight_DelegatesThroughTheTierTable()
    {
        // Arrange — weight 50 is the Common floor
        var (r, g, b) = RarityCalculator.GetRarityColorRGB(50);

        // Assert
        (r, g, b).Should().Be((1.0f, 1.0f, 1.0f));
    }

    [Fact]
    public void GetRarityColorRGB_FromLowWeight_ReturnsLegendaryOrange()
    {
        // Act
        var (r, g, b) = RarityCalculator.GetRarityColorRGB(1);

        // Assert
        (r, g, b).Should().Be((1.0f, 0.502f, 0.0f));
    }

    [Fact]
    public void GetRarityColorRGB_UndefinedTier_FallsBackToWhite()
    {
        // Act
        var (r, g, b) = RarityCalculator.GetRarityColorRGB((RarityTier)99);

        // Assert
        (r, g, b).Should().Be((1.0f, 1.0f, 1.0f));
    }

    [Fact]
    public void GetRarityColor_UndefinedTier_FallsBackToWhite()
    {
        // Act
        var color = RarityCalculator.GetRarityColor((RarityTier)99);

        // Assert
        color.Should().Be("#FFFFFF");
    }

    // ── Names ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetRarityName_Tier_ReturnsTheEnumName()
    {
        // Act & Assert
        RarityCalculator.GetRarityName(RarityTier.Epic).Should().Be("Epic");
    }

    [Theory]
    [InlineData(100, "Common")]
    [InlineData(30, "Uncommon")]
    [InlineData(20, "Rare")]
    [InlineData(10, "Epic")]
    [InlineData(1, "Legendary")]
    public void GetRarityName_FromWeight_ResolvesTheTierFirst(int weight, string expected)
    {
        // Act & Assert
        RarityCalculator.GetRarityName(weight).Should().Be(expected);
    }

    // ── Weight boundaries ────────────────────────────────────────────────────

    [Theory]
    [InlineData(RarityTier.Common, 50, null)]
    [InlineData(RarityTier.Uncommon, 30, 49)]
    [InlineData(RarityTier.Rare, 15, 29)]
    [InlineData(RarityTier.Epic, 5, 14)]
    [InlineData(RarityTier.Legendary, 1, 4)]
    public void GetMinAndMaxWeight_DescribeTheClosedRangeForEachTier(RarityTier tier, int min, int? max)
    {
        // Act & Assert
        RarityCalculator.GetMinWeight(tier).Should().Be(min);
        RarityCalculator.GetMaxWeight(tier).Should().Be(max);
    }

    [Fact]
    public void GetMinAndMaxWeight_UndefinedTier_DefaultToAnOpenOnePlusRange()
    {
        // Act & Assert
        RarityCalculator.GetMinWeight((RarityTier)99).Should().Be(1);
        RarityCalculator.GetMaxWeight((RarityTier)99).Should().BeNull();
    }

    [Theory]
    [InlineData(RarityTier.Common, "50+")]
    [InlineData(RarityTier.Uncommon, "30-49")]
    [InlineData(RarityTier.Rare, "15-29")]
    [InlineData(RarityTier.Epic, "5-14")]
    [InlineData(RarityTier.Legendary, "1-4")]
    public void GetWeightRangeDisplay_RendersBoundedAndOpenRanges(RarityTier tier, string expected)
    {
        // Act & Assert
        RarityCalculator.GetWeightRangeDisplay(tier).Should().Be(expected);
    }

    // ── Comparisons ──────────────────────────────────────────────────────────

    [Fact]
    public void Compare_Tiers_RanksLegendaryAsRarer()
    {
        // Act & Assert
        RarityCalculator.Compare(RarityTier.Legendary, RarityTier.Common).Should().BeGreaterThan(0);
        RarityCalculator.Compare(RarityTier.Common, RarityTier.Legendary).Should().BeLessThan(0);
        RarityCalculator.Compare(RarityTier.Rare, RarityTier.Rare).Should().Be(0);
    }

    [Fact]
    public void Compare_Weights_ComparesTheResolvedTiers()
    {
        // Act & Assert — lower weight is rarer
        RarityCalculator.Compare(1, 100).Should().BeGreaterThan(0);
        RarityCalculator.Compare(100, 1).Should().BeLessThan(0);
    }

    [Fact]
    public void Compare_WeightsInsideTheSameTier_IsEqual()
    {
        // Act & Assert — 51 and 90 are both Common
        RarityCalculator.Compare(51, 90).Should().Be(0);
    }

    // ── Tier predicates ─────────────────────────────────────────────────────

    [Fact]
    public void IsTier_MatchesTheResolvedTierOnly()
    {
        // Act & Assert
        RarityCalculator.IsTier(20, RarityTier.Rare).Should().BeTrue();
        RarityCalculator.IsTier(20, RarityTier.Epic).Should().BeFalse();
    }

    [Theory]
    [InlineData(20, RarityTier.Epic, false)]
    [InlineData(20, RarityTier.Rare, true)]
    [InlineData(20, RarityTier.Legendary, false)]
    [InlineData(2, RarityTier.Legendary, true)]
    [InlineData(100, RarityTier.Uncommon, false)]
    public void IsAtLeast_IsTrueForTheGivenTierAndEverythingRarer(int weight, RarityTier minimum, bool expected)
    {
        // Act & Assert
        RarityCalculator.IsAtLeast(weight, minimum).Should().Be(expected);
    }

    [Fact]
    public void GetAllTiers_ReturnsEveryTierFromCommonToLegendary()
    {
        // Act
        var tiers = RarityCalculator.GetAllTiers();

        // Assert
        tiers.Should().ContainInOrder(
            RarityTier.Common,
            RarityTier.Uncommon,
            RarityTier.Rare,
            RarityTier.Epic,
            RarityTier.Legendary);
    }

    // ── Formatting ───────────────────────────────────────────────────────────

    [Fact]
    public void GetFormattedName_WrapsTheNameInTierTagAndColour()
    {
        // Act
        var text = RarityCalculator.GetFormattedName(1, "Ancient Sword");

        // Assert
        text.Should().Be("[color=#FF8000][Legendary] Ancient Sword[/color]");
    }

    // ── Drop chances ─────────────────────────────────────────────────────────

    [Fact]
    public void GetDropChancePercent_IsTheItemsShareOfThePool()
    {
        // Act & Assert
        RarityCalculator.GetDropChancePercent(30, 150).Should().Be(20.0);
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(30, -5)]
    public void GetDropChancePercent_WithNoPool_ReturnsZero(int itemWeight, int poolWeight)
    {
        // Act & Assert
        RarityCalculator.GetDropChancePercent(itemWeight, poolWeight).Should().Be(0.0);
    }

    // ── Descriptions ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RarityTier.Common, "Found everywhere. Basic quality.")]
    [InlineData(RarityTier.Uncommon, "Somewhat rare. Decent quality.")]
    [InlineData(RarityTier.Rare, "Hard to find. High quality.")]
    [InlineData(RarityTier.Epic, "Extremely rare. Exceptional quality.")]
    [InlineData(RarityTier.Legendary, "Nearly impossible to find. Mythical quality.")]
    public void GetRarityDescription_ReturnsTierFlavourText(RarityTier tier, string expected)
    {
        // Act & Assert
        RarityCalculator.GetRarityDescription(tier).Should().Be(expected);
    }

    [Fact]
    public void GetRarityDescription_UndefinedTier_ReturnsUnknown()
    {
        // Act & Assert
        RarityCalculator.GetRarityDescription((RarityTier)99).Should().Be("Unknown rarity.");
    }
}
