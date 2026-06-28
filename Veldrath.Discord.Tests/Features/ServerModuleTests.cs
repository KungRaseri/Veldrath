using System.Net;
using System.Reflection;
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
    /// mocked interaction context.
    /// </summary>
    private static ServerModule CreateModule(string content, HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(content, statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var statusService = new ServerStatusService(httpClient);
        return new ServerModule(statusService);
    }

    /// <summary>
    /// Creates a substitute <see cref="SocketInteraction"/> with abstract members stubbed
    /// and <see cref="IDiscordInteraction.DeferAsync"/> / <see cref="IDiscordInteraction.FollowupAsync"/>
    /// set up as no-ops.
    /// </summary>
    private static SocketInteraction CreateMockInteraction()
    {
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

        // Stub abstract members so the proxy doesn't throw
        interaction.User.Returns(Substitute.For<SocketUser>());
        interaction.Channel.Returns(Substitute.For<ISocketMessageChannel>());
        interaction.Data.Returns(Substitute.For<IDiscordInteractionData>());

        return interaction;
    }

    /// <summary>
    /// Uses reflection to set the <see cref="InteractionModuleBase{T}.Context"/>
    /// property, which has an <c>internal</c> setter.
    /// </summary>
    private static void SetModuleContext<TModule>(TModule module, SocketInteractionContext context)
        where TModule : InteractionModuleBase<SocketInteractionContext>
    {
        var prop = typeof(InteractionModuleBase<SocketInteractionContext>)
            .GetProperty("Context", BindingFlags.Public | BindingFlags.Instance)!;
        var setter = prop.GetSetMethod(true); // non-public
        setter!.Invoke(module, [context]);
    }

    /// <summary>
    /// Sets up the module with a mocked interaction and returns the interaction
    /// so the test can assert on calls.
    /// </summary>
    private static SocketInteraction ArrangeModule(ServerModule module)
    {
        var interaction = CreateMockInteraction();
        var client = Substitute.For<DiscordSocketClient>();
        var context = new SocketInteractionContext(client, interaction);
        SetModuleContext(module, context);
        return interaction;
    }

    // ──────────────────────────────────────────────
    //  /status
    // ──────────────────────────────────────────────

    [Fact]
    public async Task StatusAsync_WhenZonesReturned_ShowsOnlineEmbed()
    {
        // Arrange
        var module = CreateModule(SampleJson, HttpStatusCode.OK);
        var interaction = ArrangeModule(module);

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.StatusAsync();

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Be("⚔️ Realm Unbound — Status");
        captured.Color.Should().NotBeNull();
        captured.Color.Value.R.Should().Be(0xFF);
        captured.Color.Value.G.Should().Be(0xAA);
        captured.Color.Value.B.Should().Be(0x00);
    }

    [Fact]
    public async Task StatusAsync_WhenServerOffline_ShowsOfflineEmbed()
    {
        // Arrange
        var module = CreateModule(string.Empty, HttpStatusCode.InternalServerError);
        var interaction = ArrangeModule(module);

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.StatusAsync();

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Be("⚔️ Realm Unbound");
        captured.Color.Should().NotBeNull();
        captured.Color.Value.R.Should().Be(0xFF);
        captured.Color.Value.G.Should().Be(0x00);
        captured.Color.Value.B.Should().Be(0x00);
    }

    [Fact]
    public async Task StatusAsync_WhenNoZones_ShowsZeroStats()
    {
        // Arrange
        var module = CreateModule(EmptyZonesJson, HttpStatusCode.OK);
        var interaction = ArrangeModule(module);

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.StatusAsync();

        // Assert
        captured.Should().NotBeNull();
        captured!.Fields.Should()
            .Contain(f => f.Name == "Wanderers" && f.Value == "None");
        captured.Fields.Should()
            .Contain(f => f.Name == "Active Zones" && f.Value == "0");
    }

    // ──────────────────────────────────────────────
    //  /zones
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ZonesAsync_WhenZonesReturned_ShowsZoneListEmbed()
    {
        // Arrange
        var module = CreateModule(SampleJson, HttpStatusCode.OK);
        var interaction = ArrangeModule(module);

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ZonesAsync();

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Be("🗺️ Realm Zones");
        captured.Fields.Count(f => !f.Inline).Should().Be(2);
    }

    [Fact]
    public async Task ZonesAsync_WhenServerOffline_ShowsTextMessage()
    {
        // Arrange
        var module = CreateModule(string.Empty, HttpStatusCode.InternalServerError);
        var interaction = ArrangeModule(module);

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ZonesAsync();

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("unreachable");
    }

    [Fact]
    public async Task ZonesAsync_WhenEmpty_ShowsEmptyTextMessage()
    {
        // Arrange
        var module = CreateModule(EmptyZonesJson, HttpStatusCode.OK);
        var interaction = ArrangeModule(module);

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ZonesAsync();

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("No zones");
    }

    [Fact]
    public async Task ZonesAsync_WhenMoreThanTwentyZones_ShowsLimitedFooter()
    {
        // Arrange
        var module = CreateModule(ManyZonesJson, HttpStatusCode.OK);
        var interaction = ArrangeModule(module);

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ZonesAsync();

        // Assert
        captured.Should().NotBeNull();
        captured!.Fields.Length.Should().Be(20);
        captured.Footer.Should().NotBeNull();
        captured.Footer.Value.Text.Should().Contain("Showing 20 of 25");
    }
}
