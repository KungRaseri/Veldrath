using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class HarvestingConfigServiceTests
{
    // ── Stub infrastructure ────────────────────────────────────────────────

    private sealed class StubConfigService(string? json) : GameConfigService
    {
        public override string? GetData(string key) => json;
    }

    private sealed class CallCountingConfigService(string? json, Action onCall) : GameConfigService
    {
        public override string? GetData(string key) { onCall(); return json; }
    }

    private static HarvestingConfigService CreateService(string? json = null) =>
        new(new StubConfigService(json), NullLogger<HarvestingConfigService>.Instance);

    private const string ValidJson = """
        {
          "nodeHealth": {
            "baseDepletion": 30,
            "healthThresholds": { "healthy": 0.9, "depleted": 0.5, "exhausted": 0.2 },
            "respawnRate": 5
          },
          "yieldCalculation": {
            "skillScaling": 0.005,
            "toolBonusPerTier": 0.15,
            "maxToolBonus": 0.45,
            "criticalMultiplier": 3.0,
            "exhaustedPenalty": 0.3
          },
          "criticalHarvest": {
            "baseChance": 0.10,
            "skillScaling": 0.001,
            "toolBonusPerTier": 0.02,
            "richNodeBonus": 0.08,
            "bonusMaterialChance": 0.30,
            "rareDropChance": 0.15,
            "durabilityReduction": 0.4,
            "xpBonus": 2.0
          },
          "toolRequirements": {
            "enforceMinimum": false,
            "noToolPenalty": 0.6,
            "noToolDepletionMultiplier": 3.0,
            "allowNoToolForCommon": false
          },
          "skillXP": {
            "baseXP": 20,
            "toolQualityBonus": 0.2,
            "tierMultipliers": { "common": 1.5, "rare": 4.0 }
          },
          "durabilityLoss": {
            "baseLoss": 2,
            "nodeResistance": { "rock": 1.5 },
            "toolHardness": { "iron": 0.8 }
          }
        }
        """;

    // ── LoadConfig — defaults ──────────────────────────────────────────────

    [Fact]
    public void LoadConfig_WhenConfigNotInDb_ReturnsDefaultValues()
    {
        var config = CreateService(json: null).LoadConfig();

        config.BaseDepletion.Should().Be(20);
        config.HealthyThreshold.Should().Be(0.8);
        config.DepletedThreshold.Should().Be(0.4);
        config.ExhaustedThreshold.Should().Be(0.1);
        config.RespawnRate.Should().Be(1);
        config.EnforceMinimumTool.Should().BeTrue();
        config.AllowNoToolForCommon.Should().BeTrue();
        config.BaseXP.Should().Be(10);
        config.BaseDurabilityLoss.Should().Be(1);
    }

    // ── LoadConfig — JSON mapping ──────────────────────────────────────────

    [Fact]
    public void LoadConfig_ValidJson_MapsNodeHealthSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.BaseDepletion.Should().Be(30);
        config.HealthyThreshold.Should().Be(0.9);
        config.DepletedThreshold.Should().Be(0.5);
        config.ExhaustedThreshold.Should().Be(0.2);
        config.RespawnRate.Should().Be(5);
    }

    [Fact]
    public void LoadConfig_ValidJson_MapsYieldCalculationSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.SkillScaling.Should().Be(0.005);
        config.ToolBonusPerTier.Should().Be(0.15);
        config.MaxToolBonus.Should().Be(0.45);
        config.CriticalMultiplier.Should().Be(3.0);
        config.ExhaustedPenalty.Should().Be(0.3);
    }

    [Fact]
    public void LoadConfig_ValidJson_MapsCriticalHarvestSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.CriticalBaseChance.Should().Be(0.10);
        config.CriticalSkillScaling.Should().Be(0.001);
        config.BonusMaterialChance.Should().Be(0.30);
        config.RareDropChance.Should().Be(0.15);
        config.CriticalXPBonus.Should().Be(2.0);
    }

    [Fact]
    public void LoadConfig_ValidJson_MapsToolRequirementsSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.EnforceMinimumTool.Should().BeFalse();
        config.NoToolPenalty.Should().Be(0.6);
        config.NoToolDepletionMultiplier.Should().Be(3.0);
        config.AllowNoToolForCommon.Should().BeFalse();
    }

    [Fact]
    public void LoadConfig_ValidJson_MapsSkillXPSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.BaseXP.Should().Be(20);
        config.ToolQualityBonus.Should().Be(0.2);
        config.TierXPMultipliers.Should().ContainKey("common").WhoseValue.Should().Be(1.5);
        config.TierXPMultipliers.Should().ContainKey("rare").WhoseValue.Should().Be(4.0);
    }

    [Fact]
    public void LoadConfig_ValidJson_MapsDurabilityLossSection()
    {
        var config = CreateService(ValidJson).LoadConfig();

        config.BaseDurabilityLoss.Should().Be(2);
        config.NodeResistance.Should().ContainKey("rock").WhoseValue.Should().Be(1.5);
        config.ToolHardness.Should().ContainKey("iron").WhoseValue.Should().Be(0.8);
    }

    // ── LoadConfig — caching ───────────────────────────────────────────────

    [Fact]
    public void LoadConfig_CachesResult_OnSubsequentCalls()
    {
        var callCount = 0;
        var svc = new HarvestingConfigService(
            new CallCountingConfigService(ValidJson, () => callCount++),
            NullLogger<HarvestingConfigService>.Instance);

        svc.LoadConfig();
        svc.LoadConfig();

        callCount.Should().Be(1, "config data should only be loaded once");
    }

    [Fact]
    public void ClearCache_ForcesReload_OnNextCall()
    {
        var callCount = 0;
        var svc = new HarvestingConfigService(
            new CallCountingConfigService(ValidJson, () => callCount++),
            NullLogger<HarvestingConfigService>.Instance);

        svc.LoadConfig();
        svc.ClearCache();
        svc.LoadConfig();

        callCount.Should().Be(2, "config should be reloaded after cache is cleared");
    }

    // ── LoadConfig — error handling ────────────────────────────────────────

    [Fact]
    public void LoadConfig_InvalidJson_FallsBackToDefaults()
    {
        var config = CreateService("{not valid json!!").LoadConfig();

        // Falls back to type defaults — spot-check a few representative values
        config.BaseDepletion.Should().Be(20);
        config.BaseXP.Should().Be(10);
        config.EnforceMinimumTool.Should().BeTrue();
    }
}
