namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Live record of a character currently online, tracking their position in the world.
/// Created when <c>EnterZone</c> is called; deleted on disconnect or explicit <c>LeaveZone</c>.
/// A character is always in a region. <see cref="ZoneId"/> is <see langword="null"/> when the
/// character is on the region map rather than inside a specific zone.
/// </summary>
public class PlayerSession
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The character this session belongs to (unique — one session per character).</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Display name of the character, denormalised for fast broadcast lookup.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>SignalR <c>Context.ConnectionId</c> — used to find and clean up on disconnect.</summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// The region the character is currently in. Always set — characters are always in a region.
    /// </summary>
    public string RegionId { get; set; } = string.Empty;

    /// <summary>
    /// The zone the character is currently inside, or <see langword="null"/> when the character
    /// is navigating the region map rather than inside a specific zone.
    /// </summary>
    public string? ZoneId { get; set; }

    /// <summary>Tile column of the character's current position (region map or zone).</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the character's current position (region map or zone).</summary>
    public int TileY { get; set; }

    /// <summary>UTC timestamp of when this session was created.</summary>
    public DateTimeOffset EnteredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC timestamp of the character's last accepted tile move in this session.
    /// Used by the server-side rate limiter to cap movement at 10 tiles/second.
    /// </summary>
    public DateTimeOffset LastMovedAt { get; set; } = DateTimeOffset.MinValue;

    // Navigation
    /// <summary>The character this session belongs to.</summary>
    public Character Character { get; set; } = null!;

    /// <summary>The region the character is in.</summary>
    public Region Region { get; set; } = null!;

    /// <summary>The zone the character is in, or <see langword="null"/> when on the region map.</summary>
    public Zone? Zone { get; set; }
}
