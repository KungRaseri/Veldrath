using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// Tuning for the chest sweeper and for how long an emptied resource node stays empty.
/// </summary>
/// <remarks>
/// Everything here is durable state rather than a timer in memory: a chest carries its own
/// <c>ReopenAt</c> and a node carries its <c>LastHarvestedAt</c>, so a restart mid-window continues the
/// countdown instead of losing it. See <c>ChestService</c>.
/// </remarks>
public sealed class ChestOptions
{
    /// <summary>The configuration section this class binds to.</summary>
    public const string SectionName = "Chests";

    /// <summary>Gets or sets how often the sweeper runs, in seconds.</summary>
    [Range(1, 3_600)]
    public int SweepIntervalSeconds { get; set; } = 60;

    /// <summary>Gets or sets how long an opened chest stays gone, in minutes, before it can be opened again.</summary>
    [Range(1, 10_080)]
    public int ReopenAfterMinutes { get; set; } = 180;

    /// <summary>
    /// Gets or sets how often a zone may gain a roaming chest, in minutes, counted from the last time one
    /// appeared or was opened there.
    /// </summary>
    [Range(1, 10_080)]
    public int SpawnEveryMinutes { get; set; } = 90;

    /// <summary>Gets or sets how many unopened roaming chests a zone may hold at once.</summary>
    [Range(1, 20)]
    public int MaxRoamingPerZone { get; set; } = 1;

    /// <summary>
    /// Gets or sets the lowest <c>DangerLevel</c> a location needs to be considered as a roaming chest site.
    /// Towns and safe respawns are excluded by their traits regardless of this value.
    /// </summary>
    [Range(0, 10)]
    public int MinSiteDangerLevel { get; set; } = 2;
}
