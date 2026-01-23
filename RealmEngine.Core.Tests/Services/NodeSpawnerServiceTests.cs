using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

public class NodeSpawnerServiceTests
{
    private readonly Mock<ILogger<NodeSpawnerService>> _loggerMock;
    private readonly Mock<ILogger<ResourceNodeLoaderService>> _loaderLoggerMock;
    private readonly GameDataCache _dataCache;
    private readonly string _testDataPath;

    public NodeSpawnerServiceTests()
    {
        _loggerMock = new Mock<ILogger<NodeSpawnerService>>();
        _loaderLoggerMock = new Mock<ILogger<ResourceNodeLoaderService>>();
        
        // ResourceNodeLoaderService expects the base Data/Json path
        var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..");
        _testDataPath = Path.GetFullPath(Path.Combine(projectRoot, "RealmEngine.Data", "Data", "Json"));

        // Initialize GameDataCache
        _dataCache = new GameDataCache(_testDataPath);
        _dataCache.LoadAllData();
    }

    [Fact]
    public void SpawnNodes_Should_Return_Nodes_For_Valid_Biome()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("forest_clearing", "forest", "medium");

        // Assert
        nodes.Should().NotBeEmpty();
        nodes.All(n => !string.IsNullOrEmpty(n.NodeId)).Should().BeTrue();
        nodes.All(n => n.NodeId.StartsWith("forest_clearing_")).Should().BeTrue();
    }

    [Fact]
    public void SpawnNodes_Should_Return_Empty_List_For_Invalid_Biome()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("location_1", "nonexistent_biome", "medium");

        // Assert
        nodes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("abundant", 5, 7)]
    [InlineData("high", 3, 4)]
    [InlineData("medium", 2, 3)]
    [InlineData("low", 1, 2)]
    [InlineData("rare", 0, 1)]
    public void SpawnNodes_Should_Respect_Density_Rules(string density, int minCount, int maxCount)
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("test_location", "forest", density);

        // Assert
        nodes.Count.Should().BeInRange(minCount, maxCount);
    }

    [Fact]
    public void SpawnNodes_Should_Generate_Unique_Node_IDs()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("test_location", "forest", "high");

        // Assert
        var nodeIds = nodes.Select(n => n.NodeId).ToList();
        nodeIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SpawnNodes_Should_Set_Full_Health_On_Spawn()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("test_location", "forest", "medium");

        // Assert
        nodes.Should().NotBeEmpty();
        nodes.All(n => n.CurrentHealth == n.MaxHealth).Should().BeTrue();
        nodes.All(n => n.CurrentHealth > 0).Should().BeTrue();
    }

    [Fact]
    public void SpawnNodes_Should_Set_IsExhausted_To_False()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("test_location", "forest", "medium");

        // Assert
        nodes.Should().NotBeEmpty();
        nodes.All(n => n.CanHarvest()).Should().BeTrue();
    }

    [Fact]
    public void SpawnNodes_Should_Set_LocationId_Correctly()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act
        var nodes = spawner.SpawnNodes("mountain_pass", "mountains", "medium");

        // Assert
        nodes.Should().NotBeEmpty();
        nodes.All(n => n.LocationId == "mountain_pass").Should().BeTrue();
    }

    [Fact]
    public void SpawnNodes_Should_Apply_Weighted_Random_Selection()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act - Spawn many nodes to test probability distribution
        var allNodes = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            var nodes = spawner.SpawnNodes($"location_{i}", "forest", "abundant");
            allNodes.AddRange(nodes.Select(n => n.NodeType));
        }

        // Assert - Should have variety of node types
        var uniqueTypes = allNodes.Distinct().ToList();
        uniqueTypes.Count.Should().BeGreaterThan(1, "weighted selection should produce variety");

        // More common nodes (higher rarityWeight) should appear more frequently
        var typeCounts = allNodes.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        _loggerMock.Object.LogInformation("Node type distribution: {Distribution}", 
            string.Join(", ", typeCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
    }

    [Fact]
    public void SpawnNodes_Should_Throw_On_Null_LocationId()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act & Assert
        var act = () => spawner.SpawnNodes(null!, "forest", "medium");
        act.Should().Throw<ArgumentException>().WithParameterName("locationId");
    }

    [Fact]
    public void SpawnNodes_Should_Throw_On_Null_Biome()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);

        // Act & Assert
        var act = () => spawner.SpawnNodes("location_1", null!, "medium");
        act.Should().Throw<ArgumentException>().WithParameterName("biome");
    }

    [Fact]
    public void RespawnNode_Should_Restore_Exhausted_Node_After_Cooldown()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);
        var nodes = spawner.SpawnNodes("test_location", "forest", "medium");
        var node = nodes.First();

        // Exhaust the node
        node.CurrentHealth = 0;
        node.LastHarvestedAt = DateTime.UtcNow.AddMinutes(-61); // 61 minutes ago

        // Act
        var respawned = spawner.RespawnNode(node, cooldownMinutes: 60);

        // Assert
        respawned.Should().BeTrue();
        node.CurrentHealth.Should().Be(node.MaxHealth);
        node.CanHarvest().Should().BeTrue();
        node.LastHarvestedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void RespawnNode_Should_Not_Respawn_Before_Cooldown()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);
        var nodes = spawner.SpawnNodes("test_location", "forest", "medium");
        var node = nodes.First();

        // Exhaust the node
        node.CurrentHealth = 0;
        node.LastHarvestedAt = DateTime.UtcNow.AddMinutes(-30); // Only 30 minutes ago

        // Act
        var respawned = spawner.RespawnNode(node, cooldownMinutes: 60);

        // Assert
        respawned.Should().BeFalse();
        node.CanHarvest().Should().BeFalse();
        node.CurrentHealth.Should().Be(0);
    }

    [Fact]
    public void RespawnNode_Should_Return_False_For_Non_Exhausted_Node()
    {
        // Arrange
        var nodeLoader = new ResourceNodeLoaderService(_loaderLoggerMock.Object, _dataCache);
        var spawner = new NodeSpawnerService(_loggerMock.Object, nodeLoader);
        var nodes = spawner.SpawnNodes("test_location", "forest", "medium");
        var node = nodes.First();

        // Act (node is not exhausted)
        var respawned = spawner.RespawnNode(node);

        // Assert
        respawned.Should().BeFalse();
    }
}
