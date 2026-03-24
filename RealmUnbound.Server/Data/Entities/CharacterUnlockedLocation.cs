namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Records a ZoneLocation that a specific character has unlocked through discovery or quest completion.
/// Hidden locations are absent from zone listings until a corresponding row exists here.
/// </summary>
public class CharacterUnlockedLocation
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to the character who unlocked the location.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Slug of the ZoneLocation that was unlocked.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>When the location was unlocked.</summary>
    public DateTimeOffset UnlockedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>How the location was unlocked (e.g. "skill_check_passive", "skill_check_active", "quest", "item", "manual").</summary>
    public string UnlockSource { get; set; } = string.Empty;

    // Navigation
    /// <summary>The character who unlocked this location.</summary>
    public Character Character { get; set; } = null!;
}
