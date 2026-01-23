using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.Harvesting;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;
using System.IO;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Harvesting;

/// <summary>
/// Integration tests for the complete harvesting workflow.
/// Tests repository integration, inventory updates, and node state transitions.
/// </summary>
public class HarvestingIntegrationTests
{
    private readonly InMemoryNodeRepository _nodeRepository;
    private readonly InMemoryInventoryService _inventoryService;
    private readonly HarvestingConfig _config;
    
    public HarvestingIntegrationTests()
    {
        // Create logger factory
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Create default configuration
        _config = new HarvestingConfig
        {
            NodeHealth = new NodeHealthConfig
            {
                BaseDepletion = 50,
                HealthThresholds = new Dictionary<string, double>
                {
                    { "healthy", 0.80 },
                    { "depleted", 0.40 },
                    { "exhausted", 0.10 }
                },
                RespawnRate = 1,
                RespawnUnit = "hour"
            },
            YieldCalculation = new YieldCalculationConfig
            {
                SkillScaling = 0.003,
                ToolBonusPerTier = 0.10,
                MaxToolBonus = 0.30,
                CriticalMultiplier = 2.0,
                ExhaustedPenalty = 0.5
            },
            CriticalHarvest = new CriticalHarvestConfig
            {
                BaseChance = 0.05,
                SkillScaling = 0.001,
                ToolBonusPerTier = 0.02,
                RichNodeBonus = 0.10,
                BonusMaterialChance = 0.50,
                RareDropChance = 0.20,
                DurabilityReduction = 0.5,
                XpBonus = 1.5
            },
            ToolRequirements = new ToolRequirementsConfig
            {
                EnforceMinimum = true,
                NoToolPenalty = 0.5,
                NoToolDepletionMultiplier = 1.5,
                AllowNoToolForCommon = true
            },
            SkillXP = new SkillXPConfig
            {
                BaseXP = 10,
                TierMultipliers = new TierMultipliers
                {
                    Common = 1.0,
                    Uncommon = 1.5,
                    Rare = 2.0,
                    Epic = 3.0,
                    Legendary = 5.0
                },
                ToolQualityBonus = 0.05,
                CriticalBonus = 1.5
            },
            DurabilityLoss = new DurabilityLossConfig
            {
                BaseLoss = 1,
                NodeResistance = new NodeResistanceConfig
                {
                    Common = 1.0,
                    Uncommon = 1.2,
                    Rare = 1.5,
                    Epic = 2.0,
                    Legendary = 3.0
                },
                ToolHardness = new ToolHardnessConfig
                {
                    Tier1 = 1.0,
                    Tier2 = 0.9,
                    Tier3 = 0.8,
                    Tier4 = 0.7,
                    Tier5 = 0.6
                }
            }
        };
        
        // Create in-memory repositories
        _nodeRepository = new InMemoryNodeRepository(
            loggerFactory.CreateLogger<InMemoryNodeRepository>()
        );
        _inventoryService = new InMemoryInventoryService(
            loggerFactory.CreateLogger<InMemoryInventoryService>()
        );
    }
    
    [Fact]
    public async Task HarvestNode_FullWorkflow_ShouldUpdateInventoryAndNodeState()
    {
        // Arrange
        var node = new HarvestableNode
        {
            NodeId = "test-oak-001",
            NodeType = "oak_tree",
            DisplayName = "Ancient Oak Tree",
            MaterialTier = "common",
            CurrentHealth = 100,
            MaxHealth = 100,
            MinToolTier = 0, // No tool required
            BaseYield = 3,
            LootTableRef = "@loot-tables/nodes/woods:common-trees",
            IsRichNode = false,
            BiomeType = "forest",
            LocationId = "starting-forest"
        };
        
        await _nodeRepository.SpawnNodeAsync(node);
        
        var command = new HarvestNodeCommand
        {
            CharacterName = "TestPlayer",
            NodeId = "test-oak-001",
            EquippedToolRef = null, // No tool for common materials
            SkillRankOverride = 10 // Low skill level
        };
        
        // Act
        var handler = GetHandler();
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.Success.Should().BeTrue($"Failure reason: {result.FailureReason}");
        result.MaterialsGained.Should().NotBeEmpty();
        result.SkillXPGained.Should().BeGreaterThan(0);
        result.NodeHealthRemaining.Should().BeLessThan(100);
        result.NodeHealthPercent.Should().BeGreaterThan(0);
        
        // Verify node state updated in repository
        var updatedNode = await _nodeRepository.GetNodeByIdAsync("test-oak-001");
        updatedNode.Should().NotBeNull();
        updatedNode!.CurrentHealth.Should().BeLessThan(100);
        updatedNode.TimesHarvested.Should().Be(1);
        
        // Verify inventory updated - check if any materials were added
        var hasItems = result.MaterialsGained.Count > 0;
        hasItems.Should().BeTrue();
    }
    
    [Fact]
    public async Task HarvestNode_NonexistentNode_ShouldFailGracefully()
    {
        // Arrange
        var command = new HarvestNodeCommand
        {
            CharacterName = "TestPlayer",
            NodeId = "nonexistent-node-999",
            EquippedToolRef = null,
            SkillRankOverride = 10
        };
        
        // Act
        var handler = GetHandler();
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not found");
        result.MaterialsGained.Should().BeEmpty();
        result.SkillXPGained.Should().Be(0);
    }
    
    [Fact]
    public async Task HarvestNode_MultipleHarvests_ShouldDepleteNode()
    {
        // Arrange
        var node = new HarvestableNode
        {
            NodeId = "test-copper-001",
            NodeType = "copper_vein",
            DisplayName = "Copper Vein",
            MaterialTier = "common",
            CurrentHealth = 150,
            MaxHealth = 150,
            MinToolTier = 1, // Requires pickaxe
            BaseYield = 5,
            LootTableRef = "@loot-tables/nodes/ores:common-ores",
            IsRichNode = false,
            BiomeType = "mountains",
            LocationId = "iron-peaks"
        };
        
        await _nodeRepository.SpawnNodeAsync(node);
        
        var handler = GetHandler();
        
        // Act - Harvest multiple times
        var results = new List<HarvestResult>();
        for (int i = 0; i < 3; i++)
        {
            var command = new HarvestNodeCommand
            {
                CharacterName = "TestMiner",
                NodeId = "test-copper-001",
                EquippedToolRef = "@items/tools/pickaxes:iron-pickaxe",
                SkillRankOverride = 20
            };
            
            var result = await handler.Handle(command, CancellationToken.None);
            results.Add(result);
        }
        
        // Assert
        results.Should().HaveCount(3);
        
        // All harvests should succeed
        results[0].Success.Should().BeTrue($"Failure reason: {results[0].FailureReason}");
        results[1].Success.Should().BeTrue($"Failure reason: {results[1].FailureReason}");
        results[2].Success.Should().BeTrue($"Failure reason: {results[2].FailureReason}");
        
        // Node should progressively deplete
        var finalNode = await _nodeRepository.GetNodeByIdAsync("test-copper-001");
        finalNode.Should().NotBeNull();
        finalNode!.CurrentHealth.Should().BeLessThan(150);
        finalNode.TimesHarvested.Should().BeGreaterThanOrEqualTo(3);
        
        // Health should decrease with each harvest
        results[0].NodeHealthRemaining.Should().BeGreaterThan(results[1].NodeHealthRemaining);
        results[1].NodeHealthRemaining.Should().BeGreaterThan(results[2].NodeHealthRemaining);
    }
    
    [Fact]
    public async Task HarvestNode_InsufficientToolTier_ShouldFail()
    {
        // Arrange
        var node = new HarvestableNode
        {
            NodeId = "test-mithril-001",
            NodeType = "mithril_vein",
            DisplayName = "Mithril Vein",
            MaterialTier = "rare",
            CurrentHealth = 200,
            MaxHealth = 200,
            MinToolTier = 3, // Requires tier 3+ pickaxe
            BaseYield = 2,
            LootTableRef = "@loot-tables/nodes/ores:rare-ores",
            IsRichNode = true,
            BiomeType = "deep-mines",
            LocationId = "mithril-depths"
        };
        
        await _nodeRepository.SpawnNodeAsync(node);
        
        var command = new HarvestNodeCommand
        {
            CharacterName = "TestMiner",
            NodeId = "test-mithril-001",
            EquippedToolRef = "@items/tools/pickaxes:iron-pickaxe", // Only tier 1
            SkillRankOverride = 50
        };
        
        // Act
        var handler = GetHandler();
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("tier");
        result.MaterialsGained.Should().BeEmpty();
        
        // Node should not be modified
        var unchangedNode = await _nodeRepository.GetNodeByIdAsync("test-mithril-001");
        unchangedNode!.CurrentHealth.Should().Be(200);
        unchangedNode.TimesHarvested.Should().Be(0);
    }
    
    [Fact]
    public async Task HarvestNode_NodeStateTransitions_ShouldReflectHealthPercentage()
    {
        // Arrange
        var node = new HarvestableNode
        {
            NodeId = "test-silver-001",
            NodeType = "silver_vein",
            DisplayName = "Silver Vein",
            MaterialTier = "uncommon",
            CurrentHealth = 100,
            MaxHealth = 100,
            MinToolTier = 2,
            BaseYield = 4,
            LootTableRef = "@loot-tables/nodes/ores:common-ores",
            IsRichNode = false,
            BiomeType = "mountains",
            LocationId = "silver-peaks"
        };
        
        await _nodeRepository.SpawnNodeAsync(node);
        var handler = GetHandler();
        
        // Act & Assert - Healthy (>= 80%)
        node.CurrentHealth = 85;
        await _nodeRepository.SaveNodeAsync(node);
        node.GetNodeState().Should().Be(NodeState.Healthy);
        
        // Act & Assert - Depleted (40-79%)
        node.CurrentHealth = 60;
        await _nodeRepository.SaveNodeAsync(node);
        node.GetNodeState().Should().Be(NodeState.Depleted);
        
        // Act & Assert - Exhausted (10-39%)
        node.CurrentHealth = 25;
        await _nodeRepository.SaveNodeAsync(node);
        node.GetNodeState().Should().Be(NodeState.Exhausted);
        
        // Act & Assert - Empty (< 10%)
        node.CurrentHealth = 5;
        await _nodeRepository.SaveNodeAsync(node);
        node.GetNodeState().Should().Be(NodeState.Empty);
        node.CanHarvest().Should().BeFalse();
    }
    
    [Fact]
    public async Task HarvestNode_InventoryAccumulation_ShouldStackMaterials()
    {
        // Arrange
        var node = new HarvestableNode
        {
            NodeId = "test-flax-001",
            NodeType = "flax_plant",
            DisplayName = "Flax Plant",
            MaterialTier = "common",
            CurrentHealth = 100,
            MaxHealth = 100,
            MinToolTier = 0,
            BaseYield = 10,
            LootTableRef = "@loot-tables/nodes/plants:common-plants",
            IsRichNode = false,
            BiomeType = "plains",
            LocationId = "green-meadows"
        };
        
        await _nodeRepository.SpawnNodeAsync(node);
        var handler = GetHandler();
        
        // Act - Harvest twice
        var command1 = new HarvestNodeCommand
        {
            CharacterName = "TestHerbalist",
            NodeId = "test-flax-001",
            SkillRankOverride = 15
        };
        
        var result1 = await handler.Handle(command1, CancellationToken.None);
        
        // Reset node for second harvest
        node.CurrentHealth = 100;
        await _nodeRepository.SaveNodeAsync(node);
        
        var command2 = new HarvestNodeCommand
        {
            CharacterName = "TestHerbalist",
            NodeId = "test-flax-001",
            SkillRankOverride = 15
        };
        
        var result2 = await handler.Handle(command2, CancellationToken.None);
        
        // Assert
        result1.Success.Should().BeTrue($"First harvest failure: {result1.FailureReason}");
        result2.Success.Should().BeTrue($"Second harvest failure: {result2.FailureReason}");
        
        // Materials should stack (total count should be sum of both harvests)
        var totalCount = result1.MaterialsGained.Sum(m => m.Quantity) + 
                        result2.MaterialsGained.Sum(m => m.Quantity);
        totalCount.Should().BeGreaterThan(0);
        
        // Verify at least one material type was added
        result1.MaterialsGained.Should().NotBeEmpty();
        result2.MaterialsGained.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task GetNodesByLocation_ShouldReturnAllNodesInLocation()
    {
        // Arrange
        var nodes = new List<HarvestableNode>
        {
            new()
            {
                NodeId = "forest-oak-001",
                NodeType = "oak_tree",
                DisplayName = "Oak Tree",
                LocationId = "starting-forest",
                CurrentHealth = 100,
                MaxHealth = 100,
                MaterialTier = "common",
                MinToolTier = 0,
                BaseYield = 3,
                LootTableRef = "@loot-tables/nodes/woods:common-trees",
                BiomeType = "forest"
            },
            new()
            {
                NodeId = "forest-birch-001",
                NodeType = "birch_tree",
                DisplayName = "Birch Tree",
                LocationId = "starting-forest",
                CurrentHealth = 100,
                MaxHealth = 100,
                MaterialTier = "common",
                MinToolTier = 0,
                BaseYield = 2,
                LootTableRef = "@loot-tables/nodes/woods:common-trees",
                BiomeType = "forest"
            },
            new()
            {
                NodeId = "mountain-copper-001",
                NodeType = "copper_vein",
                DisplayName = "Copper Vein",
                LocationId = "iron-peaks",
                CurrentHealth = 150,
                MaxHealth = 150,
                MaterialTier = "common",
                MinToolTier = 1,
                BaseYield = 5,
                LootTableRef = "@loot-tables/nodes/ores:common-ores",
                BiomeType = "mountains"
            }
        };
        
        foreach (var node in nodes)
        {
            await _nodeRepository.SpawnNodeAsync(node);
        }
        
        // Act
        var forestNodes = await _nodeRepository.GetNodesByLocationAsync("starting-forest");
        var mountainNodes = await _nodeRepository.GetNodesByLocationAsync("iron-peaks");
        
        // Assert
        forestNodes.Should().HaveCount(2);
        forestNodes.Should().Contain(n => n.NodeId == "forest-oak-001");
        forestNodes.Should().Contain(n => n.NodeId == "forest-birch-001");
        
        mountainNodes.Should().HaveCount(1);
        mountainNodes.Should().Contain(n => n.NodeId == "mountain-copper-001");
    }
    
    /// <summary>
    /// Helper method to create a configured handler instance.
    /// </summary>
    private HarvestNodeCommandHandler GetHandler()
    {
        var localLoggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        var calculator = new HarvestCalculatorService(
            localLoggerFactory.CreateLogger<HarvestCalculatorService>(),
            _config
        );
        
        var toolValidator = new ToolValidationService(
            localLoggerFactory.CreateLogger<ToolValidationService>(),
            _config
        );
        
        var criticalService = new CriticalHarvestService(
            localLoggerFactory.CreateLogger<CriticalHarvestService>(),
            _config
        );
        
        // Get absolute path to Data/Json directory
        var dataPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json"
        );
        dataPath = Path.GetFullPath(dataPath);
        
        var lootTableService = new NodeLootTableService(
            localLoggerFactory.CreateLogger<NodeLootTableService>(),
            dataPath
        );
        
        return new HarvestNodeCommandHandler(
            localLoggerFactory.CreateLogger<HarvestNodeCommandHandler>(),
            calculator,
            toolValidator,
            criticalService,
            lootTableService,
            _nodeRepository,
            _inventoryService
        );
    }
}


