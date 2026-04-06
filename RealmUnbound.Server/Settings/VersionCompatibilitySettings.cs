namespace RealmUnbound.Server.Settings;

/// <summary>
/// Configuration for client compatibility enforcement sent to connecting clients via <c>ServerInfo</c>.
/// Bound from the <c>VersionCompatibility</c> section in <c>appsettings.json</c> (dev default) and
/// overridden by the generated <c>appsettings.Production.json</c> at Release publish time.
/// </summary>
public record VersionCompatibilitySettings
{
    /// <summary>
    /// The minimum client version this server will accept, formatted as <c>Major.Minor</c>
    /// (e.g. <c>"0.1"</c>). Only bump this when a genuinely breaking protocol change is introduced.
    /// </summary>
    public string MinCompatibleClientVersion { get; init; } = "0.1";
}
