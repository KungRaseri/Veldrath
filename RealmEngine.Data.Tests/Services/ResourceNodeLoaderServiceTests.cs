using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Data.Services;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace RealmEngine.Data.Tests.Services
{
    public class ResourceNodeLoaderServiceTests
    {
        private readonly Mock<ILogger<ResourceNodeLoaderService>> _loggerMock;
        private readonly GameDataCache _dataCache;
        private readonly string _testDataPath;

        public ResourceNodeLoaderServiceTests()
        {
            _loggerMock = new Mock<ILogger<ResourceNodeLoaderService>>();
            
            // Use actual data path for integration tests
            _testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..",
                "RealmEngine.Data", "Data", "Json"
            );
            _testDataPath = Path.GetFullPath(_testDataPath);

            // Initialize GameDataCache
            _dataCache = new GameDataCache(_testDataPath);
            _dataCache.LoadAllData();
        }

        [Fact]
        public void LoadNodes_Should_Load_All_Nodes_Successfully()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _dataCache);

            // Act
            service.LoadNodes();
            var allNodes = service.GetAllNodes();

            // Assert
            allNodes.Should().NotBeEmpty();
            allNodes.Should().HaveCountGreaterThan(10, "resource-nodes.json should have at least 12 nodes");
        }

        [Fact]
        public void LoadNodes_Should_Cache_Results()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _dataCache);

            // Act
            service.LoadNodes();
            var firstCallCount = service.GetAllNodes().Count;
            service.LoadNodes(); // Second call should use cache
            var secondCallCount = service.GetAllNodes().Count;

            // Assert
            firstCallCount.Should().Be(secondCallCount);
            firstCallCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetNodeById_Should_Return_Correct_Node()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var copperVein = service.GetNodeById("copper_vein");

            // Assert
            copperVein.Should().NotBeNull();
            copperVein!.Name.Should().Be("Copper Vein");
            copperVein.Tier.Should().Be("common");
            copperVein.SkillRef.Should().Be("@skills/profession:mining");
            copperVein.MinToolTier.Should().Be(0);
        }

        [Fact]
        public void GetNodeById_Should_Return_Null_For_NonExistent_Node()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var result = service.GetNodeById("nonexistent_node");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetNodesByBiome_Should_Return_Matching_Nodes()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var forestNodes = service.GetNodesByBiome("forest");

            // Assert
            forestNodes.Should().NotBeEmpty();
            forestNodes.Should().Contain(n => n.NodeType == "oak_tree");
            forestNodes.All(n => n.Biomes.Contains("forest", StringComparer.OrdinalIgnoreCase))
                .Should().BeTrue();
        }

        [Fact]
        public void GetNodesByTier_Should_Return_Matching_Nodes()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var commonNodes = service.GetNodesByTier("common");

            // Assert
            commonNodes.Should().NotBeEmpty();
            commonNodes.All(n => n.Tier.Equals("common", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue();
        }

        [Fact]
        public void GetNodesBySkill_Should_Return_Matching_Nodes()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var miningNodes = service.GetNodesBySkill("@skills/profession:mining");

            // Assert
            miningNodes.Should().NotBeEmpty();
            miningNodes.Should().Contain(n => n.NodeType == "copper_vein");
            miningNodes.Should().Contain(n => n.NodeType == "iron_vein");
            miningNodes.All(n => n.SkillRef.Equals("@skills/profession:mining", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue();
        }

        [Fact]
        public void GetNodesBySkill_Should_Find_Woodcutting_Nodes()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var woodcuttingNodes = service.GetNodesBySkill("@skills/profession:woodcutting");

            // Assert
            woodcuttingNodes.Should().NotBeEmpty();
            woodcuttingNodes.Should().Contain(n => n.NodeType == "oak_tree");
            woodcuttingNodes.Should().Contain(n => n.NodeType == "ash_tree");
        }

        [Fact]
        public void ClearCache_Should_Force_Reload()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();
            var initialCount = service.GetAllNodes().Count;

            // Act
            service.ClearCache();
            service.LoadNodes();
            var reloadedCount = service.GetAllNodes().Count;

            // Assert
            reloadedCount.Should().Be(initialCount);
            reloadedCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void LoadNodes_Should_Parse_All_Required_Fields()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var oakTree = service.GetNodeById("oak_tree");

            // Assert
            oakTree.Should().NotBeNull();
            oakTree!.NodeType.Should().Be("oak_tree");
            oakTree.Name.Should().NotBeEmpty();
            oakTree.Tier.Should().NotBeEmpty();
            oakTree.SkillRef.Should().NotBeEmpty();
            oakTree.Health.Should().BeGreaterThan(0);
            oakTree.BaseYield.Should().BeGreaterThan(0);
            oakTree.LootTable.Should().NotBeEmpty();
            oakTree.Biomes.Should().NotBeEmpty();
            oakTree.RarityWeight.Should().BeGreaterThan(0);
            oakTree.Icon.Should().NotBeEmpty();
        }

        [Fact]
        public void LoadNodes_Should_Handle_Multiple_Node_Categories()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var oreNode = service.GetNodeById("copper_vein");
            var treeNode = service.GetNodeById("oak_tree");
            var herbNode = service.GetNodeById("common_herbs");

            // Assert
            oreNode.Should().NotBeNull("ore_veins category should be loaded");
            treeNode.Should().NotBeNull("trees category should be loaded");
            herbNode.Should().NotBeNull("herb_patches category should be loaded");
        }

        [Fact]
        public void GetNodesByBiome_Should_Be_Case_Insensitive()
        {
            // Arrange
            var service = new ResourceNodeLoaderService(_loggerMock.Object, _testDataPath);
            service.LoadNodes();

            // Act
            var lowerCase = service.GetNodesByBiome("forest");
            var upperCase = service.GetNodesByBiome("FOREST");
            var mixedCase = service.GetNodesByBiome("FoReSt");

            // Assert
            lowerCase.Should().NotBeEmpty();
            upperCase.Count.Should().Be(lowerCase.Count);
            mixedCase.Count.Should().Be(lowerCase.Count);
        }
    }
}
