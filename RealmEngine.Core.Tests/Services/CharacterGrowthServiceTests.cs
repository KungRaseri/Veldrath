using FluentAssertions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for CharacterGrowthService stat calculations and default configuration.
/// Uses NullGameConfigService to test fallback/default behaviour.
/// </summary>
public class CharacterGrowthServiceTests
{
    private readonly CharacterGrowthService _service;

    public CharacterGrowthServiceTests()
    {
        // NullGameConfigService always returns null -> service falls back to built-in defaults.
        _service = new CharacterGrowthService(new NullGameConfigService());
    }

    [Fact]
    public void GetClassMultipliers_WithInvalidClassReference_ShouldReturnNull()
    {
        // Act
        var multipliers = _service.GetClassMultipliers("@classes/invalid:NonExistent");

        // Assert
        multipliers.Should().BeNull();
    }

    [Fact]
    public void CalculateDerivedStat_ShouldReturnNonNegativeValue()
    {
        // Act - default config has no derived stat formulas, result should be baseValue
        var result = _service.CalculateDerivedStat("health", 50, 15, 1, 1.0);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ApplySoftCap_WithValueBelowCap_ShouldReturnUnchanged()
    {
        // Arrange
        var lowValue = 10.0;

        // Act
        var result = _service.ApplySoftCap("strength", lowValue);

        // Assert - value below cap passes through unchanged
        result.Should().Be(lowValue);
    }

    [Fact]
    public void ApplyHardCap_WithValueBelowCap_ShouldReturnUnchanged()
    {
        // Arrange
        var lowValue = 10.0;

        // Act
        var result = _service.ApplyHardCap("strength", lowValue);

        // Assert
        result.Should().Be(lowValue);
    }

    [Fact]
    public void CalculateRespecCost_AtLevelOne_ShouldReturnPositiveValue()
    {
        // Act
        var cost = _service.CalculateRespecCost(1);

        // Assert
        cost.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateRespecCost_ShouldIncreaseWithLevel()
    {
        // Act
        var costLevel1 = _service.CalculateRespecCost(1);
        var costLevel10 = _service.CalculateRespecCost(10);
        var costLevel50 = _service.CalculateRespecCost(50);

        // Assert
        costLevel10.Should().BeGreaterThanOrEqualTo(costLevel1);
        costLevel50.Should().BeGreaterThanOrEqualTo(costLevel10);
    }

    [Fact]
    public void ClearCache_ShouldNotThrow()
    {
        // Arrange
        _service.GetClassMultipliers("@classes/warriors:fighter");

        // Act
        var act = () => _service.ClearCache();

        // Assert
        act.Should().NotThrow();
    }
}
