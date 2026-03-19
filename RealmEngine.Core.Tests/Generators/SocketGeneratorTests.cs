using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

[Trait("Category", "Generators")]
public class SocketGeneratorTests
{
    private static SocketGenerator CreateGenerator(SocketConfig? config = null) =>
        new(config ?? new SocketConfig(), NullLogger<SocketGenerator>.Instance);

    // ── Fallback (empty config) ────────────────────────────────────────────

    [Theory]
    [InlineData(ItemRarity.Common)]
    [InlineData(ItemRarity.Uncommon)]
    public void GenerateSockets_BelowRareTier_EmptyConfig_ReturnsNoSockets(ItemRarity rarity)
    {
        // Fallback: rarity < Rare → 0 sockets
        var result = CreateGenerator().GenerateSockets(rarity, ItemType.Weapon, null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSockets_RareTier_EmptyConfig_AlwaysReturnsAtLeastOneSocket()
    {
        // Fallback: rarity >= Rare → Random.Next(1, 3) = 1 or 2 sockets
        var generator = CreateGenerator();
        for (int i = 0; i < 20; i++)
        {
            var result = generator.GenerateSockets(ItemRarity.Rare, ItemType.Weapon, null);
            var total = result.Values.Sum(v => v.Count);
            total.Should().BeInRange(1, 2, "rare items always receive 1-2 sockets with no config");
        }
    }

    [Fact]
    public void GenerateSockets_LegendaryTier_EmptyConfig_AlwaysReturnsAtLeastOneSocket()
    {
        var generator = CreateGenerator();
        for (int i = 0; i < 20; i++)
        {
            var result = generator.GenerateSockets(ItemRarity.Legendary, ItemType.Weapon, null);
            var total = result.Values.Sum(v => v.Count);
            total.Should().BeInRange(1, 2);
        }
    }

    // ── Config-driven deterministic counts ────────────────────────────────

    [Fact]
    public void GenerateSockets_ConfigForcesZeroSockets_ReturnsEmpty()
    {
        // Chances[0]=100 → cumulative=100 on first iteration → returns index 0 = 0 sockets
        var config = new SocketConfig
        {
            SocketCounts = new Dictionary<string, SocketCountConfig>
            {
                ["Rare"] = new SocketCountConfig { Chances = [100, 0, 0] }
            }
        };
        var result = CreateGenerator(config).GenerateSockets(ItemRarity.Rare, ItemType.Weapon, null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSockets_ConfigForcesOneSocket_ReturnsTotalOfOne()
    {
        // [0, 100, 0] → cumulative reaches 100 at index 1 → returns 1 socket
        var config = new SocketConfig
        {
            SocketCounts = new Dictionary<string, SocketCountConfig>
            {
                ["Rare"] = new SocketCountConfig { Chances = [0, 100, 0] }
            }
        };
        var result = CreateGenerator(config).GenerateSockets(ItemRarity.Rare, ItemType.Weapon, null);
        result.Values.Sum(v => v.Count).Should().Be(1);
    }

    [Fact]
    public void GenerateSockets_ConfigForcesTwoSockets_ReturnsTotalOfTwo()
    {
        // [0, 0, 100] → cumulative reaches 100 at index 2 → returns 2 sockets
        var config = new SocketConfig
        {
            SocketCounts = new Dictionary<string, SocketCountConfig>
            {
                ["Epic"] = new SocketCountConfig { Chances = [0, 0, 100] }
            }
        };
        var result = CreateGenerator(config).GenerateSockets(ItemRarity.Epic, ItemType.Weapon, null);
        result.Values.Sum(v => v.Count).Should().Be(2);
    }

    // ── Return type shape ─────────────────────────────────────────────────

    [Fact]
    public void GenerateSockets_ReturnsOnlyKnownSocketTypes()
    {
        var config = new SocketConfig
        {
            SocketCounts = new Dictionary<string, SocketCountConfig>
            {
                ["Legendary"] = new SocketCountConfig { Chances = [0, 0, 100] }
            }
        };
        var result = CreateGenerator(config).GenerateSockets(ItemRarity.Legendary, ItemType.Weapon, null);
        result.Keys.Should().OnlyContain(t =>
            t == SocketType.Gem || t == SocketType.Rune ||
            t == SocketType.Crystal || t == SocketType.Orb);
    }

    [Fact]
    public void GenerateSockets_AllSocketsHaveCorrectType()
    {
        var config = new SocketConfig
        {
            SocketCounts = new Dictionary<string, SocketCountConfig>
            {
                ["Rare"] = new SocketCountConfig { Chances = [0, 100, 0] }
            }
        };
        var result = CreateGenerator(config).GenerateSockets(ItemRarity.Rare, ItemType.Weapon, null);
        foreach (var (type, sockets) in result)
            sockets.Should().AllSatisfy(s => s.Type.Should().Be(type));
    }
}
