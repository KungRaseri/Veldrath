using System.Net;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
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

    private static string SampleJson => JsonSerializer.Serialize(SampleZones);
    private static string ManyZonesJson => JsonSerializer.Serialize(ManyZones);
    private static string EmptyZonesJson => JsonSerializer.Serialize(Array.Empty<ZoneDto>());

    /// <summary>
    /// Creates a <see cref="ServerModule"/> wired to a fake HTTP handler and a
    /// mocked interaction context. Returns the module and the mocked interaction
    /// so the test can assert on the response.
    /// </summary>
    private static (ServerModule Module, SocketInteraction Interaction) CreateModule(
        string content, HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(content, statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var statusService = new ServerStatusService(httpClient);

        var interaction = Substitute.For<SocketInteraction>();
        interaction.DeferAsync(Arg.Any<bool>(), Arg.Any<RequestOptions?>())
            .Returns(Task.CompletedTask);
        interaction.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>())
            .Returns(Task.CompletedTask);

        // Stub abstract members so the proxy doesn't throw when constructed
        interaction.User.Returns(Substitute.For<SocketUser>());
        interaction.Channel.Returns(Substitute.For<ISocketMessageChannel>());
        interaction.Data.Returns(Substitute.For<IDiscordInteractionData>());

        var client = Substitute.For<DiscordSocketClient>();
        var context = new SocketInteractionContext(client, interaction);

        var module = new ServerModule(statusService);
        module.Context = context;

        return (module, interaction);
    }

    // ──────────────────────────────────────────────
    //  /status
    // ──────────────────────────────────────────────

    [Fact]
    public async Task StatusAsync_WhenZonesReturned_ShowsOnlineEmbed()
    {
        // Arrange
        var (module, interaction) = CreateModule(SampleJson, HttpStatusCode.OK);

        // Act
        await module.StatusAsync();

        // Assert
        await interaction.Received(1).DeferAsync(Arg.Any<bool>(), Arg.Any<RequestOptions?>());
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title == "⚔️ Realm Unbound — Status"
                && e.Color?.R == 0xFF
                && e.Color?.G == 0xAA
                && e.Color?.B == 0x00),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    [Fact]
    public async Task StatusAsync_WhenServerOffline_ShowsOfflineEmbed()
    {
        // Arrange — return error status to simulate unreachable server
        var (module, interaction) = CreateModule(string.Empty, HttpStatusCode.InternalServerError);

        // Act
        await module.StatusAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title == "⚔️ Realm Unbound"
                && e.Color?.R == 0xFF
                && e.Color?.G == 0x00
                && e.Color?.B == 0x00),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    [Fact]
    public async Task StatusAsync_WhenNoZones_ShowsZeroStats()
    {
        // Arrange
        var (module, interaction) = CreateModule(EmptyZonesJson, HttpStatusCode.OK);

        // Act
        await module.StatusAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Fields.Any(f => f.Name == "Wanderers" && f.Value == "None")
                && e.Fields.Any(f => f.Name == "Active Zones" && f.Value == "0")),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    // ──────────────────────────────────────────────
    //  /zones
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ZonesAsync_WhenZonesReturned_ShowsZoneListEmbed()
    {
        // Arrange
        var (module, interaction) = CreateModule(SampleJson, HttpStatusCode.OK);

        // Act
        await module.ZonesAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title == "🗺️ Realm Zones"
                && e.Fields.Count(f => f.Inline == false) == 2),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    [Fact]
    public async Task ZonesAsync_WhenServerOffline_ShowsTextMessage()
    {
        // Arrange
        var (module, interaction) = CreateModule(string.Empty, HttpStatusCode.InternalServerError);

        // Act
        await module.ZonesAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("unreachable")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Is<bool>(ephemeral => ephemeral),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    [Fact]
    public async Task ZonesAsync_WhenEmpty_ShowsEmptyTextMessage()
    {
        // Arrange
        var (module, interaction) = CreateModule(EmptyZonesJson, HttpStatusCode.OK);

        // Act
        await module.ZonesAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("No zones")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Is<bool>(ephemeral => ephemeral),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }

    [Fact]
    public async Task ZonesAsync_WhenMoreThanTwentyZones_ShowsLimitedFooter()
    {
        // Arrange
        var (module, interaction) = CreateModule(ManyZonesJson, HttpStatusCode.OK);

        // Act
        await module.ZonesAsync();

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Fields.Count == 20
                && e.Footer?.Text != null
                && e.Footer.Value.Text.Contains("Showing 20 of 25")),
            Arg.Any<RequestOptions?>(),
            Arg.Any<PollProperties?>());
    }
}
