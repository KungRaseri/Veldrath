using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class HarvestingConfigServiceTests
{
    private sealed class StubConfigService(string? json) : GameConfigService
    {
        public override string? GetData(string key) => json;
    }

    private sealed class CallCountingService(string? json, Action onGetData) : GameConfigService
    {
        public override string? GetData(string key) { onGetData(); return json; }
    }

    private static HarvestingConfigService Create(string? json) =>
        new(new StubConfigService(json), NullLogger<HarvestingConfigService>.Instance);

    // Minimal valid JSON that exercises every section.
    private const string ValidJson = """
        {
          "nodeHealth": {
            "baseDepletion": 25,
            "healthThresholds": { "healthy": 0.7, "depleted": 0.3, "exhausted": 0.05 },
            "respawnRate": 5
          },
          "yieldCalculation": {
            "skillScaling": 0.005,
            "toolBonusPerTier": 0.15,
            "maxToolBonus": 0.4,
            "criticalMultiplier": 3.0,
            "exhaustedPenalty": 0.4
          },
          "criticalHarvest": {
            "baseChance": 0.08,
            "skillScaling": 0.001,
            "toolBonusPerTier": 0.02,
            "richNodeBonus": 0.06,
            "bonusMaterialChance": 0.3,
            "rareDropChance": 0.12,
            "durabilityReduction": 0.6,
            "xpBonus": 2.0
          },
          "toolRequirements": {
            "enforceMinimum": false,
            "noToolPenalty": 0.3,
            "noToolDepletionMultiplier": 3.0,
            "allowNoToolForCommon": false
          },
          "skillXP": {
            "baseXP": 20,
            "toolQualityBonus": 0.2,
            "tierMultipliers": { "common": 1.0, "rare": 4.0 }
          },
          "durabilityLoss": {
            "baseLoss": 2,
            "nodeResistance": { "common": 1.1 },
            "toolHardness": { "tier3": 1.2 }
          }
        }
        """;

    // ── Default config (null JSON) ─────────────────────────────────────────────

    [Fact]
    public void LoadConfig_ReturnsDefaults_WhenConfigServiceReturnsNull()
    {
        var config = Create(null).LoadConfig();

        config.BaseDepletion.Should().Be(20);
        config.HealthyThreshold.Should().Be(0.8);
        config.DepletedThreshold.Should().Be(0.4);
        config.ExhaustedThreshold.Should().Be(0.1);
        config.RespawnRate.Should().Be(1);
    }

    [Fact]
    public void LoadConfig_ReturnsDefaults_WhenJsonIsMalformed()
    {
        var config = Create("{ this is not valid json }").LoadConfig();

        config.BaseDepletion.Should().Be(20); // default
    }

    // ── JSON parsing ───────────────────────────────────────────────────────────

    [Fact]
    public void LoadConfig_ParsesNodeHealth_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.BaseDepletion.Should().Be(25);
        config.HealthyThreshold.Should().Be(0.7);
        config.DepletedThreshold.Should().Be(0.3);
        config.ExhaustedThreshold.Should().Be(0.05);
        config.RespawnRate.Should().Be(5);
    }

    [Fact]
    public void LoadConfig_ParsesYieldCalculation_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.SkillScaling.Should().Be(0.005);
        config.ToolBonusPerTier.Should().Be(0.15);
        config.MaxToolBonus.Should().Be(0.4);
        config.CriticalMultiplier.Should().Be(3.0);
        config.ExhaustedPenalty.Should().Be(0.4);
    }

    [Fact]
    public void LoadConfig_ParsesCriticalHarvest_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.CriticalBaseChance.Should().Be(0.08);
        config.BonusMaterialChance.Should().Be(0.3);
        config.CriticalXPBonus.Should().Be(2.0);
    }

    [Fact]
    public void LoadConfig_ParsesToolRequirements_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.EnforceMinimumTool.Should().BeFalse();
        config.NoToolPenalty.Should().Be(0.3);
        config.AllowNoToolForCommon.Should().BeFalse();
    }

    [Fact]
    public void LoadConfig_ParsesSkillXP_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.BaseXP.Should().Be(20);
        config.ToolQualityBonus.Should().Be(0.2);
        config.TierXPMultipliers.Should().ContainKey("rare").WhoseValue.Should().Be(4.0);
    }

    [Fact]
    public void LoadConfig_ParsesDurabilityLoss_FromValidJson()
    {
        var config = Create(ValidJson).LoadConfig();

        config.BaseDurabilityLoss.Should().Be(2);
        config.NodeResistance.Should().ContainKey("common").WhoseValue.Should().Be(1.1);
        config.ToolHardness.Should().ContainKey("tier3").WhoseValue.Should().Be(1.2);
    }

    // ── Caching behaviour ──────────────────────────────────────────────────────

    [Fact]
    public void LoadConfig_CachesResult_OnSubsequentCalls()
    {
        int calls = 0;
        var svc = new HarvestingConfigService(
            new CallCountingService(ValidJson, () => calls++),
            NullLogger<HarvestingConfigService>.Instance);

        svc.LoadConfig();
        svc.LoadConfig();

        calls.Should().Be(1, "config is only parsed once when cached");
    }

    [Fact]
    public void ClearCache_ForcesReload_OnNextCall()
    {
        int calls = 0;
        var svc = new HarvestingConfigService(
            new CallCountingService(ValidJson, () => calls++),
            NullLogger<HarvestingConfigService>.Instance);

        svc.LoadConfig();
        svc.ClearCache();
        svc.LoadConfig();

        calls.Should().Be(2, "config is reloaded after cache is cleared");
    }
}
