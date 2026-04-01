using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
public class SpeciesTests
{
    private static Species MakeSpecies(
        int str = 0, int dex = 0, int con = 0,
        int intel = 0, int wis = 0, int cha = 0) =>
        new()
        {
            Slug         = "test-species",
            DisplayName  = "Test Species",
            BonusStrength     = str,
            BonusDexterity    = dex,
            BonusConstitution = con,
            BonusIntelligence = intel,
            BonusWisdom       = wis,
            BonusCharisma     = cha,
        };

    [Fact]
    public void ApplyBonuses_AllZero_DoesNotChangeStats()
    {
        var species   = MakeSpecies();
        var character = new Character { Strength = 10, Dexterity = 10, Constitution = 10,
                                        Intelligence = 10, Wisdom = 10, Charisma = 10 };

        species.ApplyBonuses(character);

        character.Strength.Should().Be(10);
        character.Dexterity.Should().Be(10);
        character.Constitution.Should().Be(10);
        character.Intelligence.Should().Be(10);
        character.Wisdom.Should().Be(10);
        character.Charisma.Should().Be(10);
    }

    [Fact]
    public void ApplyBonuses_NonZeroBonuses_AddsToAllStats()
    {
        var species   = MakeSpecies(str: 2, dex: 1, con: 0, intel: 0, wis: 1, cha: 0);
        var character = new Character { Strength = 10, Dexterity = 12, Constitution = 8,
                                        Intelligence = 14, Wisdom = 10, Charisma = 11 };

        species.ApplyBonuses(character);

        character.Strength.Should().Be(12);
        character.Dexterity.Should().Be(13);
        character.Constitution.Should().Be(8);
        character.Intelligence.Should().Be(14);
        character.Wisdom.Should().Be(11);
        character.Charisma.Should().Be(11);
    }

    [Fact]
    public void ApplyBonuses_AppliedTwice_StacksAdditively()
    {
        var species   = MakeSpecies(str: 2);
        var character = new Character { Strength = 10 };

        species.ApplyBonuses(character);
        species.ApplyBonuses(character);

        character.Strength.Should().Be(14);
    }

    [Fact]
    public void ApplyBonuses_AllSixBonuses_AllApplied()
    {
        var species   = MakeSpecies(str: 1, dex: 2, con: 3, intel: 4, wis: 5, cha: 6);
        var character = new Character
        {
            Strength = 8, Dexterity = 8, Constitution = 8,
            Intelligence = 8, Wisdom = 8, Charisma = 8,
        };

        species.ApplyBonuses(character);

        character.Strength.Should().Be(9);
        character.Dexterity.Should().Be(10);
        character.Constitution.Should().Be(11);
        character.Intelligence.Should().Be(12);
        character.Wisdom.Should().Be(13);
        character.Charisma.Should().Be(14);
    }
}
