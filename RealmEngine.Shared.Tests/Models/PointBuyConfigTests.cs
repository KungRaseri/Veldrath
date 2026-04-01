using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
public class PointBuyConfigTests
{
    private readonly PointBuyConfig _config = new();

    // GetCost
    [Theory]
    [InlineData(8,  0)]
    [InlineData(9,  1)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(14, 7)]
    [InlineData(15, 9)]
    public void GetCost_ValidValue_ReturnsExpectedCost(int value, int expectedCost)
    {
        _config.GetCost(value).Should().Be(expectedCost);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetCost_OutOfRangeValue_ReturnsMinusOne(int value)
    {
        _config.GetCost(value).Should().Be(-1);
    }

    // CalculateTotalCost
    [Fact]
    public void CalculateTotalCost_AllAtMin_ReturnsZero()
    {
        var allocs = StatDict(8, 8, 8, 8, 8, 8);
        _config.CalculateTotalCost(allocs).Should().Be(0);
    }

    [Fact]
    public void CalculateTotalCost_AllAtMax_Returns54()
    {
        var allocs = StatDict(15, 15, 15, 15, 15, 15);
        _config.CalculateTotalCost(allocs).Should().Be(54); // 9 × 6
    }

    [Fact]
    public void CalculateTotalCost_MixedValues_ReturnsCorrectSum()
    {
        // 15(9) + 14(7) + 13(5) + 10(2) + 8(0) + 8(0) = 23
        var allocs = new Dictionary<string, int>
        {
            ["Strength"]     = 15,
            ["Dexterity"]    = 14,
            ["Constitution"] = 13,
            ["Intelligence"] = 10,
            ["Wisdom"]       = 8,
            ["Charisma"]     = 8,
        };
        _config.CalculateTotalCost(allocs).Should().Be(23);
    }

    [Fact]
    public void CalculateTotalCost_OutOfRangeValue_ReturnsMaxInt()
    {
        var allocs = StatDict(16, 8, 8, 8, 8, 8);
        _config.CalculateTotalCost(allocs).Should().Be(int.MaxValue);
    }

    // IsValid
    [Fact]
    public void IsValid_ExactlyOnBudget_ReturnsTrue()
    {
        // 15(9) + 14(7) + 11(3) + 10(2) + 10(2) + 10(2) = 25  ≤ 27
        var allocs = new Dictionary<string, int>
        {
            ["Strength"]     = 15,
            ["Dexterity"]    = 14,
            ["Constitution"] = 11,
            ["Intelligence"] = 10,
            ["Wisdom"]       = 10,
            ["Charisma"]     = 10,
        };
        _config.IsValid(allocs).Should().BeTrue();
    }

    [Fact]
    public void IsValid_AllAtMin_ReturnsTrue()
    {
        _config.IsValid(StatDict(8, 8, 8, 8, 8, 8)).Should().BeTrue();
    }

    [Fact]
    public void IsValid_OverBudget_ReturnsFalse()
    {
        _config.IsValid(StatDict(15, 15, 15, 15, 15, 15)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ValueBelowMin_ReturnsFalse()
    {
        _config.IsValid(StatDict(7, 10, 10, 10, 10, 10)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ValueAboveMax_ReturnsFalse()
    {
        _config.IsValid(StatDict(16, 10, 10, 10, 10, 10)).Should().BeFalse();
    }

    private static Dictionary<string, int> StatDict(int str, int dex, int con, int intel, int wis, int cha) =>
        new()
        {
            ["Strength"]     = str,
            ["Dexterity"]    = dex,
            ["Constitution"] = con,
            ["Intelligence"] = intel,
            ["Wisdom"]       = wis,
            ["Charisma"]     = cha,
        };
}
