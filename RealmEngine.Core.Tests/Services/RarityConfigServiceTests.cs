using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class RarityConfigServiceTests
{
    // Local stub — GameConfigService is abstract with no state.
    private sealed class StubConfigService(string? json) : GameConfigService
    {
        public override string? GetData(string key) => json;
    }

    private static RarityConfigService CreateService(string? json = null) =>
        new(new StubConfigService(json), NullLogger<RarityConfigService>.Instance);

    private const string ValidTierJson = """
        {
          "tiers": [
            { "name": "Common",    "rarityWeightRange": { "min": 50, "max": 100 }, "color": "#FFFFFF", "dropChance": 0.50 },
            { "name": "Uncommon",  "rarityWeightRange": { "min": 30, "max": 49  }, "color": "#1EFF00", "dropChance": 0.25 },
            { "name": "Rare",      "rarityWeightRange": { "min": 15, "max": 29  }, "color": "#0070DD", "dropChance": 0.15 },
            { "name": "Epic",      "rarityWeightRange": { "min": 5,  "max": 14  }, "color": "#A335EE", "dropChance": 0.08 },
            { "name": "Legendary", "rarityWeightRange": { "min": 1,  "max": 4   }, "color": "#FF8000", "dropChance": 0.02 }
          ]
        }
        """;

    [Fact]
    public void LoadConfig_ReturnsFiveTiers_WhenConfigServiceReturnsNull()
    {
        var svc = CreateService(json: null);

        var config = svc.LoadConfig();

        config.Tiers.Should().HaveCount(5);
    }

    [Fact]
    public void LoadConfig_ReturnsTiers_WhenValidJsonProvided()
    {
        var svc = CreateService(ValidTierJson);

        var config = svc.LoadConfig();

        config.Tiers.Should().HaveCount(5);
        config.Tiers.Should().Contain(t => t.Name == "Common");
        config.Tiers.Should().Contain(t => t.Name == "Legendary");
    }

    [Fact]
    public void LoadConfig_CachesResult_OnSubsequentCalls()
    {
        int callCount = 0;
        var inner = new CallCountingConfigService(ValidTierJson, () => callCount++);
        var svc = new RarityConfigService(inner, NullLogger<RarityConfigService>.Instance);

        svc.LoadConfig();
        svc.LoadConfig();

        callCount.Should().Be(1, "config data should only be loaded once");
    }

    [Fact]
    public void ClearCache_ForcesReload_OnNextCall()
    {
        int callCount = 0;
        var inner = new CallCountingConfigService(ValidTierJson, () => callCount++);
        var svc = new RarityConfigService(inner, NullLogger<RarityConfigService>.Instance);

        svc.LoadConfig();
        svc.ClearCache();
        svc.LoadConfig();

        callCount.Should().Be(2, "config should be reloaded after cache is cleared");
    }

    [Fact]
    public void GetTierForWeight_ReturnsCommonTier_ForHighRarityWeight()
    {
        var svc = CreateService(ValidTierJson);

        var tier = svc.GetTierForWeight(75);

        tier.Name.Should().Be("Common");
    }

    [Fact]
    public void GetTierForWeight_ReturnsLegendaryTier_ForLowRarityWeight()
    {
        var svc = CreateService(ValidTierJson);

        var tier = svc.GetTierForWeight(2);

        tier.Name.Should().Be("Legendary");
    }

    [Fact]
    public void GetTierForWeight_ReturnsRareTier_ForMidRarityWeight()
    {
        var svc = CreateService(ValidTierJson);

        var tier = svc.GetTierForWeight(20);

        tier.Name.Should().Be("Rare");
    }

    [Fact]
    public void GetColorForWeight_ReturnsExpectedColor_ForCommonWeight()
    {
        var svc = CreateService(ValidTierJson);

        svc.GetColorForWeight(80).Should().Be("#FFFFFF");
    }

    [Fact]
    public void LoadConfig_UsesFallbackDefaults_WhenJsonIsMalformed()
    {
        var svc = CreateService("{ invalid json }");

        // Should not throw — falls back to defaults
        var config = svc.LoadConfig();

        config.Tiers.Should().HaveCount(5);
    }

    // Helper stub that counts how many times GetData is called.
    private sealed class CallCountingConfigService(string? json, Action onGetData) : GameConfigService
    {
        public override string? GetData(string key)
        {
            onGetData();
            return json;
        }
    }
}
