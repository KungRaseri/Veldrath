using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for CharacterGrowthService configuration loading and stat calculations.
/// </summary>
public class CharacterGrowthServiceTests
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly CharacterGrowthService _service;

    public CharacterGrowthServiceTests()
    {
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");

        _dataCache = new GameDataCache(dataPath, null);
        _dataCache.LoadAllData();

        _referenceResolver = new ReferenceResolverService(_dataCache, NullLogger<ReferenceResolverService>.Instance);
        _service = new CharacterGrowthService(_dataCache, _referenceResolver);
    }

    [Fact]
    public void LoadConfig_ShouldLoadGrowthStatsConfiguration()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.Should().NotBeNull();
        config.Version.Should().NotBeNullOrEmpty();
        config.DerivedStats.Should().NotBeNull();
        config.StatCaps.Should().NotBeNull();
        config.ClassGrowthMultipliers.Should().NotBeNull();
    }

    [Fact]
    public void LoadConfig_ShouldCacheConfiguration()
    {
        // Act
        var config1 = _service.LoadConfig();
        var config2 = _service.LoadConfig();

        // Assert
        config1.Should().BeSameAs(config2, "configuration should be cached");
    }

    [Fact]
    public void LoadConfig_ShouldLoadClassGrowthMultipliers()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.ClassGrowthMultipliers.Should().NotBeEmpty();
        config.ClassGrowthMultipliers.Should().Contain(m => m.ClassRef.Contains("@classes/"));
    }

    [Fact]
    public void LoadConfig_ShouldLoadDerivedStatsFormulas()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.DerivedStats.Should().NotBeNull();
        config.DerivedStats.Should().NotBeEmpty();
        config.DerivedStats.Should().ContainKey("health");
        config.DerivedStats["health"].Formula.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadConfig_ShouldLoadStatCaps()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.StatCaps.Should().NotBeNull();
        config.StatCaps.SoftCaps.Should().NotBeNull();
        config.StatCaps.HardCaps.Should().NotBeNull();
        config.StatCaps.SoftCaps.StatLimits.Should().NotBeEmpty();
        config.StatCaps.HardCaps.StatLimits.Should().NotBeEmpty();
    }

    [Fact]
    public void GetClassMultipliers_WithValidClassReference_ShouldReturnMultipliers()
    {
        // Arrange
        var config = _service.LoadConfig();
        var validClassRef = config.ClassGrowthMultipliers.FirstOrDefault()?.ClassRef;
        
        if (validClassRef == null)
        {
            // Skip test if no class multipliers exist
            return;
        }

        // Act
        var multipliers = _service.GetClassMultipliers(validClassRef);

        // Assert
        multipliers.Should().NotBeNull();
        multipliers!.ClassRef.Should().Be(validClassRef);
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
    public void LoadConfig_ShouldLoadStatPointAllocationRules()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.StatPointAllocation.Should().NotBeNull();
        config.StatPointAllocation.PointsPerLevel.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LoadConfig_ShouldLoadRespecSystemConfiguration()
    {
        // Act
        var config = _service.LoadConfig();

        // Assert
        config.RespecSystem.Should().NotBeNull();
        config.RespecSystem.CostFormula.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadConfig_WithMissingFile_ShouldReturnDefaultConfig()
    {
        // Arrange - Create service with invalid data path
        var invalidDataCache = new GameDataCache("invalid/path", null);
        var service = new CharacterGrowthService(invalidDataCache, _referenceResolver);

        // Act
        var config = service.LoadConfig();

        // Assert
        config.Should().NotBeNull("default config should be returned when file is missing");
        config.Version.Should().NotBeNullOrEmpty();
    }
}
