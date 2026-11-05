using System.Net;
using System.Text.Json;
using FluentAssertions;
using Veldrath.Contracts.Zones;
using Veldrath.Discord.Features;
using Veldrath.Discord.Services;
using Veldrath.Discord.Tests.Infrastructure;
using Veldrath.Discord.Tests.Services;

namespace Veldrath.Discord.Tests.Features;

/// <summary>
/// Tests for the <see cref="ServerModule"/> slash command bodies, driven through the real module against a
/// stubbed status endpoint (see <see cref="DiscordInteractionHarness"/>).
/// </summary>
public class ServerModuleCommandTests
{
    private static readonly List<ZoneDto> SampleZones =
    [
        new("crestfall", "Crestfall", "A town", "Town", 0, 50, true, 3, "varenmark", true, true),
        new("the-droveway", "The Droveway", "A road", "Wilderness", 1, 10, false, 1, "varenmark", false, false),
    ];

    private static ServerModule Module(HttpStatusCode status, string json)
    {
        var handler = new FakeHttpMessageHandler(json, status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        return new ServerModule(new ServerStatusService(http));
    }

    [Fact]
    public async Task StatusAsync_RepliesWithTheStatusEmbed()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(HttpStatusCode.OK, JsonSerializer.Serialize(SampleZones)));

        await module.StatusAsync();

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Deferred.Should().BeTrue();
        transcript.DeferEphemeral.Should().BeTrue();
        transcript.Text.Should().BeNull();
        transcript.Embed.Should().NotBeNull();
        transcript.Embed!.Title.Should().Be("⚔️ Realm Unbound — Status");
        transcript.Embed!.Fields.Should().Contain(f => f.Name == "Status" && f.Value == "🟢 Online");
        transcript.Embed!.Fields.Should().Contain(f => f.Name == "Wanderers" && f.Value == "4");
        transcript.Embed!.Fields.Should().Contain(f => f.Name == "Active Zones" && f.Value == "2");
    }

    [Fact]
    public async Task ZonesAsync_WhenTheRealmIsUnreachable_RepliesWithAPlainNotice()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(HttpStatusCode.InternalServerError, "{}"));

        await module.ZonesAsync();

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Embed.Should().BeNull();
        transcript.Text.Should().Be("🔴 The realm server is currently unreachable.");
        transcript.Ephemeral.Should().BeTrue();
    }

    [Fact]
    public async Task ZonesAsync_WhenNoZonesAreConfigured_RepliesWithAPlainNotice()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(HttpStatusCode.OK, "[]"));

        await module.ZonesAsync();

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be("No zones are currently configured in the realm.");
        harness.Transcript.Ephemeral.Should().BeTrue();
    }

    [Fact]
    public async Task ZonesAsync_WhenZonesExist_RepliesWithTheZonesEmbed()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(HttpStatusCode.OK, JsonSerializer.Serialize(SampleZones)));

        await module.ZonesAsync();

        var transcript = harness.Transcript;
        transcript.Embed.Should().NotBeNull();
        transcript.Embed!.Title.Should().Be("🗺️ Realm Zones");
        transcript.Embed!.Fields.Should().HaveCount(2);
        transcript.Embed!.Fields[0].Name.Should().Be("Crestfall");
        transcript.Embed!.Footer!.Value.Text.Should().Be("2 zones total");
    }
}
