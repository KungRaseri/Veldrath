using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// Tuning for the durable world: how long a defeated enemy stays gone, how often the sweeper runs, how many rows it
/// takes at once, and how long a disconnected combat session survives.
/// </summary>
/// <remarks>
/// Bound from the <c>Enemies</c> configuration section with <c>ValidateOnStart</c>, so a missing or nonsensical value
/// fails the host at startup instead of silently falling back to a default — the failsafe rule this milestone
/// establishes for both content and configuration.
/// </remarks>
public sealed class EnemyWorldOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Enemies";

    /// <summary>Gets or sets how long a defeated enemy stays gone, in seconds.</summary>
    [Range(1, 86_400)]
    public int RespawnDelaySeconds { get; set; } = 60;

    /// <summary>Gets or sets how often the respawn sweeper runs, in seconds.</summary>
    [Range(1, 3_600)]
    public int SweepIntervalSeconds { get; set; } = 5;

    /// <summary>Gets or sets the maximum number of enemies revived per sweep.</summary>
    [Range(1, 1_000)]
    public int RespawnBatchSize { get; set; } = 50;

    /// <summary>Gets or sets how long a disconnected combat session survives before it is pruned, in seconds.</summary>
    [Range(1, 86_400)]
    public int SessionGraceSeconds { get; set; } = 60;

    /// <summary>Gets the respawn delay as a duration.</summary>
    public TimeSpan RespawnDelay => TimeSpan.FromSeconds(RespawnDelaySeconds);

    /// <summary>Gets the session grace window as a duration.</summary>
    public TimeSpan SessionGrace => TimeSpan.FromSeconds(SessionGraceSeconds);
}
