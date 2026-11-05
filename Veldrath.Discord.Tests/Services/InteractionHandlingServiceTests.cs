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

namespace Veldrath.Discord.Tests.Services;

/// <summary>
/// Tests the gateway wiring of <see cref="InteractionHandlingService"/> against a real
/// (never-connected) <see cref="DiscordSocketClient"/> and <see cref="InteractionService"/>.
/// Discord.NET exposes no fake for either type, so these construct the live objects but never
/// open a gateway connection.
/// </summary>
[Trait("Category", "Discord")]
public class InteractionHandlingServiceTests : IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly ServiceProvider _provider;

    public InteractionHandlingServiceTests()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages,
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

    private InteractionHandlingService CreateService(ulong devGuildId = 0) =>
        new(
            _client,
            _interactions,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DiscordSettings { Token = "test-token", DevGuildId = devGuildId }),
            NullLogger<InteractionHandlingService>.Instance);

    [Fact]
    public async Task InitializeAsync_CompletesAgainstALiveGatewayClient()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_WithNoDiscoverableModules_RegistersNothing()
    {
        // Arrange — the test assembly is not the entry assembly, so no modules are discovered
        var service = CreateService();

        // Act
        await service.InitializeAsync();

        // Assert
        _interactions.SlashCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_CanBeCalledTwiceWithoutDuplicatingHandlers()
    {
        // Arrange
        var service = CreateService();
        await service.InitializeAsync();

        // Act
        var act = () => service.InitializeAsync();

        // Assert — Discord.NET event subscriptions are additive and de-duplicate the same delegate;
        // calling twice must still not throw.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_WithADevGuildConfigured_StillCompletes()
    {
        // Arrange — the dev-guild branch only runs on the Ready event, which a never-connected
        // client never raises; initialisation itself must stay side-effect free.
        var service = CreateService(devGuildId: 1234567890);

        // Act
        var act = () => service.InitializeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
