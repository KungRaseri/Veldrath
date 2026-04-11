using Discord;
using Discord.Interactions;

namespace Veldrath.Discord.Features;

/// <summary>Basic connectivity and diagnostics slash commands.</summary>
public sealed class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    /// Responds with the bot's current gateway round-trip latency in milliseconds.
    /// Useful for verifying the bot is alive and measuring connection quality.
    /// </summary>
    [SlashCommand("ping", "Check the bot's latency")]
    public async Task PingAsync()
        => await RespondAsync($"Pong! Gateway latency: **{Context.Client.Latency} ms**",
               allowedMentions: AllowedMentions.None);
}
