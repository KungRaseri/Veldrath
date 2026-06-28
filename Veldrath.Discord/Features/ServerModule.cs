using Discord;
using Discord.Interactions;
using Veldrath.Contracts.Zones;
using Veldrath.Discord.Services;

namespace Veldrath.Discord.Features;

/// <summary>Live server and realm status commands.</summary>
public sealed class ServerModule(ServerStatusService server) : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Color Gold = new(0xFFAA00);

    [SlashCommand("status", "Check if the realm is online and see who's adventuring")]
    public async Task StatusAsync()
    {
        await DeferAsync(ephemeral: true);

        var zones = await server.GetZonesAsync();
        var embed = BuildStatusEmbed(zones, Gold);

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("zones", "Browse all zones in the realm and their current population")]
    public async Task ZonesAsync()
    {
        await DeferAsync(ephemeral: true);

        var zones = await server.GetZonesAsync();

        if (zones is null)
        {
            await FollowupAsync("🔴 The realm server is currently unreachable.", ephemeral: true);
            return;
        }

        if (zones.Count == 0)
        {
            await FollowupAsync("No zones are currently configured in the realm.", ephemeral: true);
            return;
        }

        var embed = BuildZonesEmbed(zones);
        await FollowupAsync(embed: embed);
    }

    /// <summary>Builds the embed for the <c>/status</c> command.</summary>
    internal static Embed BuildStatusEmbed(List<ZoneDto>? zones, Color accent)
    {
        if (zones is null)
        {
            return new EmbedBuilder()
                .WithTitle("⚔️ Realm Unbound")
                .WithDescription("🔴 **The realm is unreachable.** The server may be offline.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();
        }

        var totalPlayers = zones.Sum(z => z.OnlinePlayers);

        return new EmbedBuilder()
            .WithTitle("⚔️ Realm Unbound — Status")
            .WithDescription("The realm stands. Adventurers are abroad.")
            .WithColor(accent)
            .AddField("Status", "🟢 Online", inline: true)
            .AddField("Wanderers", totalPlayers == 0 ? "None" : totalPlayers.ToString(), inline: true)
            .AddField("Active Zones", zones.Count.ToString(), inline: true)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>Builds the embed for the <c>/zones</c> command.</summary>
    internal static Embed BuildZonesEmbed(List<ZoneDto> zones)
    {
        var embed = new EmbedBuilder()
            .WithTitle("🗺️ Realm Zones")
            .WithColor(Gold)
            .WithFooter(zones.Count > 20 ? $"Showing 20 of {zones.Count} zones" : $"{zones.Count} zones total");

        foreach (var zone in zones.Take(20))
        {
            var desc = string.IsNullOrWhiteSpace(zone.Description)
                ? "*Unknown lands…*"
                : (zone.Description.Length > 80 ? zone.Description[..80] + "…" : zone.Description);

            var badge = zone.IsStarter ? "⭐ Starter" : zone.Type;
            var pop = zone.OnlinePlayers == 0 ? "Empty" : $"{zone.OnlinePlayers} online";

            embed.AddField(
                zone.Name,
                $"{desc}\n**{pop}** · Min. level **{zone.MinLevel}** · {badge}",
                inline: false);
        }

        return embed.Build();
    }
}
