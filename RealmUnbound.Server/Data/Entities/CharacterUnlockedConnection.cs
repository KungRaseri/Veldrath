namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Records a <see cref="RealmEngine.Data.Entities.ZoneLocationConnection"/> that a specific character
/// has unlocked through discovery or quest completion.
/// Hidden connections are absent from zone listings until a corresponding row exists here.
/// </summary>
public class CharacterUnlockedConnection
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to the character who unlocked the connection.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The <see cref="RealmEngine.Data.Entities.ZoneLocationConnection.Id"/> that was unlocked.</summary>
    public int ConnectionId { get; set; }

    /// <summary>When the connection was unlocked.</summary>
    public DateTimeOffset UnlockedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>How the connection was unlocked (e.g. "skill_check_active", "quest", "item", "manual").</summary>
    public string UnlockSource { get; set; } = string.Empty;

    // Navigation
    /// <summary>The character who unlocked this connection.</summary>
    public Character Character { get; set; } = null!;
}
