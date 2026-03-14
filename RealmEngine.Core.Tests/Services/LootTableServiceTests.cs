using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for LootTableService loot drop calculations.
/// Uses InMemoryLootTableRepository and a mocked IDbContextFactory.
/// </summary>
public class LootTableServiceTests
{
    private readonly LootTableService _service;

    public LootTableServiceTests()
    {
        var repo = new InMemoryLootTableRepository();
        var dbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        _service = new LootTableService(NullLogger<LootTableService>.Instance, repo, dbFactory.Object);
    }

    [Fact]
    public async Task RollHarvestingDrops_WithInvalidReference_ShouldReturnEmptyList()
    {
        // Arrange
        var invalidRef = "@loot-tables/invalid/category:nonexistent";

        // Act
        var drops = await _service.RollHarvestingDrops(invalidRef, 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("invalid loot table should return no drops");
    }

    [Fact]
    public async Task RollHarvestingDrops_WithNullReference_ShouldReturnEmptyList()
    {
        // Act
        var drops = await _service.RollHarvestingDrops(null!, 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("null reference should return no drops");
    }

    [Fact]
    public async Task RollHarvestingDrops_WithEmptyReference_ShouldReturnEmptyList()
    {
        // Act
        var drops = await _service.RollHarvestingDrops("", 5, false);

        // Assert
        drops.Should().NotBeNull();
        drops.Should().BeEmpty("empty reference should return no drops");
    }

    [Fact]
    public async Task RollHarvestingDrops_WithZeroYield_ShouldNotThrow()
    {
        // Arrange
        var lootTableRef = "@loot-tables/harvesting/woods:oak";

        // Act
        var drops = await _service.RollHarvestingDrops(lootTableRef, 0, false);

        // Assert
        drops.Should().NotBeNull();
    }

    [Fact]
    public async Task RollEnemyDrops_WithInvalidRef_ShouldReturnEmptyResult()
    {
        // Act
        var result = await _service.RollEnemyDrops("@loot-tables/enemies:nonexistent");

        // Assert
        result.Should().NotBeNull();
        result.Drops.Should().BeEmpty();
    }
}
