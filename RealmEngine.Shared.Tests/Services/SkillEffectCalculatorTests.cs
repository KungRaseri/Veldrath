using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Services;

[Trait("Category", "Utilities")]
public class SkillEffectCalculatorTests
{
    private static Character MakeCharacter(params (string slug, int rank)[] skills)
    {
        var character = new Character
        {
            Name = "Tester",
            ClassName = "Warrior",
        };
        foreach (var (slug, rank) in skills)
        {
            character.Skills[slug] = new CharacterSkill { SkillId = slug, CurrentRank = rank };
        }
        return character;
    }

    // GetPhysicalDamageMultiplier

    [Fact]
    public void GetPhysicalDamageMultiplier_NoSkill_ReturnsOne()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetPhysicalDamageMultiplier(character).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetPhysicalDamageMultiplier_UnknownSkillSlug_ReturnsOne()
    {
        var character = MakeCharacter(("swords", 50));
        SkillEffectCalculator.GetPhysicalDamageMultiplier(character, "axes").Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetPhysicalDamageMultiplier_Rank100_ReturnsPlusHalf()
    {
        var character = MakeCharacter(("swords", 100));
        // 1.0 + (100 * 0.005) = 1.5
        SkillEffectCalculator.GetPhysicalDamageMultiplier(character, "swords").Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void GetPhysicalDamageMultiplier_Rank50_ReturnsCorrectBonus()
    {
        var character = MakeCharacter(("swords", 50));
        // 1.0 + (50 * 0.005) = 1.25
        SkillEffectCalculator.GetPhysicalDamageMultiplier(character, "swords").Should().BeApproximately(1.25, 0.001);
    }

    // GetMagicDamageMultiplier

    [Fact]
    public void GetMagicDamageMultiplier_NoSkill_ReturnsOne()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetMagicDamageMultiplier(character).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetMagicDamageMultiplier_Rank100_ReturnsCorrectBonus()
    {
        var character = MakeCharacter(("arcane", 100));
        // 1.0 + (100 * 0.008) = 1.8
        SkillEffectCalculator.GetMagicDamageMultiplier(character, "arcane").Should().BeApproximately(1.8, 0.001);
    }

    // GetCriticalChanceBonus

    [Fact]
    public void GetCriticalChanceBonus_NoSkill_ReturnsZero()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetCriticalChanceBonus(character).Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void GetCriticalChanceBonus_PrecisionRank100_ReturnsFiftyPercent()
    {
        var character = MakeCharacter(("precision", 100));
        // 100 * 0.5 = 50
        SkillEffectCalculator.GetCriticalChanceBonus(character).Should().BeApproximately(50.0, 0.001);
    }

    // GetPhysicalDefenseMultiplier

    [Fact]
    public void GetPhysicalDefenseMultiplier_NoSkill_ReturnsOne()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetPhysicalDefenseMultiplier(character).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetPhysicalDefenseMultiplier_ArmorRank100_ReturnsCorrectMultiplier()
    {
        var character = MakeCharacter(("armor", 100));
        // 1.0 + (100 * 0.003) = 1.3
        SkillEffectCalculator.GetPhysicalDefenseMultiplier(character).Should().BeApproximately(1.3, 0.001);
    }

    [Fact]
    public void GetPhysicalDefenseMultiplier_ArmorAndBlock_StacksBoth()
    {
        var character = MakeCharacter(("armor", 100), ("block", 100));
        // 1.0 + (100 * 0.003) + (100 * 0.002) = 1.5
        SkillEffectCalculator.GetPhysicalDefenseMultiplier(character).Should().BeApproximately(1.5, 0.001);
    }

    // GetDodgeChanceBonus

    [Fact]
    public void GetDodgeChanceBonus_NoSkill_ReturnsZero()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetDodgeChanceBonus(character).Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void GetDodgeChanceBonus_AcrobaticsRank100_ReturnsThirtyPercent()
    {
        var character = MakeCharacter(("acrobatics", 100));
        // 100 * 0.3 = 30
        SkillEffectCalculator.GetDodgeChanceBonus(character).Should().BeApproximately(30.0, 0.001);
    }

    [Fact]
    public void GetDodgeChanceBonus_LightArmorRank100_ReturnsTenPercent()
    {
        var character = MakeCharacter(("light-armor", 100));
        // 100 * 0.1 = 10
        SkillEffectCalculator.GetDodgeChanceBonus(character).Should().BeApproximately(10.0, 0.001);
    }

    [Fact]
    public void GetDodgeChanceBonus_BothSkills_StacksCorrectly()
    {
        var character = MakeCharacter(("acrobatics", 100), ("light-armor", 100));
        // 30 + 10 = 40
        SkillEffectCalculator.GetDodgeChanceBonus(character).Should().BeApproximately(40.0, 0.001);
    }

    // GetRareItemFindBonus

    [Fact]
    public void GetRareItemFindBonus_NoSkill_ReturnsZero()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetRareItemFindBonus(character).Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void GetRareItemFindBonus_LuckRank100_ReturnsFiftyPercent()
    {
        var character = MakeCharacter(("luck", 100));
        // 100 * 0.5 = 50
        SkillEffectCalculator.GetRareItemFindBonus(character).Should().BeApproximately(50.0, 0.001);
    }

    // GetMaxManaMultiplier

    [Fact]
    public void GetMaxManaMultiplier_NoSkill_ReturnsOne()
    {
        var character = MakeCharacter();
        SkillEffectCalculator.GetMaxManaMultiplier(character).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetMaxManaMultiplier_ManaPoolRank100_ReturnsDouble()
    {
        var character = MakeCharacter(("mana-pool", 100));
        // 1.0 + (100 * 0.01) = 2.0
        SkillEffectCalculator.GetMaxManaMultiplier(character).Should().BeApproximately(2.0, 0.001);
    }
}
