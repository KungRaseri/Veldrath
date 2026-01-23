using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Shared.Models.Harvesting;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

public class HarvestCalculatorTests
{
    private readonly Mock<ILogger<HarvestCalculatorService>> _mockLogger;
    private readonly HarvestingConfig _config;
    private readonly HarvestCalculatorService _calculator;

    public HarvestCalculatorTests()
    {
        _mockLogger = new Mock<ILogger<HarvestCalculatorService>>();
        _config = CreateDefaultConfig();
        _calculator = new HarvestCalculatorService(_mockLogger.Object, _config);
    }

    [Fact]
    public void CalculateYield_NoviceWithNoTool_ReturnsBaseYield()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 0;
        int toolTier = 0;
        bool isCritical = false;

        // Act
        var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

        // Assert
        yield.Should().Be(2, "base yield is 2 with no bonuses");
    }

    [Fact]
    public void CalculateYield_ExpertWithSteelTool_AppliesSkillAndToolBonuses()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 100; // +30% skill bonus (100 × 0.003)
        int toolTier = 3; // Tier 3 on tier 0 node = +30% cap
        bool isCritical = false;

        // Act
        var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

        // Assert: 2 × 1.3 × 1.3 = 3.38, rounded to 3
        yield.Should().Be(3, "skill and tool bonuses should stack multiplicatively");
    }

    [Fact]
    public void CalculateYield_CriticalHarvest_DoublesYield()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 0;
        int toolTier = 0;
        bool isCritical = true;

        // Act
        var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

        // Assert: 2 × 2.0 = 4
        yield.Should().Be(4, "critical harvest doubles the yield");
    }

    [Fact]
    public void CalculateYield_ExhaustedNode_AppliesPenalty()
    {
        // Arrange
        var node = CreateCommonNode();
        node.CurrentHealth = 20; // 20% health = Exhausted state
        int skillRank = 0;
        int toolTier = 0;
        bool isCritical = false;

        // Act
        var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

        // Assert: 2 × 0.5 (exhausted penalty) = 1
        yield.Should().Be(1, "exhausted nodes have 50% yield penalty");
    }

    [Fact]
    public void CalculateYield_ToolBonusCappedAt30Percent()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 0;
        int toolTier = 5; // Tier 5 on tier 0 node would be +50% without cap
        bool isCritical = false;

        // Act
        var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

        // Assert: 2 × 1.3 (capped at +30%) = 2.6, rounded to 3
        yield.Should().Be(3, "tool bonus should be capped at +30%");
    }

    [Fact]
    public void CalculateDepletion_NoviceWithBronzeTool_ReturnsBaseDepletion()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 0;
        int toolTier = 1;
        bool hasNoTool = false;

        // Act
        var depletion = _calculator.CalculateDepletion(node, skillRank, toolTier, hasNoTool);

        // Assert: 20 × 1.0 (tier 1) / 1.0 (rank 0) = 20
        depletion.Should().Be(20, "base depletion is 20 for tier 1 tool at rank 0");
    }

    [Fact]
    public void CalculateDepletion_MasterWithMithrilTool_ReducesDepletion()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 200; // -40% skill reduction (200 × 0.002)
        int toolTier = 4; // Tier 4 tool modifier = 0.7
        bool hasNoTool = false;

        // Act
        var depletion = _calculator.CalculateDepletion(node, skillRank, toolTier, hasNoTool);

        // Assert: 20 × 0.7 / 0.6 = 23.33, rounded to 23
        depletion.Should().Be(23, "high skill and quality tool should optimize depletion");
    }

    [Fact]
    public void CalculateDepletion_NoTool_DoublesDepletion()
    {
        // Arrange
        var node = CreateCommonNode();
        int skillRank = 0;
        int toolTier = 0;
        bool hasNoTool = true;

        // Act
        var depletion = _calculator.CalculateDepletion(node, skillRank, toolTier, hasNoTool);

        // Assert: 20 × 1.0 / 1.0 × 2.0 = 40
        depletion.Should().Be(40, "no tool doubles depletion (wasteful)");
    }

    [Fact]
    public void CalculateSkillXP_CommonNode_AwardsBaseXP()
    {
        // Arrange
        var node = CreateCommonNode();
        int toolTier = 1;
        bool isCritical = false;

        // Act
        var xp = _calculator.CalculateSkillXP(node, toolTier, isCritical);

        // Assert: 10 × 1.0 (common) × 1.1 (tier 1) = 11
        xp.Should().Be(11, "common node with tier 1 tool awards 11 XP");
    }

    [Fact]
    public void CalculateSkillXP_EpicNodeWithCritical_AwardsHighXP()
    {
        // Arrange
        var node = CreateEpicNode();
        int toolTier = 4;
        bool isCritical = true;

        // Act
        var xp = _calculator.CalculateSkillXP(node, toolTier, isCritical);

        // Assert: 10 × 4.0 (epic) × 1.4 (tier 4) × 1.5 (crit) = 84
        xp.Should().Be(84, "epic node with tier 4 tool and critical harvest awards significant XP");
    }

    [Fact]
    public void CalculateDurabilityLoss_CommonNodeTier3Tool_LowDurabilityLoss()
    {
        // Arrange
        var node = CreateCommonNode();
        int toolTier = 3;
        bool isCritical = false;

        // Act
        var durabilityLoss = _calculator.CalculateDurabilityLoss(node, toolTier, isCritical);

        // Assert: 1 × (1.0 / 1.0) = 1
        durabilityLoss.Should().Be(1, "tier 3 tool on common node has standard durability loss");
    }

    [Fact]
    public void CalculateDurabilityLoss_LegendaryNodeTier1Tool_HighDurabilityLoss()
    {
        // Arrange
        var node = CreateLegendaryNode();
        int toolTier = 1;
        bool isCritical = false;

        // Act
        var durabilityLoss = _calculator.CalculateDurabilityLoss(node, toolTier, isCritical);

        // Assert: 1 × (2.0 / 0.5) = 4
        durabilityLoss.Should().Be(4, "weak tool on legendary node has high durability loss");
    }

    [Fact]
    public void CalculateDurabilityLoss_CriticalHarvest_ReducesDurabilityLoss()
    {
        // Arrange
        var node = CreateCommonNode();
        int toolTier = 1;
        bool isCritical = true;

        // Act
        var durabilityLoss = _calculator.CalculateDurabilityLoss(node, toolTier, isCritical);

        // Assert: 1 × (1.0 / 0.5) × 0.5 (crit reduction) = 1
        durabilityLoss.Should().Be(1, "critical harvest reduces durability loss by 50%");
    }

    // Helper methods
    private HarvestableNode CreateCommonNode()
    {
        return new HarvestableNode
        {
            NodeId = "test_copper",
            DisplayName = "Copper Vein",
            MaterialTier = "common",
            CurrentHealth = 100,
            MaxHealth = 100,
            BaseYield = 2,
            MinToolTier = 0,
            IsRichNode = false
        };
    }

    private HarvestableNode CreateEpicNode()
    {
        return new HarvestableNode
        {
            NodeId = "test_mithril",
            DisplayName = "Mithril Vein",
            MaterialTier = "epic",
            CurrentHealth = 300,
            MaxHealth = 300,
            BaseYield = 1,
            MinToolTier = 3,
            IsRichNode = false
        };
    }

    private HarvestableNode CreateLegendaryNode()
    {
        return new HarvestableNode
        {
            NodeId = "test_adamantine",
            DisplayName = "Adamantine Vein",
            MaterialTier = "legendary",
            CurrentHealth = 500,
            MaxHealth = 500,
            BaseYield = 1,
            MinToolTier = 4,
            IsRichNode = true
        };
    }

    private HarvestingConfig CreateDefaultConfig()
    {
        return new HarvestingConfig
        {
            NodeHealth = new NodeHealthConfig
            {
                BaseDepletion = 20,
                HealthThresholds = new Dictionary<string, double>
                {
                    ["healthy"] = 0.8,
                    ["depleted"] = 0.4,
                    ["exhausted"] = 0.1
                }
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
                SkillScaling = 0.0005,
                ToolBonusPerTier = 0.01,
                RichNodeBonus = 0.05,
                DurabilityReduction = 0.5,
                XpBonus = 1.5
            },
            ToolRequirements = new ToolRequirementsConfig
            {
                EnforceMinimum = true,
                NoToolPenalty = 0.5,
                NoToolDepletionMultiplier = 2.0,
                AllowNoToolForCommon = true
            },
            SkillXP = new SkillXPConfig
            {
                BaseXP = 10,
                TierMultipliers = new TierMultipliers
                {
                    Common = 1.0,
                    Uncommon = 2.0,
                    Rare = 3.0,
                    Epic = 4.0,
                    Legendary = 5.0
                },
                ToolQualityBonus = 0.1,
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
                    Epic = 1.8,
                    Legendary = 2.0
                },
                ToolHardness = new ToolHardnessConfig
                {
                    Tier1 = 0.5,
                    Tier2 = 0.7,
                    Tier3 = 1.0,
                    Tier4 = 1.5,
                    Tier5 = 2.0
                }
            }
        };
    }
}
