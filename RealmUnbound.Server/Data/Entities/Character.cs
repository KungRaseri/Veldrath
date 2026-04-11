namespace Veldrath.Server.Data.Entities;

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

    /// <summary>Display name of the selected character class (e.g. "Warrior", "Mage").</summary>
    public string ClassName { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    public long Experience { get; set; }

    /// <summary>
    /// JSON-serialised attribute snapshot (Strength, Dexterity, etc.).
    /// Deserialised on demand — not mapped to individual columns.
    /// </summary>
    public string Attributes { get; set; } = "{}";

    /// <summary>
    /// JSON-serialised equipment snapshot (slot → item-ref slug, e.g. <c>{"MainHand":"iron_sword"}</c>).
    /// Kept separate from <see cref="Attributes"/> to avoid type conflicts (attribute values are integers;
    /// equipment values are string slugs).  Deserialised on demand.
    /// </summary>
    public string EquipmentBlob { get; set; } = "{}";

    /// <summary>
    /// JSON-serialised inventory snapshot as an array of <c>{ ItemRef, Quantity, Durability? }</c> objects.
    /// Deserialised on demand. Empty array when the character carries no items.
    /// </summary>
    public string InventoryBlob { get; set; } = "[]";

    /// <summary>Zone the character starts in (fixed on creation).</summary>
    public string StartingZoneId { get; set; } = "crestfall";

    /// <summary>Zone the character is currently in (updated on EnterZone).</summary>
    public string CurrentZoneId { get; set; } = "crestfall";

    /// <summary>Slug of the ZoneLocation the character is currently at within the zone, or <see langword="null"/> if not at a specific location.</summary>
    public string? CurrentZoneLocationSlug { get; set; }

    /// <summary>Identifier of the background chosen at character creation (e.g. "acolyte", "criminal"), or <see langword="null"/> if none was selected.</summary>
    public string? BackgroundId { get; set; }

    /// <summary>Slug of the species chosen at character creation (e.g. "human", "elf"), or <see langword="null"/> if none was selected.</summary>
    public string? SpeciesSlug { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastPlayedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete timestamp. Non-null means the character has been deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Difficulty mode chosen at character creation: <c>"normal"</c> or <c>"hardcore"</c>.
    /// Hardcore characters are permanently deleted on death.
    /// </summary>
    public string DifficultyMode { get; set; } = "normal";

    /// <summary>
    /// JSON-serialised list of ability slugs the character has learned (e.g. <c>["fireball","heal"]</c>).
    /// Deserialised on demand. Empty array for characters with no learned abilities.
    /// </summary>
    public string AbilitiesBlob { get; set; } = "[]";

    // ── Tilemap position ───────────────────────────────────────────────────

    /// <summary>Tile X coordinate of the character's current position on the zone grid.</summary>
    public int TileX { get; set; } = 0;

    /// <summary>Tile Y coordinate of the character's current position on the zone grid.</summary>
    public int TileY { get; set; } = 0;

    /// <summary>
    /// Zone ID whose tile grid the character is currently positioned in.
    /// <see langword="null"/> until the character first enters a zone with an authored tilemap.
    /// When this differs from <see cref="CurrentZoneId"/> the character is snapped to the spawn tile on entry.
    /// </summary>
    public string? TileZoneId { get; set; }

    // Navigation
    public PlayerAccount Account { get; set; } = null!;
}
