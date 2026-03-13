namespace RealmEngine.Data.Entities;

/// <summary>
/// System configuration data. One row per config file.
/// ConfigKey e.g. "experience", "rarity", "budget", "growth-stats", "socket-config".
/// Data is JSONB — config files have unique schemas per key.
/// </summary>
public class GameConfig
{
    /// <summary>Unique config identifier — e.g. "experience", "rarity", "budget", "growth-stats".</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>Full JSON config payload, preserved as-is from the source file.</summary>
    public string Data { get; set; } = "{}";

    /// <summary>Incremented by the import pipeline on each upsert.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Timestamp of the last import pipeline upsert (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
