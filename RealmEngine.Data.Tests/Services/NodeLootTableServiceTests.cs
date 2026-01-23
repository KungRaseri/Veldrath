using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Services;

public class NodeLootTableServiceTests
{
    private readonly Mock<ILogger<NodeLootTableService>> _mockLogger;
    private readonly NodeLootTableService _service;
    private readonly NodeLootTableService _deterministicService;
    private readonly string _testDataPath;

    public NodeLootTableServiceTests()
    {
        _mockLogger = new Mock<ILogger<NodeLootTableService>>();
        
        // Navigate to actual data path for integration tests
        _testDataPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..",
            "RealmEngine.Data", "Data", "Json"
        );
        _testDataPath = Path.GetFullPath(_testDataPath);
        
        // Service with random seed for normal operation
        _service = new NodeLootTableService(_mockLogger.Object, _testDataPath);
        
        // Service with fixed seed for deterministic tests
        _deterministicService = new NodeLootTableService(_mockLogger.Object, _testDataPath, seed: 42);
    }

    [Fact]
    public void LoadLootTables_Should_Load_All_Loot_Table_Files()
    {
        // Act
        _service.LoadLootTables();

        // Assert - should load 4 tables (copper, iron, oak, ash)
        // Verify by calling RollLootDrops for each table
        var copperDrops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);
        var ironDrops = _service.RollLootDrops("@loot-tables/nodes/ores:iron", 1, false);
        var oakDrops = _service.RollLootDrops("@loot-tables/nodes/woods:oak", 1, false);
        var ashDrops = _service.RollLootDrops("@loot-tables/nodes/woods:ash", 1, false);

        // All should return at least primary drops
        copperDrops.Should().NotBeEmpty();
        ironDrops.Should().NotBeEmpty();
        oakDrops.Should().NotBeEmpty();
        ashDrops.Should().NotBeEmpty();
    }

    [Fact]
    public void RollLootDrops_Should_Return_ItemDrop_With_Correct_Structure()
    {
        // Arrange
        _service.LoadLootTables();

        // Act
        var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Assert
        drops.Should().NotBeEmpty();
        var firstDrop = drops.First();
        
        firstDrop.ItemRef.Should().NotBeNullOrEmpty();
        firstDrop.ItemName.Should().NotBeNullOrEmpty();
        firstDrop.Quantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RollLootDrops_Should_Always_Return_Primary_Drop()
    {
        // Arrange
        _service.LoadLootTables();

        // Act - copper ore has 100% copper-ore drop
        var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Assert
        drops.Should().Contain(d => 
            d.ItemRef.Contains("copper-ore") && 
            d.IsBonus == false);
    }

    [Fact]
    public void RollLootDrops_Should_Scale_Primary_Drops_By_Yield()
    {
        // Arrange
        _service.LoadLootTables();

        // Act - roll with yield of 3
        var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 3, false);

        // Assert - primary drop should be 3x base quantity
        var primaryDrop = drops.FirstOrDefault(d => !d.IsBonus);
        primaryDrop.Should().NotBeNull();
        primaryDrop!.Quantity.Should().Be(3); // 1 * 3 = 3
    }

    [Fact]
    public void RollLootDrops_Should_Not_Scale_Bonus_Drops_By_Yield()
    {
        // Arrange
        _deterministicService.LoadLootTables();

        // Act - roll multiple times to get bonus drops
        List<ItemDrop> allDrops = new();
        for (int i = 0; i < 50; i++)
        {
            var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 5, false);
            allDrops.AddRange(drops);
        }

        // Assert - bonus drops should have quantity 1-2, not scaled by yield
        var bonusDrops = allDrops.Where(d => d.IsBonus).ToList();
        if (bonusDrops.Any())
        {
            bonusDrops.Should().AllSatisfy(drop => 
                drop.Quantity.Should().BeLessThanOrEqualTo(2));
        }
    }

    [Fact]
    public void RollLootDrops_Should_Only_Include_Critical_Drops_When_IsCriticalHarvest_True()
    {
        // Arrange
        _service.LoadLootTables();

        // Act - iron has 3% rough-ruby drop that requires critical
        List<ItemDrop> normalDrops = new();
        List<ItemDrop> criticalDrops = new();

        for (int i = 0; i < 100; i++)
        {
            var normal = _service.RollLootDrops("@loot-tables/nodes/ores:iron", 1, false);
            var critical = _service.RollLootDrops("@loot-tables/nodes/ores:iron", 1, true);
            
            normalDrops.AddRange(normal);
            criticalDrops.AddRange(critical);
        }

        // Assert - rough-ruby should never appear in normal drops
        normalDrops.Should().NotContain(d => d.ItemRef.Contains("ruby"));
        
        // Critical drops might contain ruby (3% chance, so not guaranteed in 100 rolls)
        // But at least verify structure supports it
        criticalDrops.Should().NotBeEmpty();
    }

    [Fact]
    public void RollLootDrops_Should_Respect_Drop_Probabilities()
    {
        // Arrange
        _service.LoadLootTables();

        // Act - copper has 15% tin-ore bonus drop
        int totalRolls = 1000;
        int tinOreDrops = 0;

        for (int i = 0; i < totalRolls; i++)
        {
            var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);
            if (drops.Any(d => d.ItemRef.Contains("tin-ore")))
            {
                tinOreDrops++;
            }
        }

        // Assert - should be approximately 15% (±5% tolerance)
        double actualPercentage = (double)tinOreDrops / totalRolls * 100;
        actualPercentage.Should().BeInRange(10, 20);
    }

    [Fact]
    public void RollLootDrops_Should_Extract_Correct_Item_Names()
    {
        // Arrange
        _service.LoadLootTables();

        // Act
        var drops = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Assert - copper-ore should become "Copper Ore"
        var copperDrop = drops.FirstOrDefault(d => d.ItemRef.Contains("copper-ore"));
        copperDrop.Should().NotBeNull();
        copperDrop!.ItemName.Should().Be("Copper Ore");
    }

    [Fact]
    public void RollLootDrops_Should_Handle_Invalid_LootTableRef_Gracefully()
    {
        // Arrange
        _service.LoadLootTables();

        // Act
        var drops = _service.RollLootDrops("@loot-tables/nodes/invalid:table", 1, false);

        // Assert - should return empty list instead of throwing
        drops.Should().BeEmpty();
    }

    [Fact]
    public void ClearCache_Should_Allow_Reloading_Loot_Tables()
    {
        // Arrange
        _service.LoadLootTables();
        var dropsBeforeClear = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Act
        _service.ClearCache();
        _service.LoadLootTables();
        var dropsAfterClear = _service.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Assert - should still work after clearing cache
        dropsBeforeClear.Should().NotBeEmpty();
        dropsAfterClear.Should().NotBeEmpty();
    }

    [Fact]
    public void RollLootDrops_With_Deterministic_Seed_Should_Produce_Consistent_Results()
    {
        // Arrange
        var service1 = new NodeLootTableService(_mockLogger.Object, _testDataPath, seed: 123);
        var service2 = new NodeLootTableService(_mockLogger.Object, _testDataPath, seed: 123);
        
        service1.LoadLootTables();
        service2.LoadLootTables();

        // Act
        var drops1 = service1.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);
        var drops2 = service2.RollLootDrops("@loot-tables/nodes/ores:copper", 1, false);

        // Assert - same seed should produce same results
        drops1.Should().HaveCount(drops2.Count);
        
        for (int i = 0; i < drops1.Count; i++)
        {
            drops1[i].ItemRef.Should().Be(drops2[i].ItemRef);
            drops1[i].Quantity.Should().Be(drops2[i].Quantity);
            drops1[i].IsBonus.Should().Be(drops2[i].IsBonus);
        }
    }

    [Theory]
    [InlineData("@loot-tables/nodes/ores:copper")]
    [InlineData("@loot-tables/nodes/ores:iron")]
    [InlineData("@loot-tables/nodes/woods:oak")]
    [InlineData("@loot-tables/nodes/woods:ash")]
    public void RollLootDrops_Should_Handle_All_Loot_Table_References(string lootTableRef)
    {
        // Arrange
        _service.LoadLootTables();

        // Act
        var drops = _service.RollLootDrops(lootTableRef, 1, false);

        // Assert
        drops.Should().NotBeEmpty();
        drops.Should().Contain(d => !d.IsBonus); // Should always have primary drop
    }

    [Fact]
    public void RollLootDrops_Should_Handle_Variable_Quantity_Bonus_Drops()
    {
        // Arrange
        _service.LoadLootTables();

        // Act - oak has bark drops with minQuantity=1, maxQuantity=2
        List<ItemDrop> allDrops = new();
        for (int i = 0; i < 100; i++)
        {
            var drops = _service.RollLootDrops("@loot-tables/nodes/woods:oak", 1, false);
            allDrops.AddRange(drops);
        }

        // Assert - bark drops should have quantities of 1 or 2
        var barkDrops = allDrops.Where(d => d.ItemRef.Contains("bark")).ToList();
        if (barkDrops.Any())
        {
            barkDrops.Should().AllSatisfy(drop => 
                drop.Quantity.Should().BeInRange(1, 2));
        }
    }
}
