namespace Veldrath.Server.Settings;

/// <summary>
/// Configuration options for <see cref="Services.ActiveCharacterTracker"/>.
/// Bind from <c>appsettings.json</c> under the <c>"ActiveCharacterTracker"</c> section via
/// <c>services.Configure<ActiveCharacterTrackerOptions>(config.GetSection("ActiveCharacterTracker"))</c>.
/// </summary>
public sealed class ActiveCharacterTrackerOptions
{
    /// <summary>
    /// Number of seconds a disconnecting character claim remains valid before
    /// another connection can forcibly take it. Defaults to <c>30</c> seconds.
    /// </summary>
    public int GracePeriodSeconds { get; set; } = 30;
}
