using RealmEngine.Core.Services;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Covers <see cref="CharacterGrowthService"/>'s JSON configuration pipeline: parsing
/// growth-stats into derived-stat formulas, soft/hard cap tiers (including non-numeric extension
/// data), class growth multipliers and the respec switch, plus the invalid/partial/missing fallbacks.
/// </summary>
[Trait("Category", "Service")]
public class CharacterGrowthServiceConfigTests
{
    private sealed class StubGameConfigService : GameConfigService
    {
        private readonly string? _payload;

        public StubGameConfigService(string? payload) => _payload = payload;

        public int Reads { get; private set; }

        public override string? GetData(string key)
        {
            Reads++;
            return key == "growth-stats" ? _payload : null;
        }
    }

    private const string FullConfigJson = """
    {
      "metadata": { "version": "5.2" },
      "derivedStats": {
        "health": { "formula": "base + con*10", "description": "Total HP" },
        "mana": { "formula": "base + int*5", "description": "Total mana" },
        "physicaldamage": { "formula": "str*0.5", "description": "Physical" },
        "magicaldamage": { "formula": "int*0.5", "description": "Magical" },
        "criticalchance": { "formula": "0.05 + dex*0.001", "description": "Crit" },
        "criticaldamage": { "formula": "1.5 + dex*0.002", "description": "Crit dmg" },
        "armor": { "formula": "con*2", "description": "Armor" },
        "magicresist": { "formula": "wis*2", "description": "Resist" },
        "dodge": { "formula": "0.05 + dex*0.001", "description": "Dodge" },
        "block": { "formula": "0.05 + con*0.0005", "description": "Block" }
      },
      "statCaps": {
        "softCaps": {
          "description": "Diminishing returns",
          "diminishingFactor": 0.25,
          "strength": 100,
          "dexterity": "not-a-number"
        },
        "hardCaps": {
          "description": "Maximum values",
          "strength": 200
        }
      },
      "classGrowthMultipliers": {
        "growth": [
          { "classRef": "@classes/warrior:Fighter", "multipliers": { "health": 1.5, "mana": 0.5 } },
          { "classRef": "", "multipliers": { "health": 9.9 } }
        ]
      },
      "statPointAllocation": {
        "pointsPerLevel": 6,
        "description": "Six per level",
        "minimumPerStat": 1,
        "maximumPerStat": 4
      },
      "respecSystem": {
        "enabled": false,
        "costFormula": "level * 50",
        "description": "Disabled"
      }
    }
    """;

    private static CharacterGrowthService CreateService(string? json, out StubGameConfigService config)
    {
        config = new StubGameConfigService(json);
        return new CharacterGrowthService(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<CharacterGrowthService>.Instance);
    }

    private static CharacterGrowthService CreateService(string? json) => CreateService(json, out _);

    [Fact]
    public void GetClassMultipliers_ResolvesJsonReferenceCaseInsensitively()
    {
        var service = CreateService(FullConfigJson);

        var multipliers = service.GetClassMultipliers("@CLASSES/WARRIOR:FIGHTER");

        multipliers.Should().NotBeNull();
        multipliers!.ClassRef.Should().Be("@classes/warrior:Fighter");
        multipliers.Multipliers["health"].Should().Be(1.5);
        service.GetClassMultipliers("@classes/rogue:Rogue").Should().BeNull();
    }

    [Fact]
    public void GetClassMultipliers_SkipsEntriesWithoutAClassReference()
    {
        var service = CreateService(FullConfigJson);

        var all = service.GetClassMultipliers("");

        all.Should().BeNull("growth entries with an empty classRef are dropped while parsing");
    }

    [Theory]
    [InlineData("health", 50, 15, 2, 206)]
    [InlineData("mana", 50, 15, 2, 131)]
    [InlineData("physicaldamage", 1, 20, 1, 10)]
    [InlineData("magicaldamage", 1, 20, 1, 10)]
    [InlineData("criticalchance", 0, 20, 1, 0.07)]
    [InlineData("criticaldamage", 0, 20, 1, 1.54)]
    [InlineData("armor", 0, 20, 1, 40)]
    [InlineData("magicresist", 0, 15, 1, 30)]
    [InlineData("dodge", 0, 20, 1, 0.07)]
    [InlineData("block", 0, 20, 1, 0.06)]
    public void CalculateDerivedStat_EvaluatesEachFormula(
        string statName, double baseValue, double primaryStat, int level, double expected)
    {
        var service = CreateService(FullConfigJson);

        var result = service.CalculateDerivedStat(statName, baseValue, primaryStat, level, classGrowthValue: 3.0);

        result.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void CalculateDerivedStat_UnknownStat_ReturnsBaseValue()
    {
        var service = CreateService(FullConfigJson);

        service.CalculateDerivedStat("charisma", baseValue: 12, primaryStat: 99, level: 5, classGrowthValue: 2).Should().Be(12);
    }

    [Fact]
    public void CalculateDerivedStat_ConfiguredButUnmappedStat_ReturnsBaseValue()
    {
        var service = CreateService("""
        { "derivedStats": { "speed": { "formula": "dex", "description": "Move speed" } } }
        """);

        service.CalculateDerivedStat("speed", baseValue: 7, primaryStat: 50, level: 3, classGrowthValue: 1).Should().Be(7);
    }

    [Fact]
    public void ApplySoftCap_BelowCap_ReturnsValueUnchanged()
    {
        var service = CreateService(FullConfigJson);

        service.ApplySoftCap("strength", 80).Should().Be(80);
    }

    [Fact]
    public void ApplySoftCap_AboveCap_AppliesConfiguredDiminishingFactor()
    {
        var service = CreateService(FullConfigJson);

        // cap 100, factor 0.25: 100 + (200 - 100) * 0.25
        service.ApplySoftCap("strength", 200).Should().Be(125);
    }

    [Fact]
    public void ApplySoftCap_IgnoresNonNumericExtensionDataAndUnknownStats()
    {
        var service = CreateService(FullConfigJson);

        service.ApplySoftCap("dexterity", 500).Should().Be(500, "a string-valued cap limit is ignored while parsing");
        service.ApplySoftCap("charisma", 500).Should().Be(500, "unlisted stats have no soft cap");
    }

    [Fact]
    public void ApplyHardCap_ClampsAtConfiguredCap()
    {
        var service = CreateService(FullConfigJson);

        service.ApplyHardCap("strength", 500).Should().Be(200);
        service.ApplyHardCap("strength", 150).Should().Be(150);
        service.ApplyHardCap("wisdom", 500).Should().Be(500, "unlisted stats have no hard cap");
    }

    [Fact]
    public void CalculateRespecCost_ReturnsZero_WhenRespecIsDisabled()
    {
        var service = CreateService(FullConfigJson);

        service.CalculateRespecCost(30).Should().Be(0);
    }

    [Fact]
    public void CalculateRespecCost_ScalesWithLevel_WhenRespecIsEnabled()
    {
        var service = CreateService(null);

        service.CalculateRespecCost(30).Should().Be(3000);
    }

    [Fact]
    public void LoadConfig_WithDefaultFallback_UsesDocumentedCaps()
    {
        var service = CreateService(null);

        service.ApplySoftCap("strength", 200).Should().Be(150, "default soft cap 100 with a 0.5 diminishing factor");
        service.ApplyHardCap("strength", 500).Should().Be(200, "default hard cap is 200");
        service.CalculateRespecCost(2).Should().Be(200);
    }

    [Fact]
    public void LoadConfig_WithInvalidJson_FallsBackToDefaults()
    {
        var service = CreateService("{ not valid json ");

        service.CalculateRespecCost(1).Should().Be(100);
        service.ApplyHardCap("constitution", 900).Should().Be(200);
    }

    [Fact]
    public void LoadConfig_WithPartialJson_SubstitutesDefaultsForMissingSections()
    {
        var service = CreateService("""
        { "metadata": { "version": "9.9" } }
        """);

        service.ApplySoftCap("strength", 200).Should().Be(150, "missing statCaps falls back to the default cap tier");
        service.ApplyHardCap("wisdom", 1000).Should().Be(200);
        service.CalculateRespecCost(4).Should().Be(400, "missing respecSystem defaults to enabled");
    }

    [Fact]
    public void LoadConfig_WithStatCapsButNoTiers_FallsBackToDefaultTiers()
    {
        var service = CreateService("""
        { "statCaps": { } }
        """);

        service.ApplySoftCap("strength", 200).Should().Be(200, "an empty statCaps object parses to tiers with no limits");
        service.ApplyHardCap("strength", 500).Should().Be(500, "an empty statCaps object parses to tiers with no limits");
    }

    [Fact]
    public void ConfigIsCached_UntilClearCacheIsCalled()
    {
        var service = CreateService(FullConfigJson, out var config);

        service.ApplyHardCap("strength", 100);
        service.ApplyHardCap("strength", 100);
        config.Reads.Should().Be(1, "the parsed configuration is cached on the service");

        service.ClearCache();
        service.ApplyHardCap("strength", 100);
        config.Reads.Should().Be(2, "ClearCache forces a reload on next access");
    }

    [Fact]
    public void GetClassMultipliers_WithNoJson_UsesDefaultEmptyList()
    {
        var service = CreateService(null);

        service.GetClassMultipliers("@classes/warrior:Fighter").Should().BeNull();
    }
}
