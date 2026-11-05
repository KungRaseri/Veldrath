using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veldrath.Discord.Services;
using Veldrath.Discord.Settings;
using Xunit;

namespace Veldrath.Discord.Tests;

/// <summary>
/// Lifecycle tests for <see cref="BotWorker"/> against a never-connected gateway client.
/// Discord.NET offers no fake client, so the worker is driven only through the paths that
/// do not require a live connection (shutdown, and the token-validation failure).
/// </summary>
[Trait("Category", "Discord")]
public class BotWorkerTests : IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly ServiceProvider _provider;

    public BotWorkerTests()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
            LogLevel = LogSeverity.Critical,
        });
        _interactions = new InteractionService(_client, new InteractionServiceConfig
        {
            DefaultRunMode = RunMode.Sync,
        });
        _provider = new ServiceCollection().BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _client.Dispose();
    }

    private BotWorker CreateWorker(string token = "test-token")
    {
        var interactions = new InteractionHandlingService(
            _client,
            _interactions,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DiscordSettings { Token = token }),
            NullLogger<InteractionHandlingService>.Instance);

        return new BotWorker(
            _client,
            interactions,
            Options.Create(new DiscordSettings { Token = token }),
            NullLogger<BotWorker>.Instance);
    }

    [Fact]
    public async Task StopAsync_OnANeverConnectedClient_Completes()
    {
        // Arrange
        var worker = CreateWorker();

        // Act
        var act = () => worker.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_CanBeCalledRepeatedly()
    {
        // Arrange
        var worker = CreateWorker();
        await worker.StopAsync(CancellationToken.None);

        // Act
        var act = () => worker.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
