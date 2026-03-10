namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// A playable character belonging to a <see cref="PlayerAccount"/>.
/// Accounts support up to <see cref="PlayerAccount.MaxCharacterSlots"/> active characters.
/// Attributes (STR, DEX, etc.) are stored as a JSON string to avoid a wide nullable column table
/// and to allow the engine's attribute schema to evolve without DB migrations.
/// </summary>
public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to the owning <see cref="PlayerAccount"/>.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Slot position within the account's character list (1..MaxCharacterSlots).</summary>
    public int SlotIndex { get; set; }

    /// <summary>Server-wide unique display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Reference to the class catalog entry (e.g. "@classes/warriors:fighter").</summary>
    public string ClassName { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    public long Experience { get; set; }

    /// <summary>
    /// JSON-serialised attribute snapshot (Strength, Dexterity, etc.).
    /// Deserialised on demand — not mapped to individual columns.
    /// </summary>
    public string Attributes { get; set; } = "{}";

    /// <summary>Zone the character starts in (fixed on creation; tutorial zone later).</summary>
    public string StartingZoneId { get; set; } = "starting-zone";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastPlayedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete timestamp. Non-null means the character has been deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public PlayerAccount Account { get; set; } = null!;
}
