using FluentAssertions;
using Veldrath.Discord.Features;
using Veldrath.Discord.Tests.Infrastructure;

namespace Veldrath.Discord.Tests.Features;

/// <summary>Tests for the <see cref="PingModule"/> slash command body.</summary>
public class PingModuleTests
{
    [Fact]
    public async Task PingAsync_RepliesWithTheGatewayLatency()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new PingModule());

        await module.PingAsync();

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("RespondAsync");
        transcript.Deferred.Should().BeFalse();
        transcript.Text.Should().StartWith("Pong! Gateway latency: **").And.EndWith(" ms**");
        transcript.Embed.Should().BeNull();
    }
}
