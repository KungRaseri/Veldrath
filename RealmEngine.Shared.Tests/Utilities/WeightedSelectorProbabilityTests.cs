using FluentAssertions;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Tests.Infrastructure;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Utilities;

/// <summary>
/// Covers the <see cref="WeightedSelector"/> helpers the existing suite leaves untouched:
/// rarest-item ranking, weight-range filtering, expected rolls and the probability displays
/// derived from the pool approximation.
/// </summary>
[Trait("Category", "Utilities")]
public class WeightedSelectorProbabilityTests
{
    private sealed class Loot
    {
        public required string Name { get; set; }
        public int RarityWeight { get; set; }
    }

    private sealed class Nameless
    {
        public int RarityWeight { get; set; }
    }

    private sealed class Unweighted
    {
        public string Name { get; set; } = "n";
    }

    private sealed class Scripted : IRandomSource
    {
        private readonly double _value;
        public Scripted(double value) => _value = value;
        public int Next(int maxValue) => 0;
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble() => _value;
    }

    private static readonly Loot[] Pool =
    [
        new() { Name = "Common", RarityWeight = 50 },
        new() { Name = "Rare", RarityWeight = 10 },
        new() { Name = "Legendary", RarityWeight = 1 },
    ];

    // ── Selection ────────────────────────────────────────────────────────────

    [Fact]
    public void SelectByRarityWeight_WithExplicitRandomSource_UsesThatRoll()
    {
        // Arrange — total weight = 2 + 10 + 100 = 112; a 0.05 roll lands at 5.6, inside "Rare"
        var items = new[]
        {
            new Loot { Name = "Common", RarityWeight = 50 },
            new Loot { Name = "Rare", RarityWeight = 10 },
            new Loot { Name = "Legendary", RarityWeight = 1 },
        };

        // Act
        var selected = WeightedSelector.SelectByRarityWeight(items, new Scripted(0.05));

        // Assert
        selected.Name.Should().Be("Rare");
    }

    [Fact]
    public void SelectByRarityWeight_NullRandomSource_Throws()
    {
        // Act
        var act = () => WeightedSelector.SelectByRarityWeight(Pool, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Rarest ranking ───────────────────────────────────────────────────────

    [Fact]
    public void GetRarest_ReturnsLowestWeightsFirst()
    {
        // Act
        var rarest = WeightedSelector.GetRarest(Pool);

        // Assert
        rarest.Select(l => l.Name).Should().ContainInOrder("Legendary", "Rare", "Common");
    }

    [Fact]
    public void GetRarest_RespectsTheRequestedCount()
    {
        // Act
        var rarest = WeightedSelector.GetRarest(Pool, topN: 1);

        // Assert
        rarest.Should().ContainSingle().Which.Name.Should().Be("Legendary");
    }

    [Fact]
    public void GetRarest_EmptyCollection_ReturnsEmptyList()
    {
        // Act
        var rarest = WeightedSelector.GetRarest(Array.Empty<Loot>());

        // Assert
        rarest.Should().BeEmpty();
    }

    [Fact]
    public void GetRarest_TypeWithoutRarityWeight_Throws()
    {
        // Act
        var act = () => WeightedSelector.GetRarest(new[] { new Unweighted() });

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*RarityWeight*");
    }

    [Fact]
    public void GetMostCommon_TypeWithoutRarityWeight_Throws()
    {
        // Act
        var act = () => WeightedSelector.GetMostCommon(new[] { new Unweighted() });

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*RarityWeight*");
    }

    // ── Probabilities ────────────────────────────────────────────────────────

    [Fact]
    public void GetProbabilities_TypeWithoutName_ReturnsEmptyDictionary()
    {
        // Act
        var probabilities = WeightedSelector.GetProbabilities(new[] { new Nameless { RarityWeight = 10 } });

        // Assert
        probabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetProbabilities_LowerWeightsYieldLargerProbabilityShares()
    {
        // Act
        var probabilities = WeightedSelector.GetProbabilities(Pool);

        // Assert — probability is 100/rarityWeight normalised across the pool, so the
        // lowest weight takes the largest share.
        probabilities["Legendary"].Should().BeGreaterThan(probabilities["Rare"]);
        probabilities["Rare"].Should().BeGreaterThan(probabilities["Common"]);
        probabilities.Values.Sum().Should().BeApproximately(100.0, 1e-9);
    }

    // ── Weight range filtering ──────────────────────────────────────────────

    [Fact]
    public void FilterByWeightRange_KeepsOnlyItemsInsideTheInclusiveBand()
    {
        // Act
        var filtered = WeightedSelector.FilterByWeightRange(Pool, 10, 50);

        // Assert
        filtered.Select(l => l.Name).Should().BeEquivalentTo(["Common", "Rare"]);
    }

    [Fact]
    public void FilterByWeightRange_InclusiveBoundaries_AreKept()
    {
        // Act
        var filtered = WeightedSelector.FilterByWeightRange(Pool, 1, 10);

        // Assert
        filtered.Select(l => l.Name).Should().BeEquivalentTo(["Rare", "Legendary"]);
    }

    [Fact]
    public void FilterByWeightRange_EmptyCollection_ReturnsEmptyList()
    {
        // Act
        var filtered = WeightedSelector.FilterByWeightRange(Array.Empty<Loot>(), 0, 100);

        // Assert
        filtered.Should().BeEmpty();
    }

    [Fact]
    public void FilterByWeightRange_TypeWithoutRarityWeight_Throws()
    {
        // Act
        var act = () => WeightedSelector.FilterByWeightRange(new[] { new Unweighted() }, 0, 100);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*RarityWeight*");
    }

    // ── Expected rolls ──────────────────────────────────────────────────────

    [Fact]
    public void GetExpectedRolls_IsTheReciprocalOfTheApproximateProbability()
    {
        // Act — probability = (100/10) / 100 = 0.1 → 10 rolls
        var rolls = WeightedSelector.GetExpectedRolls(10, 100);

        // Assert
        rolls.Should().Be(10.0);
    }

    [Fact]
    public void GetExpectedRolls_WithNoPool_ReturnsInfinity()
    {
        // Act & Assert
        WeightedSelector.GetExpectedRolls(10, 0).Should().Be(double.PositiveInfinity);
    }

    // ── Display strings ─────────────────────────────────────────────────────

    [Fact]
    [UseCulture("en-US")]
    public void GetProbabilityDisplay_RendersOneDecimalPlace()
    {
        // Act — (100/10)/100 * 100 = 10.0
        var display = WeightedSelector.GetProbabilityDisplay(10, 100);

        // Assert
        display.Should().Be("10.0%");
    }

    [Fact]
    [UseCulture("en-US")]
    public void GetProbabilityDisplay_WithNoPool_ReturnsZeroPercent()
    {
        // Act & Assert
        WeightedSelector.GetProbabilityDisplay(10, 0).Should().Be("0.0%");
    }

    // ── Cumulative probability ──────────────────────────────────────────────

    [Fact]
    public void GetCumulativeProbability_GrowsWithEachRoll()
    {
        // Act
        var oneRoll = WeightedSelector.GetCumulativeProbability(10, 100, 1);
        var tenRolls = WeightedSelector.GetCumulativeProbability(10, 100, 10);

        // Assert
        oneRoll.Should().BeApproximately(10.0, 1e-9);
        tenRolls.Should().BeGreaterThan(oneRoll);
        tenRolls.Should().BeApproximately(65.132, 0.001);
    }

    [Theory]
    [InlineData(10, 0, 5)]
    [InlineData(10, 100, 0)]
    [InlineData(10, 100, -2)]
    public void GetCumulativeProbability_WithNoPoolOrNoRolls_ReturnsZero(int weight, int poolWeight, int rolls)
    {
        // Act & Assert
        WeightedSelector.GetCumulativeProbability(weight, poolWeight, rolls).Should().Be(0.0);
    }

    // ── Probability formula ─────────────────────────────────────────────────

    [Fact]
    public void CalculateProbability_IsOneHundredOverTheWeight()
    {
        // Act & Assert
        WeightedSelector.CalculateProbability(4).Should().Be(25.0);
    }
}
