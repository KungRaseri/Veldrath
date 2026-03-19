using FluentAssertions;
using RealmEngine.Core.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class DiceRollerTests
{
    // RollDiceString — null / empty / whitespace / invalid

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RollDiceString_NullOrWhitespace_ReturnsZero(string? input)
    {
        DiceRoller.RollDiceString(input!).Should().Be(0);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("d6")]
    [InlineData("6")]
    [InlineData("2x6")]
    public void RollDiceString_InvalidFormat_ReturnsZero(string input)
    {
        DiceRoller.RollDiceString(input).Should().Be(0);
    }

    // RollDiceString — valid notation

    [Fact]
    public void RollDiceString_OneDSix_ReturnsBetweenOneAndSix()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("1d6");
            result.Should().BeInRange(1, 6);
        }
    }

    [Fact]
    public void RollDiceString_TwoDSix_ReturnsBetweenTwoAndTwelve()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("2d6");
            result.Should().BeInRange(2, 12);
        }
    }

    [Fact]
    public void RollDiceString_WithPositiveModifier_AddsBonusToResult()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("1d6+3");
            result.Should().BeInRange(4, 9);
        }
    }

    [Fact]
    public void RollDiceString_WithNegativeModifier_SubtractsBonusFromResult()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("1d6-3");
            // Min roll is 1; 1 - 3 = -2, but clamped to 0 via Math.Max
            result.Should().BeInRange(0, 3);
        }
    }

    [Fact]
    public void RollDiceString_LargeNegativeModifier_NeverReturnsBelowZero()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("1d4-10");
            result.Should().Be(0);
        }
    }

    [Fact]
    public void RollDiceString_OneDTwenty_ReturnsBetweenOneAndTwenty()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollDiceString("1d20");
            result.Should().BeInRange(1, 20);
        }
    }

    // Roll(int sides)

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(20)]
    public void Roll_Sides_ReturnsBetweenOneAndSides(int sides)
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.Roll(sides);
            result.Should().BeInRange(1, sides);
        }
    }

    // Roll(int count, int sides)

    [Fact]
    public void Roll_CountAndSides_ReturnsBetweenCountAndCountTimesSides()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.Roll(3, 6);
            result.Should().BeInRange(3, 18);
        }
    }

    [Fact]
    public void Roll_OneDice_SameAsRollSides()
    {
        for (int i = 0; i < 100; i++)
        {
            var result = DiceRoller.Roll(1, 6);
            result.Should().BeInRange(1, 6);
        }
    }
}
