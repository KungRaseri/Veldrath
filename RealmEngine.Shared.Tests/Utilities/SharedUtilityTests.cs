using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Utilities;

// Minimal test double for WeightedSelector — requires RarityWeight and Name properties
file class SelectableItem
{
    public required string Name { get; set; }
    public int RarityWeight { get; set; }
}

// Type that lacks RarityWeight — used to verify the ArgumentException path
file class ItemWithoutWeight
{
    public required string Name { get; set; }
}

// Minimal ITraitable implementation for TraitApplicator tests
file class FakeTraitable : ITraitable
{
    public Dictionary<string, TraitValue> Traits { get; } = new();
}

[Trait("Category", "Utilities")]
public class WeightedSelectorTests
{
    [Fact]
    public void SelectByRarityWeight_EmptyCollection_ThrowsArgumentException()
    {
        var act = () => WeightedSelector.SelectByRarityWeight<SelectableItem>([]);
        act.Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void SelectByRarityWeight_SingleItem_ReturnsThatItem()
    {
        var item = new SelectableItem { Name = "Only", RarityWeight = 10 };
        var result = WeightedSelector.SelectByRarityWeight([item]);
        result.Should().BeSameAs(item);
    }

    [Fact]
    public void SelectByRarityWeight_TypeWithoutRarityWeightProperty_ThrowsArgumentException()
    {
        // Two items required to bypass the single-item early return and hit the property check
        var items = new[]
        {
            new ItemWithoutWeight { Name = "a" },
            new ItemWithoutWeight { Name = "b" },
        };
        var act = () => WeightedSelector.SelectByRarityWeight(items);
        act.Should().Throw<ArgumentException>().WithMessage("*RarityWeight*");
    }

    [Fact]
    public void SelectByRarityWeight_ValidCollection_ReturnsItemFromCollection()
    {
        var items = new[]
        {
            new SelectableItem { Name = "Common", RarityWeight = 50 },
            new SelectableItem { Name = "Rare", RarityWeight = 10 },
            new SelectableItem { Name = "Legendary", RarityWeight = 1 },
        };

        var result = WeightedSelector.SelectByRarityWeight(items);

        items.Should().Contain(result);
    }

    [Fact]
    public void CalculateProbability_ReturnsHundredDividedByWeight()
    {
        WeightedSelector.CalculateProbability(10).Should().BeApproximately(10.0, 0.001);
        WeightedSelector.CalculateProbability(50).Should().BeApproximately(2.0, 0.001);
        WeightedSelector.CalculateProbability(1).Should().BeApproximately(100.0, 0.001);
    }

    [Fact]
    public void GetProbabilities_EmptyCollection_ReturnsEmptyDictionary()
    {
        var result = WeightedSelector.GetProbabilities<SelectableItem>([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetProbabilities_ValidItems_ProbabilitiesSumToOneHundred()
    {
        var items = new[]
        {
            new SelectableItem { Name = "A", RarityWeight = 50 },
            new SelectableItem { Name = "B", RarityWeight = 10 },
        };

        var probs = WeightedSelector.GetProbabilities(items);

        probs.Values.Sum().Should().BeApproximately(100.0, 0.001);
    }

    [Fact]
    public void GetMostCommon_ReturnsTopNByHighestRarityWeight()
    {
        var items = new[]
        {
            new SelectableItem { Name = "Legendary", RarityWeight = 1 },
            new SelectableItem { Name = "Common", RarityWeight = 100 },
            new SelectableItem { Name = "Uncommon", RarityWeight = 30 },
        };

        var result = WeightedSelector.GetMostCommon(items, 2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Common");
        result[1].Name.Should().Be("Uncommon");
    }

    [Fact]
    public void GetMostCommon_EmptyCollection_ReturnsEmptyList()
    {
        var result = WeightedSelector.GetMostCommon<SelectableItem>([], 5);
        result.Should().BeEmpty();
    }
}

[Trait("Category", "Utilities")]
public class TraitApplicatorTests
{
    [Fact]
    public void ApplyTrait_AddsSingleTraitToEntity()
    {
        var entity = new FakeTraitable();
        TraitApplicator.ApplyTrait(entity, "strength", 10, TraitType.Number);
        entity.Traits.Should().ContainKey("strength");
        entity.Traits["strength"].AsInt().Should().Be(10);
    }

    [Fact]
    public void ApplyTraits_AddsAllTraitsFromDictionary()
    {
        var entity = new FakeTraitable();
        var traits = new Dictionary<string, TraitValue>
        {
            ["strength"] = new TraitValue(5, TraitType.Number),
            ["fire_resist"] = new TraitValue(20, TraitType.Number),
        };

        TraitApplicator.ApplyTraits(entity, traits);

        entity.Traits.Should().ContainKey("strength");
        entity.Traits.Should().ContainKey("fire_resist");
    }

    [Fact]
    public void GetTrait_ExistingIntTrait_ReturnsValue()
    {
        var entity = new FakeTraitable();
        entity.Traits["power"] = new TraitValue(42, TraitType.Number);

        var result = TraitApplicator.GetTrait(entity, "power", 0);

        result.Should().Be(42);
    }

    [Fact]
    public void GetTrait_MissingTrait_ReturnsDefault()
    {
        var entity = new FakeTraitable();

        var result = TraitApplicator.GetTrait(entity, "missing", 99);

        result.Should().Be(99);
    }

    [Fact]
    public void GetTrait_ExistingStringTrait_ReturnsValue()
    {
        var entity = new FakeTraitable();
        entity.Traits["element"] = new TraitValue("fire", TraitType.String);

        var result = TraitApplicator.GetTrait(entity, "element", "none");

        result.Should().Be("fire");
    }

    [Fact]
    public void HasTrait_ExistingTrait_ReturnsTrue()
    {
        var entity = new FakeTraitable();
        entity.Traits["poisoned"] = new TraitValue(true, TraitType.Boolean);

        TraitApplicator.HasTrait(entity, "poisoned").Should().BeTrue();
    }

    [Fact]
    public void HasTrait_MissingTrait_ReturnsFalse()
    {
        var entity = new FakeTraitable();

        TraitApplicator.HasTrait(entity, "nonexistent").Should().BeFalse();
    }

    [Fact]
    public void RemoveTrait_RemovesExistingTrait()
    {
        var entity = new FakeTraitable();
        entity.Traits["temp"] = new TraitValue(1, TraitType.Number);

        TraitApplicator.RemoveTrait(entity, "temp");

        entity.Traits.Should().NotContainKey("temp");
    }

    [Fact]
    public void GetTraitNames_ReturnsAllKeys()
    {
        var entity = new FakeTraitable();
        entity.Traits["a"] = new TraitValue(1, TraitType.Number);
        entity.Traits["b"] = new TraitValue(2, TraitType.Number);

        var names = TraitApplicator.GetTraitNames(entity);

        names.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void GetTotalStatBonus_SumsMultipleTraits()
    {
        var entity = new FakeTraitable();
        entity.Traits["strengthBonus"] = new TraitValue(5, TraitType.Number);
        entity.Traits["might"] = new TraitValue(3, TraitType.Number);

        var total = TraitApplicator.GetTotalStatBonus(entity, "strengthBonus", "might");

        total.Should().Be(8);
    }

    [Fact]
    public void GetTotalStatBonus_MissingTraits_ReturnsZero()
    {
        var entity = new FakeTraitable();

        var total = TraitApplicator.GetTotalStatBonus(entity, "nonexistent");

        total.Should().Be(0);
    }

    [Fact]
    public void GetResistance_ExistingResistance_ReturnsValue()
    {
        var entity = new FakeTraitable();
        entity.Traits["fireResist"] = new TraitValue(25, TraitType.Number);

        var result = TraitApplicator.GetResistance(entity, "fireResist");

        result.Should().Be(25);
    }
}

[Trait("Category", "Utilities")]
public class RarityCalculatorTests
{
    [Theory]
    [InlineData(100, RarityTier.Common)]
    [InlineData(50, RarityTier.Common)]
    [InlineData(49, RarityTier.Uncommon)]
    [InlineData(30, RarityTier.Uncommon)]
    [InlineData(29, RarityTier.Rare)]
    [InlineData(15, RarityTier.Rare)]
    [InlineData(14, RarityTier.Epic)]
    [InlineData(5, RarityTier.Epic)]
    [InlineData(4, RarityTier.Legendary)]
    [InlineData(1, RarityTier.Legendary)]
    public void GetRarityTier_ReturnsCorrectTier(int weight, RarityTier expected)
    {
        RarityCalculator.GetRarityTier(weight).Should().Be(expected);
    }

    [Theory]
    [InlineData(RarityTier.Common, "#FFFFFF")]
    [InlineData(RarityTier.Uncommon, "#1EFF00")]
    [InlineData(RarityTier.Rare, "#0070DD")]
    [InlineData(RarityTier.Epic, "#A335EE")]
    [InlineData(RarityTier.Legendary, "#FF8000")]
    public void GetRarityColor_Tier_ReturnsCorrectHex(RarityTier tier, string expected)
    {
        RarityCalculator.GetRarityColor(tier).Should().Be(expected);
    }

    [Fact]
    public void GetRarityColor_Weight_DelegatesViaTier()
    {
        // Weight 100 → Common → #FFFFFF
        RarityCalculator.GetRarityColor(100).Should().Be("#FFFFFF");
        // Weight 1 → Legendary → #FF8000
        RarityCalculator.GetRarityColor(1).Should().Be("#FF8000");
    }

    [Fact]
    public void GetRarityColorRGB_Common_ReturnsWhite()
    {
        var (r, g, b) = RarityCalculator.GetRarityColorRGB(RarityTier.Common);
        r.Should().BeApproximately(1.0f, 0.001f);
        g.Should().BeApproximately(1.0f, 0.001f);
        b.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void GetRarityColorRGB_Legendary_ReturnsOrange()
    {
        var (r, g, b) = RarityCalculator.GetRarityColorRGB(RarityTier.Legendary);
        r.Should().BeApproximately(1.0f, 0.001f);
        g.Should().BeApproximately(0.502f, 0.001f);
        b.Should().BeApproximately(0.0f, 0.001f);
    }
}
