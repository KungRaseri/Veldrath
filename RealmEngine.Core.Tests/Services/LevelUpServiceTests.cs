using FluentAssertions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
/// <summary>
/// Tests for LevelUpService domain logic.
/// </summary>
public class LevelUpServiceTests
{
    private readonly LevelUpService _service;

    public LevelUpServiceTests()
    {
        _service = new LevelUpService();
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(5, 500)]
    [InlineData(10, 1000)]
    public void CalculateExperienceForLevel_Should_Return_Correct_Amount(int level, int expectedXP)
    {
        // Act
        var result = _service.CalculateExperienceForLevel(level);

        // Assert
        result.Should().Be(expectedXP);
    }

    [Theory]
    [InlineData(1, 3)] // Base 3
    [InlineData(2, 3)] // Base 3
    [InlineData(5, 5)] // Base 3 + Bonus 2
    [InlineData(10, 5)] // Base 3 + Bonus 2
    [InlineData(15, 5)] // Base 3 + Bonus 2
    public void CalculateAttributePointsForLevel_Should_Give_Bonus_Every_5_Levels(int level, int expectedPoints)
    {
        // Act
        var result = _service.CalculateAttributePointsForLevel(level);

        // Assert
        result.Should().Be(expectedPoints);
    }

    [Theory]
    [InlineData(1, 1)] // Base 1
    [InlineData(2, 1)] // Base 1
    [InlineData(5, 2)] // Base 1 + Bonus 1
    [InlineData(10, 2)] // Base 1 + Bonus 1
    public void CalculateSkillPointsForLevel_Should_Give_Bonus_Every_5_Levels(int level, int expectedPoints)
    {
        // Act
        var result = _service.CalculateSkillPointsForLevel(level);

        // Assert
        result.Should().Be(expectedPoints);
    }

    [Fact]
    public void PreviewLevelUp_Should_Calculate_Correct_Point_Gains()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Level = 1,
            Constitution = 15,
            Wisdom = 12,
            MaxHealth = 150,
            MaxMana = 60
        };

        // Act
        var preview = _service.PreviewLevelUp(character, 5);

        // Assert
        preview.CurrentLevel.Should().Be(1);
        preview.TargetLevel.Should().Be(5);
        // Levels 2, 3, 4: 3 points each = 9
        // Level 5: 3 + 2 bonus = 5
        // Total: 14 attribute points
        preview.AttributePointsGained.Should().Be(14);
        // Levels 2, 3, 4: 1 point each = 3
        // Level 5: 1 + 1 bonus = 2
        // Total: 5 skill points
        preview.SkillPointsGained.Should().Be(5);
    }

    [Fact]
    public void ValidateAttributeAllocation_Should_Reject_Empty_Dictionary()
    {
        // Arrange
        var character = new Character { UnspentAttributePoints = 5 };
        var allocation = new Dictionary<string, int>();

        // Act
        var (isValid, errorMessage) = _service.ValidateAttributeAllocation(character, allocation);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("No attributes");
    }

    [Fact]
    public void ValidateAttributeAllocation_Should_Reject_Insufficient_Points()
    {
        // Arrange
        var character = new Character { UnspentAttributePoints = 2 };
        var allocation = new Dictionary<string, int>
        {
            ["Strength"] = 5
        };

        // Act
        var (isValid, errorMessage) = _service.ValidateAttributeAllocation(character, allocation);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("Not enough points");
    }

    [Fact]
    public void ValidateAttributeAllocation_Should_Reject_Negative_Values()
    {
        // Arrange
        var character = new Character { UnspentAttributePoints = 10 };
        var allocation = new Dictionary<string, int>
        {
            ["Strength"] = -1
        };

        // Act
        var (isValid, errorMessage) = _service.ValidateAttributeAllocation(character, allocation);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("negative");
    }

    [Fact]
    public void ValidateAttributeAllocation_Should_Reject_Invalid_Attribute_Names()
    {
        // Arrange
        var character = new Character { UnspentAttributePoints = 5 };
        var allocation = new Dictionary<string, int>
        {
            ["InvalidAttribute"] = 3
        };

        // Act
        var (isValid, errorMessage) = _service.ValidateAttributeAllocation(character, allocation);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("Invalid attributes");
    }

    [Fact]
    public void ValidateAttributeAllocation_Should_Accept_Valid_Allocation()
    {
        // Arrange
        var character = new Character { UnspentAttributePoints = 5 };
        var allocation = new Dictionary<string, int>
        {
            ["Strength"] = 3,
            ["Dexterity"] = 2
        };

        // Act
        var (isValid, errorMessage) = _service.ValidateAttributeAllocation(character, allocation);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableSkills_Should_Filter_By_Level()
    {
        // Arrange
        var character = new Character
        {
            Level = 3,
            Skills = new Dictionary<string, CharacterSkill>()
        };

        // Act
        var skills = _service.GetAvailableSkills(character);

        // Assert
        skills.Should().NotBeEmpty();
        skills.All(s => s.RequiredLevel <= 3).Should().BeTrue();
    }

    [Fact]
    public void GetAvailableSkills_Should_Exclude_Maxed_Skills()
    {
        // Arrange
        var character = new Character
        {
            Level = 10,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Power Attack"] = new CharacterSkill
                {
                    SkillId = "Power Attack",
                    CurrentRank = 5 // Max rank
                }
            }
        };

        // Act
        var skills = _service.GetAvailableSkills(character);

        // Assert
        skills.Should().NotContain(s => s.Name == "Power Attack");
    }

    [Fact]
    public void CalculateLevelsGainableFromExperience_Should_Calculate_Multiple_Levels()
    {
        // Arrange - Level 1 needs 100 XP, Level 2 needs 200 XP
        int currentLevel = 1;
        int currentExperience = 350; // Enough for levels 2 and 3

        // Act
        var levelsGained = _service.CalculateLevelsGainableFromExperience(currentLevel, currentExperience);

        // Assert
        levelsGained.Should().Be(2); // Can gain 2 levels (100 + 200 = 300, 50 XP remaining)
    }

    [Fact]
    public void CalculateTotalExperienceForLevel_Should_Sum_All_Previous_Levels()
    {
        // Arrange - Level 1 needs 100, Level 2 needs 200, Level 3 needs 300
        int targetLevel = 4;

        // Act
        var totalXP = _service.CalculateTotalExperienceForLevel(targetLevel);

        // Assert
        totalXP.Should().Be(600); // 100 + 200 + 300
    }
}
