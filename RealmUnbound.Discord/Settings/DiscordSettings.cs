namespace Veldrath.Discord.Settings;

/// <summary>
/// Configuration options bound from the <c>Discord</c> section of appsettings.
/// </summary>
public sealed record DiscordSettings
{
    /// <summary>Gets the bot token from the Discord Developer Portal.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild (server) ID used for development slash-command registration.
    /// When non-zero, commands are registered to this guild instantly instead of
    /// globally (which can take up to an hour to propagate).
    /// Set to <c>0</c> in production to register commands globally.
    /// </summary>
    public ulong DevGuildId { get; init; }

    /// <summary>Gets the base URL of the Veldrath.Server API (used for live status).</summary>
    public string ServerBaseUrl { get; init; } = "http://localhost:8080";
}
