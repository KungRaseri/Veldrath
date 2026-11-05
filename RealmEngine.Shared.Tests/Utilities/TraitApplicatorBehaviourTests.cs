using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Tests.Infrastructure;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Utilities;

/// <summary>
/// Coverage for the trait-application helpers that the existing
/// <c>SharedUtilityTests</c> suite does not exercise: typed getters, merge precedence,
/// numeric accumulation, resistance reporting, debug/UI projections and comparisons.
/// </summary>
[Trait("Category", "Utilities")]
public class TraitApplicatorBehaviourTests
{
    private sealed class FakeTraitable : ITraitable
    {
        public Dictionary<string, TraitValue> Traits { get; } = new();
    }

    private static FakeTraitable Entity(params (string Key, TraitValue Value)[] traits)
    {
        var entity = new FakeTraitable();
        foreach (var (key, value) in traits)
        {
            entity.Traits[key] = value;
        }
        return entity;
    }

    // ── Typed getters ────────────────────────────────────────────────────────

    [Fact]
    public void GetTrait_DoubleTrait_ReturnsDoubleValue()
    {
        // Arrange
        var entity = Entity(("speed", new TraitValue(2.75, TraitType.Number)));

        // Act
        var value = TraitApplicator.GetTrait(entity, "speed", 0.0);

        // Assert
        value.Should().Be(2.75);
    }

    [Fact]
    public void GetTrait_BooleanTrait_ReturnsBooleanValue()
    {
        // Arrange
        var entity = Entity(("cursed", new TraitValue(true, TraitType.Boolean)));

        // Act
        var value = TraitApplicator.GetTrait(entity, "cursed", false);

        // Assert
        value.Should().BeTrue();
    }

    [Fact]
    public void GetTrait_StringListTrait_ReturnsTheList()
    {
        // Arrange
        var entity = Entity(("schools", new TraitValue(new List<string> { "fire", "ice" }, TraitType.StringArray)));

        // Act
        var value = TraitApplicator.GetTrait(entity, "schools", new List<string>());

        // Assert
        value.Should().BeEquivalentTo(["fire", "ice"]);
    }

    [Fact]
    public void GetTrait_IntListTrait_ReturnsTheList()
    {
        // Arrange
        var entity = Entity(("tiers", new TraitValue(new List<int> { 1, 3 }, TraitType.NumberArray)));

        // Act
        var value = TraitApplicator.GetTrait(entity, "tiers", new List<int>());

        // Assert
        value.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void GetTrait_UnsupportedTargetType_ReturnsTheDefault()
    {
        // Arrange
        var entity = Entity(("speed", new TraitValue(2.0, TraitType.Number)));

        // Act — decimal is not one of the converter arms
        var value = TraitApplicator.GetTrait(entity, "speed", 9.5m);

        // Assert
        value.Should().Be(9.5m);
    }

    [Fact]
    public void GetTrait_IntRequestAgainstNumericDouble_TruncatesThroughConvert()
    {
        // Arrange
        var entity = Entity(("speed", new TraitValue(2.9, TraitType.Number)));

        // Act
        var value = TraitApplicator.GetTrait(entity, "speed", 0);

        // Assert
        value.Should().Be(3); // Convert.ToInt32 rounds banker-style
    }

    // ── Merge precedence ────────────────────────────────────────────────────

    [Fact]
    public void MergeTraits_OnlyCopiesKeysTheTargetDoesNotAlreadyHave()
    {
        // Arrange
        var target = Entity(("strength", new TraitValue(5, TraitType.Number)));
        var source = new Dictionary<string, TraitValue>
        {
            ["strength"] = new(99, TraitType.Number),
            ["dexterity"] = new(4, TraitType.Number),
        };

        // Act
        TraitApplicator.MergeTraits(target, source);

        // Assert
        target.Traits["strength"].AsInt().Should().Be(5);
        target.Traits["dexterity"].AsInt().Should().Be(4);
    }

    // ── Numeric accumulation ────────────────────────────────────────────────

    [Fact]
    public void AddNumericBonus_ExistingTrait_AccumulatesAndNormalisesToNumber()
    {
        // Arrange
        var entity = Entity(("attack", new TraitValue("10", TraitType.String)));

        // Act
        TraitApplicator.AddNumericBonus(entity, "attack", 2.5);

        // Assert
        entity.Traits["attack"].Type.Should().Be(TraitType.Number);
        entity.Traits["attack"].AsDouble().Should().Be(12.5);
    }

    [Fact]
    public void AddNumericBonus_MissingTrait_CreatesANumberTrait()
    {
        // Arrange
        var entity = Entity();

        // Act
        TraitApplicator.AddNumericBonus(entity, "attack", 3);

        // Assert
        entity.Traits["attack"].Type.Should().Be(TraitType.Number);
        entity.Traits["attack"].AsDouble().Should().Be(3);
    }

    // ── Resistances ─────────────────────────────────────────────────────────

    [Fact]
    public void HasResistance_IsTrueOnlyForPositiveValues()
    {
        // Arrange
        var entity = Entity(
            (StandardTraits.ResistFire, new TraitValue(20, TraitType.Number)),
            (StandardTraits.ResistIce, new TraitValue(0, TraitType.Number)));

        // Act & Assert
        TraitApplicator.HasResistance(entity, StandardTraits.ResistFire).Should().BeTrue();
        TraitApplicator.HasResistance(entity, StandardTraits.ResistIce).Should().BeFalse();
        TraitApplicator.HasResistance(entity, StandardTraits.ResistPoison).Should().BeFalse();
    }

    [Fact]
    public void GetAllResistances_CollectsOnlyPositiveStandardResistances()
    {
        // Arrange
        var entity = Entity(
            (StandardTraits.ResistFire, new TraitValue(15, TraitType.Number)),
            (StandardTraits.ResistMagic, new TraitValue(5, TraitType.Number)),
            (StandardTraits.ResistIce, new TraitValue(0, TraitType.Number)),
            ("unrelated", new TraitValue(99, TraitType.Number)));

        // Act
        var resistances = TraitApplicator.GetAllResistances(entity);

        // Assert
        resistances.Should().HaveCount(2);
        resistances[StandardTraits.ResistFire].Should().Be(15);
        resistances[StandardTraits.ResistMagic].Should().Be(5);
    }

    [Fact]
    public void GetAllResistances_WithNoResistances_ReturnsEmptyDictionary()
    {
        // Act
        var resistances = TraitApplicator.GetAllResistances(Entity());

        // Assert
        resistances.Should().BeEmpty();
    }

    // ── Debug and UI projections ────────────────────────────────────────────

    [Fact]
    public void DebugTraits_NoTraits_ReturnsPlaceholder()
    {
        // Act
        var text = TraitApplicator.DebugTraits(Entity());

        // Assert
        text.Should().Be("No traits");
    }

    [Fact]
    [UseCulture("en-US")]
    public void DebugTraits_FormatsEachTraitTypeDistinctly()
    {
        // Arrange
        var entity = Entity(
            ("attack", new TraitValue(12.345, TraitType.Number)),
            ("element", new TraitValue("fire", TraitType.String)),
            ("cursed", new TraitValue(true, TraitType.Boolean)),
            ("schools", new TraitValue(new List<string> { "fire", "ice" }, TraitType.StringArray)),
            ("tiers", new TraitValue(new List<int> { 1, 2 }, TraitType.NumberArray)));

        // Act
        var text = TraitApplicator.DebugTraits(entity);

        // Assert
        text.Should().Contain("  attack: 12.35");
        text.Should().Contain("  element: \"fire\"");
        text.Should().Contain("  cursed: True");
        text.Should().Contain("  schools: [fire, ice]");
        text.Should().Contain("  tiers: [1, 2]");
        text.Split('\n').Should().HaveCount(5);
    }

    [Fact]
    public void GetTraitsForUI_UnboxesEveryTraitTypeToPrimitives()
    {
        // Arrange
        var entity = Entity(
            ("attack", new TraitValue(12, TraitType.Number)),
            ("element", new TraitValue("fire", TraitType.String)),
            ("cursed", new TraitValue(true, TraitType.Boolean)),
            ("tiers", new TraitValue(new List<int> { 1, 2 }, TraitType.NumberArray)));

        // Act
        var ui = TraitApplicator.GetTraitsForUI(entity);

        // Assert
        ui["attack"].Should().BeOfType<double>().Which.Should().Be(12);
        ui["element"].Should().Be("fire");
        ui["cursed"].Should().Be(true);
        ui["tiers"].Should().BeAssignableTo<List<int>>();
    }

    [Fact]
    public void GetTraitsForUI_StringArrayBecomesAStringList()
    {
        // Arrange
        var entity = Entity(("schools", new TraitValue(new List<string> { "fire" }, TraitType.StringArray)));

        // Act
        var ui = TraitApplicator.GetTraitsForUI(entity);

        // Assert
        ui["schools"].Should().BeAssignableTo<List<string>>().Which.Should().ContainSingle("fire");
    }

    [Fact]
    public void FilterTraitsByType_KeepsOnlyMatchingTypes()
    {
        // Arrange
        var entity = Entity(
            ("attack", new TraitValue(12, TraitType.Number)),
            ("element", new TraitValue("fire", TraitType.String)),
            ("defense", new TraitValue(4, TraitType.Number)));

        // Act
        var numbers = TraitApplicator.FilterTraitsByType(entity, TraitType.Number);

        // Assert
        numbers.Keys.Should().BeEquivalentTo(["attack", "defense"]);
    }

    [Fact]
    public void GetNumericTraits_ReturnsOnlyNumbersAsDoubles()
    {
        // Arrange
        var entity = Entity(
            ("attack", new TraitValue(12, TraitType.Number)),
            ("element", new TraitValue("fire", TraitType.String)));

        // Act
        var numeric = TraitApplicator.GetNumericTraits(entity);

        // Assert
        numeric.Should().ContainSingle();
        numeric["attack"].Should().Be(12.0);
    }

    [Fact]
    public void GetBooleanTraits_ReturnsOnlyBooleans()
    {
        // Arrange
        var entity = Entity(
            ("cursed", new TraitValue(true, TraitType.Boolean)),
            ("blessed", new TraitValue(false, TraitType.Boolean)),
            ("attack", new TraitValue(12, TraitType.Number)));

        // Act
        var flags = TraitApplicator.GetBooleanTraits(entity);

        // Assert
        flags.Should().HaveCount(2);
        flags["cursed"].Should().BeTrue();
        flags["blessed"].Should().BeFalse();
    }

    // ── Comparisons ─────────────────────────────────────────────────────────

    [Fact]
    public void CompareNumericTraits_ReportsIncreasesDecreasesAndNewKeys()
    {
        // Arrange
        var before = Entity(
            ("attack", new TraitValue(10, TraitType.Number)),
            ("defense", new TraitValue(5, TraitType.Number)));
        var after = Entity(
            ("attack", new TraitValue(15, TraitType.Number)),
            ("defense", new TraitValue(2, TraitType.Number)),
            ("speed", new TraitValue(1, TraitType.Number)));

        // Act
        var diff = TraitApplicator.CompareNumericTraits(before, after);

        // Assert
        diff["attack"].Should().Be(5);
        diff["defense"].Should().Be(-3);
        diff["speed"].Should().Be(1);
    }

    [Fact]
    public void CompareNumericTraits_FiltersOutUnchangedTraits()
    {
        // Arrange
        var before = Entity(("attack", new TraitValue(10, TraitType.Number)));
        var after = Entity(("attack", new TraitValue(10, TraitType.Number)));

        // Act
        var diff = TraitApplicator.CompareNumericTraits(before, after);

        // Assert
        diff.Should().BeEmpty();
    }

    [Fact]
    [UseCulture("en-US")]
    public void FormatTraitDifferences_OrdersByMagnitudeAndSignsValues()
    {
        // Arrange
        var differences = new Dictionary<string, double>
        {
            ["attack"] = 5,
            ["defense"] = -8,
            ["speed"] = 1,
        };

        // Act
        var lines = TraitApplicator.FormatTraitDifferences(differences);

        // Assert
        lines.Should().ContainInOrder("-8.0 defense", "+5.0 attack", "+1.0 speed");
    }

    [Fact]
    [UseCulture("en-US")]
    public void FormatTraitDifferencesBBCode_ColoursGainsAndLosses()
    {
        // Arrange
        var differences = new Dictionary<string, double>
        {
            ["attack"] = 5,
            ["defense"] = -3,
        };

        // Act
        var text = TraitApplicator.FormatTraitDifferencesBBCode(differences);

        // Assert
        text.Split('\n').Should().ContainInOrder(
            "[color=green]+5.0 attack[/color]",
            "[color=red]-3.0 defense[/color]");
    }

    // ── Search and statistics ───────────────────────────────────────────────

    [Fact]
    public void SearchTraits_MatchesKeysCaseInsensitively()
    {
        // Arrange
        var entity = Entity(
            ("FireDamage", new TraitValue(5, TraitType.Number)),
            ("fireResist", new TraitValue(10, TraitType.Number)),
            ("attack", new TraitValue(12, TraitType.Number)));

        // Act
        var matches = TraitApplicator.SearchTraits(entity, "FIRE");

        // Assert
        matches.Keys.Should().BeEquivalentTo(["FireDamage", "fireResist"]);
    }

    [Fact]
    public void GetTraitStatistics_CountsTraitsPerType()
    {
        // Arrange
        var entity = Entity(
            ("attack", new TraitValue(12, TraitType.Number)),
            ("defense", new TraitValue(4, TraitType.Number)),
            ("element", new TraitValue("fire", TraitType.String)));

        // Act
        var stats = TraitApplicator.GetTraitStatistics(entity);

        // Assert
        stats[TraitType.Number].Should().Be(2);
        stats[TraitType.String].Should().Be(1);
    }

    [Fact]
    public void CloneTraits_ReplacesTargetTraitsWithDeepCopies()
    {
        // Arrange
        var source = Entity(("attack", new TraitValue(12, TraitType.Number)));
        var target = Entity(("stale", new TraitValue(1, TraitType.Number)));

        // Act
        TraitApplicator.CloneTraits(source, target);

        // Assert
        target.Traits.Keys.Should().BeEquivalentTo(["attack"]);
        target.Traits["attack"].Should().NotBeSameAs(source.Traits["attack"]);

        source.Traits["attack"] = new TraitValue(99, TraitType.Number);
        target.Traits["attack"].AsInt().Should().Be(12);
    }
}
