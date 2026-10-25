namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A character's active fight against one enemy instance, durable across restarts and short disconnects.
/// </summary>
/// <remarks>
/// The character id is the primary key on purpose: a character fights one enemy at a time, and making that a
/// database constraint replaces behaviour that used to be an implicit dictionary rule.
/// <see cref="DisconnectedAt"/> is what makes the reconnect grace window possible — a dropped player keeps their
/// turn state until the configured grace expires, and the respawn sweeper prunes only the expired rows.
/// </remarks>
public class CombatSession
{
    /// <summary>Gets or sets the character whose fight this is.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets the enemy instance being fought.</summary>
    public Guid EnemyInstanceId { get; set; }

    /// <summary>Gets or sets the SignalR zone group the fight happens in.</summary>
    public string ZoneGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the zone-location slug the fight happens at.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of rounds elapsed in this encounter.</summary>
    public int TurnCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the player chose to defend this round.</summary>
    public bool PlayerDefending { get; set; }

    /// <summary>Gets or sets when the fight began (UTC).</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the row last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the owner disconnected (UTC), or <see langword="null"/> while they are connected. A
    /// reconnect clears it and resumes the same fight; the sweeper deletes rows past the grace window.
    /// </summary>
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>Gets or sets the enemy being fought.</summary>
    public EnemyInstance? Enemy { get; set; }

    /// <summary>Gets or sets the fighting character.</summary>
    public Character? Character { get; set; }
}
