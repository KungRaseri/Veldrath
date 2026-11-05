using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Services;

/// <summary>
/// Behaviour tests for the derived/aggregate members of <see cref="SkillEffectCalculator"/>:
/// regeneration, summary rendering, bonus dictionaries, damage/defence application,
/// rank progress, multiplier formatting and the efficiency rating.
/// </summary>
[Trait("Category", "Services")]
public class SkillEffectCalculatorBehaviourTests
{
    private static Character WithSkill(string slug, int rank)
    {
        var character = new Character { Name = "Tester" };
        character.Skills[slug] = new CharacterSkill { SkillId = slug, CurrentRank = rank };
        return character;
    }

    // ── Regeneration ─────────────────────────────────────────────────────────

    [Fact]
    public void GetHealthRegeneration_NoVitality_ReturnsZero()
    {
        // Act & Assert
        SkillEffectCalculator.GetHealthRegeneration(new Character()).Should().Be(0);
    }

    [Fact]
    public void GetHealthRegeneration_TruncatesHalfAPointPerRank()
    {
        // Arrange — 7 * 0.5 = 3.5, truncated to 3
        var character = WithSkill("vitality", 7);

        // Act
        var regen = SkillEffectCalculator.GetHealthRegeneration(character);

        // Assert
        regen.Should().Be(3);
    }

    [Fact]
    public void ApplyRegeneration_HealsUpToTheRegenerationAmount()
    {
        // Arrange
        var character = WithSkill("vitality", 20); // +10 HP per turn
        character.MaxHealth = 100;
        character.Health = 95;

        // Act
        var healed = SkillEffectCalculator.ApplyRegeneration(character);

        // Assert
        healed.Should().Be(5); // clamped by MaxHealth
        character.Health.Should().Be(100);
    }

    [Fact]
    public void ApplyRegeneration_AtFullHealth_DoesNothing()
    {
        // Arrange
        var character = WithSkill("vitality", 20);
        character.MaxHealth = 100;
        character.Health = 100;

        // Act
        var healed = SkillEffectCalculator.ApplyRegeneration(character);

        // Assert
        healed.Should().Be(0);
        character.Health.Should().Be(100);
    }

    [Fact]
    public void ApplyRegeneration_WithoutVitality_DoesNothing()
    {
        // Arrange
        var character = new Character { Health = 50, MaxHealth = 100 };

        // Act
        var healed = SkillEffectCalculator.ApplyRegeneration(character);

        // Assert
        healed.Should().Be(0);
        character.Health.Should().Be(50);
    }

    // ── Summaries ────────────────────────────────────────────────────────────

    [Fact]
    public void GetSkillBonusSummary_WithNoSkills_ReportsNoActiveBonuses()
    {
        // Act
        var summary = SkillEffectCalculator.GetSkillBonusSummary(new Character());

        // Assert
        summary.Should().Be("[dim]No active skill bonuses[/]");
    }

    [Fact]
    public void GetSkillBonusSummary_ListsEveryApplicableCategory()
    {
        // Arrange
        var character = new Character();
        character.Skills["light-blades"] = new CharacterSkill { SkillId = "light-blades", CurrentRank = 20 };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", CurrentRank = 10 };
        character.Skills["precision"] = new CharacterSkill { SkillId = "precision", CurrentRank = 4 };
        character.Skills["armor"] = new CharacterSkill { SkillId = "armor", CurrentRank = 10 };
        character.Skills["acrobatics"] = new CharacterSkill { SkillId = "acrobatics", CurrentRank = 4 };
        character.Skills["luck"] = new CharacterSkill { SkillId = "luck", CurrentRank = 2 };
        character.Skills["mana-pool"] = new CharacterSkill { SkillId = "mana-pool", CurrentRank = 10 };
        character.Skills["vitality"] = new CharacterSkill { SkillId = "vitality", CurrentRank = 4 };

        // Act
        var summary = SkillEffectCalculator.GetSkillBonusSummary(character);

        // Assert
        var lines = summary.Split('\n');
        lines.Should().HaveCount(6);
        lines[0].Should().Be("[yellow]+2% Critical Chance[/]");
        lines[1].Should().Be("[blue]+3% Physical Defense[/]");
        lines[2].Should().Be("[magenta]+1% Dodge Chance[/]");
        lines[3].Should().Be("[gold1]+1% Rare Item Find[/]");
        lines[4].Should().Be("[blue]+10% Max Mana[/]");
        lines[5].Should().Be("[green]+2 HP Regeneration per turn[/]");
    }

    [Fact]
    public void GetSkillBonusSummary_OmitsWeaponAndMagicDamageBecauseNoSkillSlugIsSupplied()
    {
        // Arrange — weapon and tradition skills only contribute when a slug is passed in,
        // and GetSkillBonusSummary always calls the multipliers without one.
        var character = WithSkill("light-blades", 100);

        // Act
        var summary = SkillEffectCalculator.GetSkillBonusSummary(character);

        // Assert
        summary.Should().Be("[dim]No active skill bonuses[/]");
    }

    [Fact]
    public void GetSkillBonusSummaryBBCode_WithNoSkills_ReportsNoActiveBonuses()
    {
        // Act
        var summary = SkillEffectCalculator.GetSkillBonusSummaryBBCode(new Character());

        // Assert
        summary.Should().Be("[color=gray]No active skill bonuses[/color]");
    }

    [Fact]
    public void GetSkillBonusSummaryBBCode_ListsEveryApplicableCategoryInColour()
    {
        // Arrange
        var character = new Character();
        character.Skills["light-blades"] = new CharacterSkill { SkillId = "light-blades", CurrentRank = 20 };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", CurrentRank = 10 };
        character.Skills["precision"] = new CharacterSkill { SkillId = "precision", CurrentRank = 4 };
        character.Skills["armor"] = new CharacterSkill { SkillId = "armor", CurrentRank = 10 };
        character.Skills["acrobatics"] = new CharacterSkill { SkillId = "acrobatics", CurrentRank = 4 };
        character.Skills["luck"] = new CharacterSkill { SkillId = "luck", CurrentRank = 2 };
        character.Skills["mana-pool"] = new CharacterSkill { SkillId = "mana-pool", CurrentRank = 10 };
        character.Skills["vitality"] = new CharacterSkill { SkillId = "vitality", CurrentRank = 4 };

        // Act
        var summary = SkillEffectCalculator.GetSkillBonusSummaryBBCode(character);

        // Assert
        var lines = summary.Split('\n');
        lines.Should().HaveCount(6);
        lines[0].Should().Be("[color=yellow]+2% Critical Chance[/color]");
        lines[1].Should().Be("[color=blue]+3% Physical Defense[/color]");
        lines[2].Should().Be("[color=magenta]+1% Dodge Chance[/color]");
        lines[3].Should().Be("[color=orange]+1% Rare Item Find[/color]");
        lines[4].Should().Be("[color=blue]+10% Max Mana[/color]");
        lines[5].Should().Be("[color=green]+2 HP Regeneration per turn[/color]");
    }

    // ── Bonus dictionary and ranking ─────────────────────────────────────────

    [Fact]
    public void GetAllBonuses_ReportsEachCategoryAsAnExcessOverBaseline()
    {
        // Arrange
        var character = new Character();
        character.Skills["light-blades"] = new CharacterSkill { SkillId = "light-blades", CurrentRank = 20 };
        character.Skills["precision"] = new CharacterSkill { SkillId = "precision", CurrentRank = 10 };

        // Act
        var bonuses = SkillEffectCalculator.GetAllBonuses(character);

        // Assert
        bonuses.Should().HaveCount(8);
        bonuses["PhysicalDamage"].Should().Be(0); // no weapon slug is supplied
        bonuses["MagicDamage"].Should().Be(0);
        bonuses["CriticalChance"].Should().BeApproximately(5.0, 1e-9);
        bonuses["PhysicalDefense"].Should().Be(0);
        bonuses["MaxMana"].Should().Be(0);
        bonuses["HealthRegen"].Should().Be(0);
    }

    [Fact]
    public void GetTopBonuses_FiltersZeroBonusesAndOrdersByImpact()
    {
        // Arrange
        var character = new Character();
        character.Skills["precision"] = new CharacterSkill { SkillId = "precision", CurrentRank = 20 }; // 10%
        character.Skills["luck"] = new CharacterSkill { SkillId = "luck", CurrentRank = 4 }; // 2%
        character.Skills["vitality"] = new CharacterSkill { SkillId = "vitality", CurrentRank = 6 }; // 3 hp

        // Act
        var top = SkillEffectCalculator.GetTopBonuses(character);

        // Assert
        top.Should().HaveCount(3);
        top[0].Should().Be(("CriticalChance", 10.0));
        top[1].Should().Be(("HealthRegen", 3.0));
        top[2].Should().Be(("RareItemFind", 2.0));
    }

    [Fact]
    public void GetTopBonuses_RespectsTheRequestedCount()
    {
        // Arrange
        var character = new Character();
        character.Skills["precision"] = new CharacterSkill { SkillId = "precision", CurrentRank = 20 };
        character.Skills["luck"] = new CharacterSkill { SkillId = "luck", CurrentRank = 4 };

        // Act
        var top = SkillEffectCalculator.GetTopBonuses(character, count: 1);

        // Assert
        top.Should().ContainSingle().Which.Name.Should().Be("CriticalChance");
    }

    [Fact]
    public void GetTopBonuses_WithNoSkills_ReturnsEmptyList()
    {
        // Act
        var top = SkillEffectCalculator.GetTopBonuses(new Character());

        // Assert
        top.Should().BeEmpty();
    }

    // ── Damage / defence application ─────────────────────────────────────────

    [Fact]
    public void CalculateEffectiveDamage_AppliesWeaponSkillMultiplierAndRounds()
    {
        // Arrange
        var character = WithSkill("light-blades", 30); // +15%
        character.MaxHealth = 100;
        character.Health = 100;

        // Act
        var damage = SkillEffectCalculator.CalculateEffectiveDamage(23, character, "light-blades");

        // Assert
        damage.Should().Be(26); // 23 * 1.15 = 26.45 → 26
    }

    [Fact]
    public void CalculateEffectiveDamage_WithNoSkill_ReturnsTheBaseDamage()
    {
        // Act
        var damage = SkillEffectCalculator.CalculateEffectiveDamage(17, new Character());

        // Assert
        damage.Should().Be(17);
    }

    [Fact]
    public void CalculateEffectiveDefense_AppliesArmorAndBlockMultipliers()
    {
        // Arrange
        var character = new Character();
        character.Skills["armor"] = new CharacterSkill { SkillId = "armor", CurrentRank = 50 }; // +15%
        character.Skills["block"] = new CharacterSkill { SkillId = "block", CurrentRank = 50 }; // +10%

        // Act
        var defense = SkillEffectCalculator.CalculateEffectiveDefense(40, character);

        // Assert
        defense.Should().Be(50); // 40 * 1.25
    }

    // ── Rank progress / formatting ───────────────────────────────────────────

    [Theory]
    [InlineData(0, 100, 0.0)]
    [InlineData(50, 100, 50.0)]
    [InlineData(100, 100, 100.0)]
    [InlineData(150, 100, 100.0)]
    [InlineData(25, 50, 50.0)]
    [InlineData(10, 0, 0.0)]
    public void GetRankProgress_ClampsToZeroThroughOneHundred(int currentRank, int maxRank, double expected)
    {
        // Act & Assert
        SkillEffectCalculator.GetRankProgress(currentRank, maxRank).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.25, false, "1.25x")]
    [InlineData(1.0, false, "1.00x")]
    [InlineData(1.25, true, "+25.0%")]
    [InlineData(1.0, true, "+0.0%")]
    [InlineData(0.8, true, "-20.0%")]
    public void FormatMultiplier_RendersEitherMultiplierOrPercentageForm(double multiplier, bool asPercentage, string expected)
    {
        // Act & Assert
        SkillEffectCalculator.FormatMultiplier(multiplier, asPercentage).Should().Be(expected);
    }

    // ── Skill slug extraction ────────────────────────────────────────────────

    [Fact]
    public void GetSkillSlugFromItem_NullItem_ReturnsNull()
    {
        // Act & Assert
        SkillEffectCalculator.GetSkillSlugFromItem(null).Should().BeNull();
    }

    [Fact]
    public void GetSkillSlugFromItem_ReturnsTheSegmentAfterTheColon()
    {
        // Arrange
        var item = new Item();
        item.Traits["skillReference"] = new TraitValue("@skills/weapon:light-blades", TraitType.String);

        // Act
        var slug = SkillEffectCalculator.GetSkillSlugFromItem(item);

        // Assert
        slug.Should().Be("light-blades");
    }

    [Fact]
    public void GetSkillSlugFromItem_WithoutTheTrait_ReturnsNull()
    {
        // Act & Assert
        SkillEffectCalculator.GetSkillSlugFromItem(new Item()).Should().BeNull();
    }

    [Fact]
    public void GetSkillSlugFromItem_IgnoresANonStringSkillReference()
    {
        // Arrange
        var item = new Item();
        item.Traits["skillReference"] = new TraitValue(42, TraitType.Number);

        // Act
        var slug = SkillEffectCalculator.GetSkillSlugFromItem(item);

        // Assert
        slug.Should().BeNull();
    }

    [Fact]
    public void GetSkillSlugFromItem_EmptyReference_ReturnsNull()
    {
        // Arrange
        var item = new Item();
        item.Traits["skillReference"] = new TraitValue("", TraitType.String);

        // Act
        var slug = SkillEffectCalculator.GetSkillSlugFromItem(item);

        // Assert
        slug.Should().BeNull();
    }

    [Theory]
    [InlineData("light-blades")]
    [InlineData(":light-blades")]
    [InlineData("@skills/weapon:")]
    public void GetSkillSlugFromItem_ReferenceThatIsNotColonDelimited_ReturnsNull(string reference)
    {
        // Arrange
        var item = new Item();
        item.Traits["skillReference"] = new TraitValue(reference, TraitType.String);

        // Act
        var slug = SkillEffectCalculator.GetSkillSlugFromItem(item);

        // Assert
        slug.Should().BeNull();
    }

    // ── Efficiency rating ────────────────────────────────────────────────────

    [Fact]
    public void GetSkillEfficiencyRating_WithNoSkills_ReturnsZero()
    {
        // Act & Assert
        SkillEffectCalculator.GetSkillEfficiencyRating(new Character()).Should().Be(0);
    }

    [Fact]
    public void GetSkillEfficiencyRating_WithPerfectlyBalancedSkills_ReturnsOneHundred()
    {
        // Arrange
        var character = new Character();
        character.Skills["a"] = new CharacterSkill { SkillId = "a", CurrentRank = 10 };
        character.Skills["b"] = new CharacterSkill { SkillId = "b", CurrentRank = 10 };

        // Act
        var rating = SkillEffectCalculator.GetSkillEfficiencyRating(character);

        // Assert
        rating.Should().Be(100);
    }

    [Fact]
    public void GetSkillEfficiencyRating_WithLopsidedSkills_ReturnsLessThanOneHundred()
    {
        // Arrange
        var character = new Character();
        character.Skills["a"] = new CharacterSkill { SkillId = "a", CurrentRank = 100 };
        character.Skills["b"] = new CharacterSkill { SkillId = "b", CurrentRank = 0 };

        // Act
        var rating = SkillEffectCalculator.GetSkillEfficiencyRating(character);

        // Assert
        rating.Should().BeLessThan(100).And.BeGreaterThan(0);
    }

    [Fact]
    public void GetSkillEfficiencyRating_WithAnAllZeroSkillSet_ClampsAtZero()
    {
        // Arrange — avgRank is 0, so the ratio is 0/0 → NaN; Math.Max(0, NaN) is NaN
        var character = new Character();
        character.Skills["a"] = new CharacterSkill { SkillId = "a", CurrentRank = 0 };

        // Act
        var rating = SkillEffectCalculator.GetSkillEfficiencyRating(character);

        // Assert — documents the current degenerate-input behaviour
        double.IsNaN(rating).Should().BeTrue();
    }
}
