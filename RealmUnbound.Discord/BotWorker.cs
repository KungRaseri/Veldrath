using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using RealmUnbound.Discord.Services;
using RealmUnbound.Discord.Settings;

namespace RealmUnbound.Discord;

/// <summary>
/// Hosted service that owns the Discord client lifecycle:
/// logs in with the configured bot token, starts the gateway connection,
/// and shuts down cleanly on host stop.
/// </summary>
internal sealed class BotWorker(
    DiscordSocketClient client,
    InteractionHandlingService interactions,
    IOptions<DiscordSettings> settings,
    ILogger<BotWorker> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await interactions.InitializeAsync();

        client.Log += message =>
        {
            var level = message.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error    => LogLevel.Error,
                LogSeverity.Warning  => LogLevel.Warning,
                LogSeverity.Info     => LogLevel.Information,
                LogSeverity.Verbose  => LogLevel.Trace,
                LogSeverity.Debug    => LogLevel.Debug,
                _                   => LogLevel.Information,
            };

            logger.Log(level, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
            return Task.CompletedTask;
        };

        await client.LoginAsync(TokenType.Bot, settings.Value.Token);
        await client.StartAsync();
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.LogoutAsync();
    }
}
