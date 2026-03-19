using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Shared.Models.Harvesting;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class CriticalHarvestServiceTests
{
    private static HarvestingConfig DefaultConfig() => new()
    {
        CriticalHarvest = new CriticalHarvestConfig
        {
            BaseChance = 0.05,
            SkillScaling = 0.0005,
            ToolBonusPerTier = 0.01,
            RichNodeBonus = 0.05,
            BonusMaterialChance = 0.25,
            RareDropChance = 0.10,
        }
    };

    private static CriticalHarvestService CreateService(HarvestingConfig? config = null) =>
        new(NullLogger<CriticalHarvestService>.Instance, config ?? DefaultConfig());

    // CalculateCriticalChance

    [Fact]
    public void CalculateCriticalChance_NoSkillNoToolNotRich_ReturnsBaseChance()
    {
        var service = CreateService();
        var result = service.CalculateCriticalChance(skillRank: 0, toolTier: 0, isRichNode: false);
        result.Should().BeApproximately(0.05, 0.001);
    }

    [Fact]
    public void CalculateCriticalChance_WithSkillRank_AddsSkillBonus()
    {
        var service = CreateService();
        // base 0.05 + (100 * 0.0005) = 0.05 + 0.05 = 0.10
        var result = service.CalculateCriticalChance(skillRank: 100, toolTier: 0, isRichNode: false);
        result.Should().BeApproximately(0.10, 0.001);
    }

    [Fact]
    public void CalculateCriticalChance_WithToolTier_AddsToolBonus()
    {
        var service = CreateService();
        // base 0.05 + (3 * 0.01) = 0.05 + 0.03 = 0.08
        var result = service.CalculateCriticalChance(skillRank: 0, toolTier: 3, isRichNode: false);
        result.Should().BeApproximately(0.08, 0.001);
    }

    [Fact]
    public void CalculateCriticalChance_RichNode_AddsRichBonus()
    {
        var service = CreateService();
        // base 0.05 + 0.05 = 0.10
        var result = service.CalculateCriticalChance(skillRank: 0, toolTier: 0, isRichNode: true);
        result.Should().BeApproximately(0.10, 0.001);
    }

    [Fact]
    public void CalculateCriticalChance_AllBonuses_StacksCorrectly()
    {
        var service = CreateService();
        // base 0.05 + skill(100*0.0005=0.05) + tool(3*0.01=0.03) + rich(0.05) = 0.18
        var result = service.CalculateCriticalChance(skillRank: 100, toolTier: 3, isRichNode: true);
        result.Should().BeApproximately(0.18, 0.001);
    }

    [Fact]
    public void CalculateCriticalChance_ExceedsOne_CapsAtOne()
    {
        // Very high skill rank pushes total above 1.0
        var service = CreateService();
        var result = service.CalculateCriticalChance(skillRank: 9999, toolTier: 5, isRichNode: true);
        result.Should().Be(1.0);
    }

    [Fact]
    public void CalculateCriticalChance_ExactlyOne_ReturnsOne()
    {
        var config = new HarvestingConfig
        {
            CriticalHarvest = new CriticalHarvestConfig
            {
                BaseChance = 1.0,
                SkillScaling = 0.0,
                ToolBonusPerTier = 0.0,
                RichNodeBonus = 0.0,
            }
        };
        var service = CreateService(config);
        var result = service.CalculateCriticalChance(skillRank: 0, toolTier: 0, isRichNode: false);
        result.Should().Be(1.0);
    }

    // RollCritical

    [Fact]
    public void RollCritical_OneHundredPercentChance_AlwaysReturnsTrue()
    {
        var config = new HarvestingConfig
        {
            CriticalHarvest = new CriticalHarvestConfig { BaseChance = 1.0 }
        };
        var service = CreateService(config);
        for (int i = 0; i < 20; i++)
            service.RollCritical(skillRank: 0, toolTier: 0, isRichNode: false).Should().BeTrue();
    }

    [Fact]
    public void RollCritical_ZeroPercentChance_AlwaysReturnsFalse()
    {
        var config = new HarvestingConfig
        {
            CriticalHarvest = new CriticalHarvestConfig
            {
                BaseChance = 0.0,
                SkillScaling = 0.0,
                ToolBonusPerTier = 0.0,
                RichNodeBonus = 0.0,
            }
        };
        var service = CreateService(config);
        for (int i = 0; i < 20; i++)
            service.RollCritical(skillRank: 0, toolTier: 0, isRichNode: false).Should().BeFalse();
    }
}

[Trait("Category", "Services")]
public class ToolValidationServiceTests
{
    private static HarvestingConfig DefaultConfig() => new()
    {
        ToolRequirements = new ToolRequirementsConfig
        {
            EnforceMinimum = true,
            AllowNoToolForCommon = true,
            NoToolPenalty = 0.5,
        }
    };

    private static ToolValidationService CreateService(HarvestingConfig? config = null) =>
        new(NullLogger<ToolValidationService>.Instance, config ?? DefaultConfig());

    private static HarvestableNode MakeNode(string tier = "common", int minToolTier = 0) => new()
    {
        NodeId = "node_001",
        NodeType = "copper_vein",
        DisplayName = "Copper Vein",
        MaterialTier = tier,
        MinToolTier = minToolTier,
        CurrentHealth = 100,
        MaxHealth = 100,
    };

    // No tool scenarios

    [Fact]
    public void ValidateTool_NoToolCommonNodeAllowNoTool_IsValid()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("common"), toolTier: null, toolType: null);
        result.IsValid.Should().BeTrue();
        result.HasNoTool.Should().BeTrue();
    }

    [Fact]
    public void ValidateTool_NoToolCommonNodeDisallowed_IsInvalid()
    {
        var config = new HarvestingConfig
        {
            ToolRequirements = new ToolRequirementsConfig { AllowNoToolForCommon = false }
        };
        var service = CreateService(config);
        var result = service.ValidateTool(MakeNode("common"), toolTier: null, toolType: null);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTool_NoToolRareNode_IsInvalid()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("rare"), toolTier: null, toolType: null);
        result.IsValid.Should().BeFalse();
        result.HasNoTool.Should().BeTrue();
    }

    [Fact]
    public void ValidateTool_ZeroToolTierCommonNode_TreatedAsNoTool()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("common"), toolTier: 0, toolType: null);
        result.IsValid.Should().BeTrue();
        result.HasNoTool.Should().BeTrue();
    }

    // Tool tier enforcement

    [Fact]
    public void ValidateTool_ToolBelowMinimum_IsInvalid()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("rare", minToolTier: 3), toolTier: 1, toolType: null);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTool_ToolMeetsMinimum_IsValid()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("rare", minToolTier: 2), toolTier: 2, toolType: "pickaxe");
        result.IsValid.Should().BeTrue();
        result.ToolTier.Should().Be(2);
    }

    [Fact]
    public void ValidateTool_ToolAboveMinimum_IsValidWithYieldBonus()
    {
        var service = CreateService();
        var result = service.ValidateTool(MakeNode("rare", minToolTier: 1), toolTier: 3, toolType: "pickaxe");
        result.IsValid.Should().BeTrue();
        result.Message.Should().Contain("yield bonus");
    }

    [Fact]
    public void ValidateTool_EnforceMinimumDisabled_ToolBelowMinimumIsValid()
    {
        var config = new HarvestingConfig
        {
            ToolRequirements = new ToolRequirementsConfig { EnforceMinimum = false, AllowNoToolForCommon = true }
        };
        var service = CreateService(config);
        var result = service.ValidateTool(MakeNode("rare", minToolTier: 5), toolTier: 1, toolType: null);
        result.IsValid.Should().BeTrue();
    }

    // GetToolTierFromItem

    [Theory]
    [InlineData("bronze_pickaxe", 1)]
    [InlineData("basic_axe", 1)]
    [InlineData("crude_sickle", 1)]
    [InlineData("iron_pickaxe", 2)]
    [InlineData("quality_tool", 2)]
    [InlineData("steel_axe", 3)]
    [InlineData("master_pick", 3)]
    [InlineData("mithril_pickaxe", 4)]
    [InlineData("enchanted_axe", 4)]
    [InlineData("adamantine_pick", 5)]
    [InlineData("legendary_tool", 5)]
    [InlineData("ancient_axe", 5)]
    public void GetToolTierFromItem_KnownPatterns_ReturnsExpectedTier(string itemRef, int expected)
    {
        var service = CreateService();
        service.GetToolTierFromItem(itemRef).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    public void GetToolTierFromItem_NullOrEmpty_ReturnsZero(string? itemRef, int expected)
    {
        var service = CreateService();
        service.GetToolTierFromItem(itemRef).Should().Be(expected);
    }

    [Fact]
    public void GetToolTierFromItem_UnknownName_ReturnsTierOne()
    {
        var service = CreateService();
        service.GetToolTierFromItem("mystery_tool").Should().Be(1);
    }

    // GetToolTypeFromItem

    [Theory]
    [InlineData("bronze_pickaxe", "pickaxe")]
    [InlineData("iron_pick", "pickaxe")]
    [InlineData("steel_axe", "axe")]
    [InlineData("iron_hatchet", "axe")]
    [InlineData("bronze_sickle", "sickle")]
    [InlineData("iron_scythe", "sickle")]
    [InlineData("fishing_rod", "fishing rod")]
    [InlineData("skinning_knife", "skinning knife")]
    public void GetToolTypeFromItem_KnownPatterns_ReturnsExpectedType(string itemRef, string expected)
    {
        var service = CreateService();
        service.GetToolTypeFromItem(itemRef).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetToolTypeFromItem_NullOrEmpty_ReturnsNull(string? itemRef)
    {
        var service = CreateService();
        service.GetToolTypeFromItem(itemRef).Should().BeNull();
    }

    [Fact]
    public void GetToolTypeFromItem_UnknownPattern_ReturnsFallback()
    {
        var service = CreateService();
        service.GetToolTypeFromItem("mystery_item").Should().Be("tool");
    }
}
