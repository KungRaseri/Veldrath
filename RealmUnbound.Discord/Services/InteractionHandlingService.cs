using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using RealmUnbound.Discord.Settings;
using System.Reflection;

namespace RealmUnbound.Discord.Services;

/// <summary>
/// Discovers and registers all <see cref="InteractionModuleBase{T}"/> modules in this assembly,
/// then routes incoming <see cref="SocketInteraction"/> events to the correct handler.
/// </summary>
internal sealed class InteractionHandlingService(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IOptions<DiscordSettings> settings,
    ILogger<InteractionHandlingService> logger)
{
    /// <summary>
    /// Scans the entry assembly for interaction modules and wires up the relevant
    /// <see cref="DiscordSocketClient"/> events.
    /// Call once during <c>IHostedService.StartAsync</c>.
    /// </summary>
    public async Task InitializeAsync()
    {
        await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services);

        client.Ready             += RegisterCommandsAsync;
        client.InteractionCreated += HandleInteractionAsync;

        interactions.Log += message =>
        {
            logger.LogDebug("[Interactions] {Source}: {Message}", message.Source, message.Message);
            return Task.CompletedTask;
        };
    }

    private async Task RegisterCommandsAsync()
    {
        if (settings.Value.DevGuildId != 0)
        {
            // Guild commands update instantly — ideal during development.
            await interactions.RegisterCommandsToGuildAsync(settings.Value.DevGuildId);
            logger.LogInformation("Slash commands registered to dev guild {GuildId}", settings.Value.DevGuildId);
        }
        else
        {
            // Global commands propagate in up to 1 hour — use in production.
            await interactions.RegisterCommandsGloballyAsync();
            logger.LogInformation("Slash commands registered globally");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var ctx    = new SocketInteractionContext(client, interaction);
        var result = await interactions.ExecuteCommandAsync(ctx, services);

        if (!result.IsSuccess)
            logger.LogWarning("Interaction failed: {Error}", result.ErrorReason);
    }
}
