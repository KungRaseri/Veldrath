using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the character background identifier and its attribute-bonus
/// application, plus the reputation ladder that drives price discounts and hostility.
/// </summary>
[Trait("Category", "Unit")]
public class BackgroundAndReputationTests
{
    // ── Background ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Strength", "backgrounds/strength:blacksmith")]
    [InlineData("Dexterity", "backgrounds/dexterity:blacksmith")]
    [InlineData("WISDOM", "backgrounds/wisdom:blacksmith")]
    public void GetBackgroundId_ComposesLowercaseCategoryAndSlug(string primaryAttribute, string expected)
    {
        // Arrange
        var background = new Background { Slug = "blacksmith", PrimaryAttribute = primaryAttribute };

        // Act
        var id = background.GetBackgroundId();

        // Assert
        id.Should().Be(expected);
    }

    [Fact]
    public void ApplyBonuses_AddsThePrimaryAndSecondaryAttributeBonuses()
    {
        // Arrange
        var background = new Background
        {
            PrimaryAttribute = "Strength",
            PrimaryBonus = 2,
            SecondaryAttribute = "Constitution",
            SecondaryBonus = 1,
        };
        var character = new Character();

        // Act
        background.ApplyBonuses(character);

        // Assert
        character.Strength.Should().Be(12);
        character.Constitution.Should().Be(11);
        character.Dexterity.Should().Be(10);
    }

    [Theory]
    [InlineData("strength")]
    [InlineData("dexterity")]
    [InlineData("constitution")]
    [InlineData("intelligence")]
    [InlineData("wisdom")]
    [InlineData("charisma")]
    public void ApplyBonuses_HandlesEverySupportedAttributeName(string attribute)
    {
        // Arrange
        var background = new Background
        {
            PrimaryAttribute = attribute,
            PrimaryBonus = 3,
            SecondaryAttribute = "unknown-attribute",
            SecondaryBonus = 99,
        };
        var character = new Character();
        var before = new Dictionary<string, int>
        {
            ["Strength"] = character.Strength,
            ["Dexterity"] = character.Dexterity,
            ["Constitution"] = character.Constitution,
            ["Intelligence"] = character.Intelligence,
            ["Wisdom"] = character.Wisdom,
            ["Charisma"] = character.Charisma,
        };

        // Act
        background.ApplyBonuses(character);

        // Assert
        var after = new Dictionary<string, int>
        {
            ["Strength"] = character.Strength,
            ["Dexterity"] = character.Dexterity,
            ["Constitution"] = character.Constitution,
            ["Intelligence"] = character.Intelligence,
            ["Wisdom"] = character.Wisdom,
            ["Charisma"] = character.Charisma,
        };

        foreach (var (name, value) in after)
        {
            var expected = name.Equals(attribute, StringComparison.OrdinalIgnoreCase)
                ? before[name] + 3
                : before[name];
            value.Should().Be(expected, because: $"{name} should only change for the primary attribute");
        }
    }

    [Fact]
    public void ApplyBonuses_IgnoresUnknownAttributeNames()
    {
        // Arrange
        var background = new Background
        {
            PrimaryAttribute = "luck",
            PrimaryBonus = 5,
            SecondaryAttribute = "",
            SecondaryBonus = 5,
        };
        var character = new Character();

        // Act
        background.ApplyBonuses(character);

        // Assert
        character.Strength.Should().Be(10);
        character.Charisma.Should().Be(10);
    }

    [Fact]
    public void Background_DefaultsToEmptyStringsAndNoBonuses()
    {
        // Act
        var background = new Background();

        // Assert
        background.Slug.Should().BeEmpty();
        background.Name.Should().BeEmpty();
        background.PrimaryBonus.Should().Be(0);
        background.SecondaryBonus.Should().Be(0);
        background.RarityWeight.Should().Be(0);
        background.RecommendedLocationTypes.Should().BeEmpty();
    }

    // ── ReputationStanding ladder ───────────────────────────────────────────

    [Theory]
    [InlineData(12000, ReputationLevel.Exalted)]
    [InlineData(11999, ReputationLevel.Revered)]
    [InlineData(6000, ReputationLevel.Revered)]
    [InlineData(5999, ReputationLevel.Honored)]
    [InlineData(3000, ReputationLevel.Honored)]
    [InlineData(2999, ReputationLevel.Friendly)]
    [InlineData(500, ReputationLevel.Friendly)]
    [InlineData(499, ReputationLevel.Neutral)]
    [InlineData(-500, ReputationLevel.Neutral)]
    [InlineData(-501, ReputationLevel.Unfriendly)]
    [InlineData(-3000, ReputationLevel.Unfriendly)]
    [InlineData(-3001, ReputationLevel.Hostile)]
    [InlineData(-9000, ReputationLevel.Hostile)]
    public void Level_MapsPointsToTheCorrectBand(int points, ReputationLevel expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.Level.Should().Be(expected);
    }

    [Fact]
    public void NextLevel_IsTheNextRungUpTheLadder()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 600 };

        // Act & Assert
        standing.Level.Should().Be(ReputationLevel.Friendly);
        standing.NextLevel.Should().Be(ReputationLevel.Honored);
    }

    [Fact]
    public void NextLevel_FromHostile_IsUnfriendly()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = -5000 };

        // Act & Assert
        standing.NextLevel.Should().Be(ReputationLevel.Unfriendly);
    }

    [Fact]
    public void NextLevel_AtExalted_IsNull()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 12000 };

        // Act & Assert
        standing.NextLevel.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 500)]      // Neutral → Friendly needs 500
    [InlineData(600, 2400)]   // Friendly → Honored (3000)
    [InlineData(6000, 6000)]  // Revered → Exalted (12000)
    [InlineData(-5000, 2000)] // Hostile → Unfriendly (-3000)
    public void PointsToNextLevel_IsTheGapToTheNextThreshold(int points, int expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.PointsToNextLevel.Should().Be(expected);
    }

    [Fact]
    public void PointsToNextLevel_AtExalted_IsZero()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 12000 };

        // Act & Assert
        standing.PointsToNextLevel.Should().Be(0);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(-400, true)]    // still Neutral
    [InlineData(-1000, false)]  // Unfriendly refuses quests
    public void CanAcceptQuests_RequiresNeutralOrBetter(int points, bool expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.CanAcceptQuests.Should().Be(expected);
    }

    [Theory]
    [InlineData(-2000, true)]  // Unfriendly still trades
    [InlineData(0, true)]
    [InlineData(-4000, false)] // Hostile refuses
    public void CanTrade_RequiresUnfriendlyOrBetter(int points, bool expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.CanTrade.Should().Be(expected);
    }

    [Theory]
    [InlineData(-4000, true)]
    [InlineData(-2000, false)]
    [InlineData(0, false)]
    public void IsHostile_IsTrueOnlyInTheHostileBand(int points, bool expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.IsHostile.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0.0)]        // Neutral
    [InlineData(-4000, 0.0)]    // Hostile
    [InlineData(600, 0.05)]     // Friendly
    [InlineData(3000, 0.10)]    // Honored
    [InlineData(6000, 0.20)]    // Revered
    [InlineData(12000, 0.30)]   // Exalted
    public void GetPriceDiscount_ScalesWithTheFactionStanding(int points, double expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.GetPriceDiscount().Should().Be(expected);
    }

    [Fact]
    public void AddReputation_IncreasesThePoints()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 100 };

        // Act
        standing.AddReputation(250);

        // Assert
        standing.ReputationPoints.Should().Be(350);
    }

    [Fact]
    public void AddReputation_ClampsAtTheExaltedCap()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 11900 };

        // Act
        standing.AddReputation(20000);

        // Assert
        standing.ReputationPoints.Should().Be(21000);
    }

    [Fact]
    public void LoseReputation_DecreasesThePoints()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = 300 };

        // Act
        standing.LoseReputation(100);

        // Assert
        standing.ReputationPoints.Should().Be(200);
    }

    [Fact]
    public void LoseReputation_FloorsAtDeepHostile()
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = -8000 };

        // Act
        standing.LoseReputation(5000);

        // Assert
        standing.ReputationPoints.Should().Be(-9000);
    }

    [Fact]
    public void ReputationStanding_DefaultsToNeutralWithNoPoints()
    {
        // Act
        var standing = new ReputationStanding();

        // Assert
        standing.FactionId.Should().BeEmpty();
        standing.ReputationPoints.Should().Be(0);
        standing.Level.Should().Be(ReputationLevel.Neutral);
        standing.IsHostile.Should().BeFalse();
    }
}
