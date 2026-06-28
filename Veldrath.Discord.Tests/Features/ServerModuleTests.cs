using System.Net;
using System.Text.Json;
using Discord;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Veldrath.Contracts.Zones;
using Veldrath.Discord.Features;
using Veldrath.Discord.Services;
using Veldrath.Discord.Tests.Services;
using Xunit;

namespace Veldrath.Discord.Tests.Features;

/// <summary>Tests for <see cref="ServerModule"/> slash commands.</summary>
public class ServerModuleTests
{
    private static readonly Color Gold = new(0xFFAA00);

    private static readonly List<ZoneDto> SampleZones =
    [
        new("crestfall", "Crestfall", "A town", "Town", 0, 50, true, 3, "varenmark", true, true),
        new("the-droveway", "The Droveway", "A road", "Wilderness", 1, 10, false, 1, "varenmark", false, false)
    ];

    private static readonly List<ZoneDto> ManyZones = Enumerable
        .Range(1, 25)
        .Select(i => new ZoneDto(
            $"zone-{i}", $"Zone {i}", $"Description {i}", "Wilderness",
            1, 10, false, i % 5, "varenmark", false, false))
        .ToList();

    // ──────────────────────────────────────────────
    //  BuildStatusEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildStatusEmbed_WhenZonesReturned_HasOnlineTitleAndGoldColor()
    {
        // Act
        var embed = ServerModule.BuildStatusEmbed(SampleZones, Gold);

        // Assert
        embed.Title.Should().Be("⚔️ Realm Unbound — Status");
        embed.Description.Should().Be("The realm stands. Adventurers are abroad.");
        embed.Color.Should().NotBeNull();
        embed.Color.Value.R.Should().Be(0xFF);
        embed.Color.Value.G.Should().Be(0xAA);
        embed.Color.Value.B.Should().Be(0x00);
    }

    [Fact]
    public void BuildStatusEmbed_WhenZonesReturned_ShowsPlayerAndZoneCounts()
    {
        // Act
        var embed = ServerModule.BuildStatusEmbed(SampleZones, Gold);

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Status" && f.Value == "🟢 Online");
        embed.Fields.Should().Contain(f => f.Name == "Wanderers" && f.Value == "4");
        embed.Fields.Should().Contain(f => f.Name == "Active Zones" && f.Value == "2");
    }

    [Fact]
    public void BuildStatusEmbed_WhenNull_ShowsOfflineEmbed()
    {
        // Act
        var embed = ServerModule.BuildStatusEmbed(null, Gold);

        // Assert
        embed.Title.Should().Be("⚔️ Realm Unbound");
        embed.Description.Should().Be("🔴 **The realm is unreachable.** The server may be offline.");
        embed.Color.Should().NotBeNull();
        // Discord.Net's Color.Red is #E74C3C, not pure 0xFF0000
        embed.Color.Value.R.Should().Be(0xE7);
        embed.Color.Value.G.Should().Be(0x4C);
        embed.Color.Value.B.Should().Be(0x3C);
    }

    [Fact]
    public void BuildStatusEmbed_WhenNoZones_ShowsNoneForWanderers()
    {
        // Act
        var embed = ServerModule.BuildStatusEmbed([], Gold);

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Wanderers" && f.Value == "None");
        embed.Fields.Should().Contain(f => f.Name == "Active Zones" && f.Value == "0");
    }

    // ──────────────────────────────────────────────
    //  BuildZonesEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildZonesEmbed_WithZones_ShowsZoneList()
    {
        // Act
        var embed = ServerModule.BuildZonesEmbed(SampleZones);

        // Assert
        embed.Title.Should().Be("🗺️ Realm Zones");
        embed.Fields.Length.Should().Be(2);
        embed.Fields[0].Name.Should().Be("Crestfall");
        embed.Fields[1].Name.Should().Be("The Droveway");
    }

    [Fact]
    public void BuildZonesEmbed_WithManyZones_ShowsLimitedFooter()
    {
        // Act
        var embed = ServerModule.BuildZonesEmbed(ManyZones);

        // Assert
        embed.Fields.Length.Should().Be(20);
        embed.Footer.Should().NotBeNull();
        embed.Footer.Value.Text.Should().Contain("Showing 20 of 25");
    }

    [Fact]
    public void BuildZonesEmbed_WithFewZones_ShowsTotalFooter()
    {
        // Act
        var embed = ServerModule.BuildZonesEmbed(SampleZones);

        // Assert
        embed.Footer.Should().NotBeNull();
        embed.Footer.Value.Text.Should().Be("2 zones total");
    }

    // ──────────────────────────────────────────────
    //  Integration: ServerStatusService → Module
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ServerStatusService_GetZonesAsync_Returns_Deserialized_Zones()
    {
        // Arrange
        var json = JsonSerializer.Serialize(SampleZones);
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var service = new ServerStatusService(httpClient);

        // Act
        var result = await service.GetZonesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Id.Should().Be("crestfall");
        result![1].Id.Should().Be("the-droveway");
    }
}
