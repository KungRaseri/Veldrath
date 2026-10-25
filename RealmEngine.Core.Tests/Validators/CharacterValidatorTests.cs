using FluentValidation.TestHelper;
using RealmEngine.Core.Validators;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Validators;

[Trait("Category", "Unit")]
public class CharacterValidatorTests
{
    private readonly CharacterValidator _validator = new();

    [Fact]
    public void Should_Pass_For_Valid_Character()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 50,
            MaxHealth = 100,
            Mana = 30,
            MaxMana = 50,
            Experience = 25,
            Gold = 100
        };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")] // Too short (minimum is 2)
    [InlineData("ThisNameIsTooLongForACharacter")] // Too long
    public void Should_Fail_For_Invalid_Name(string invalidName)
    {
        // Arrange
        var character = new Character { Name = invalidName };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Should_Fail_For_Name_With_Numbers()
    {
        // Arrange
        var character = new Character { Name = "Hero123" };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Should_Fail_For_Invalid_Level(int invalidLevel)
    {
        // Arrange
        var character = new Character { Name = "Hero", Level = invalidLevel };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Level);
    }

    [Fact]
    public void Should_Fail_When_Health_Exceeds_MaxHealth()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Health = 150,
            MaxHealth = 100
        };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Health);
    }

    [Fact]
    public void Should_Fail_For_Negative_Gold()
    {
        // Arrange
        var character = new Character
        {
            Name = "Hero",
            Gold = -10
        };

        // Act
        var result = _validator.TestValidate(character);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Gold);
    }
}
