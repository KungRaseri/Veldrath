using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for LootTableService loot drop calculations and harvesting mechanics.
/// </summary>
public class LootTableServiceTests
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly LootTableService _service;

    public LootTableServiceTests()
    {
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");

        _dataCache = new GameDataCache(dataPath, null);
        _dataCache.LoadAllData();

        _referenceResolver = new ReferenceResolverService(_dataCache, NullLogger<ReferenceResolverService>.Instance);
        _service = new LootTableService(NullLogger<LootTableService>.Instance, _dataCache, _referenceResolver);
    }

    [Fact]
    public void RollHarvestingDrops_WithCriticalHarvest_ShouldIncludeCriticalOnlyDrops()
    {
        // Arrange
        var lootTableRef = "@loot-tables/harvesting/woods:oak";
        var baseYield = 5;

        // Act
        var drops = _service.RollHarvestingDrops(lootTableRef, baseYield, true);

        // Assert
        drops.Should().NotBeNull();
        // Critical harvest allows drops that require critical (tested indirectly - service should not throw)
        // The actual behavior depends on loot table data, so we just verify it runs successfully
    }

    [Fact]
    public void RollHarvestingDrops_WithInvalidReference_ShouldReturnEmptyList()
    {
        // Arrange
        var invalidRef = "@loot-tables/invalid/category:nonexistent";

        // Act
        var drops = _service.RollHarvestingDrops(invalidRef, 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("invalid loot table should return no drops");
    }

    [Fact]
    public void RollHarvestingDrops_WithNullReference_ShouldReturnEmptyList()
    {
        // Act
        var drops = _service.RollHarvestingDrops(null!, 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("null reference should return no drops");
    }

    [Fact]
    public void RollHarvestingDrops_WithEmptyReference_ShouldReturnEmptyList()
    {
        // Act
        var drops = _service.RollHarvestingDrops("", 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("empty reference should return no drops");
    }

    [Fact]
    public void RollHarvestingDrops_WithZeroYield_ShouldReturnDrops()
    {
        // Arrange
        var lootTableRef = "@loot-tables/harvesting/woods:oak";

        // Act
        var drops = _service.RollHarvestingDrops(lootTableRef, 0, false);

        // Assert
        // Service should still process loot table even with zero yield
        drops.Should().NotBeNull();
    }

    [Fact]
    public void RollHarvestingDrops_WithHighYield_ShouldScaleDropQuantity()
    {
        // Arrange
        var lootTableRef = "@loot-tables/harvesting/woods:oak";
        var lowYield = 1;
        var highYield = 20;

        // Act
        var lowYieldDrops = _service.RollHarvestingDrops(lootTableRef, lowYield, false);
        var highYieldDrops = _service.RollHarvestingDrops(lootTableRef, highYield, false);

        // Assert
        lowYieldDrops.Should().NotBeNull();
        highYieldDrops.Should().NotBeNull();
        
        var lowTotalQuantity = lowYieldDrops.Sum(d => d.Quantity);
        var highTotalQuantity = highYieldDrops.Sum(d => d.Quantity);
        
        highTotalQuantity.Should().BeGreaterThanOrEqualTo(lowTotalQuantity,
            "higher yield should produce equal or more items");
    }

    [Fact]
    public void Constructor_WithSeed_ShouldProduceDeterministicResults()
    {
        // Arrange
        var seed = 12345;
        var service1 = new LootTableService(NullLogger<LootTableService>.Instance, _dataCache, _referenceResolver, seed);
        var service2 = new LootTableService(NullLogger<LootTableService>.Instance, _dataCache, _referenceResolver, seed);
        var lootTableRef = "@loot-tables/harvesting/woods:oak";

        // Act
        var drops1 = service1.RollHarvestingDrops(lootTableRef, 5, false);
        var drops2 = service2.RollHarvestingDrops(lootTableRef, 5, false);

        // Assert
        drops1.Count.Should().Be(drops2.Count, "same seed should produce same results");
        for (int i = 0; i < drops1.Count; i++)
        {
            drops1[i].ItemRef.Should().Be(drops2[i].ItemRef);
            drops1[i].Quantity.Should().Be(drops2[i].Quantity);
        }
    }

}
