namespace RealmUnbound.Server.Settings;

/// <summary>
/// Configuration options for the server-side moderation system.
/// Bind from <c>appsettings.json</c> under the <c>"Moderation"</c> section via
/// <c>services.Configure&lt;ModerationOptions&gt;(config.GetSection("Moderation"))</c>.
/// </summary>
public sealed class ModerationOptions
{
    /// <summary>
    /// Number of formal warnings an account must accumulate before an automatic ban is issued.
    /// Defaults to <c>3</c>. Set to <c>0</c> to disable auto-ban on warnings.
    /// </summary>
    public int AutoBanWarnThreshold { get; set; } = 3;

    /// <summary>
    /// Maximum number of characters allowed in a single chat message before it is truncated.
    /// Defaults to <c>500</c>.
    /// </summary>
    public int MaxChatMessageLength { get; set; } = 500;
}
