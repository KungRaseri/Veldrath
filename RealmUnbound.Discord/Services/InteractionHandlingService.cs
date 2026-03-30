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
    IServiceScopeFactory scopeFactory,
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
        // Create a scope so Discord.NET can instantiate module types during reflection/discovery.
        using (var scope = scopeFactory.CreateScope())
        {
            await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), scope.ServiceProvider);
        }

        var commandCount = interactions.SlashCommands.Count;
        logger.LogInformation("Loaded {Count} slash command(s): {Names}",
            commandCount,
            string.Join(", ", interactions.SlashCommands.Select(c => c.Name)));

        client.Ready              += RegisterCommandsAsync;
        client.InteractionCreated += HandleInteractionAsync;

        interactions.Log += message =>
        {
            // Use Debug for all interaction service log messages — InteractionExecuted handles failures.
            logger.LogDebug("[Interactions] {Source}: {Message}", message.Source, message.Message);
            return Task.CompletedTask;
        };

        interactions.InteractionExecuted += (_, ctx, result) =>
        {
            if (!result.IsSuccess)
            {
                var name = (ctx?.Interaction as ISlashCommandInteraction)?.Data?.Name ?? "unknown";
                var ex   = (result as ExecuteResult?)?.Exception;
                if (ex is not null)
                    logger.LogError(ex, "Interaction '{Name}' failed: [{Error}] {Reason}", name, result.Error, result.ErrorReason);
                else
                    logger.LogWarning("Interaction '{Name}' failed: [{Error}] {Reason}", name, result.Error, result.ErrorReason);
            }
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

    private Task HandleInteractionAsync(SocketInteraction interaction)
    {
        // Fire on a thread-pool thread so the gateway event dispatcher is freed immediately.
        // This prevents the gateway thread from being blocked and eating into Discord's
        // 3-second interaction ACK window before DeferAsync/RespondAsync is called.
        // RunMode.Sync is kept so ExecuteCommandAsync awaits the command before the scope disposes.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var ctx = new SocketInteractionContext(client, interaction);
                await interactions.ExecuteCommandAsync(ctx, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception dispatching interaction '{Type}'", interaction.GetType().Name);
            }
        });
        return Task.CompletedTask;
    }
}
