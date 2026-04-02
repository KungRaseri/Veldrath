namespace RealmEngine.Shared.Models;

/// <summary>
/// Tracks the state of an in-progress character creation wizard session.
/// Created by <c>BeginCreationSessionCommand</c> and consumed by <c>FinalizeCreationSessionCommand</c>.
/// </summary>
public class CharacterCreationSession
{
    /// <summary>Gets or sets the unique identifier for this session.</summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the current status of the session.</summary>
    public CreationSessionStatus Status { get; set; } = CreationSessionStatus.Draft;

    /// <summary>Gets or sets the UTC time this session was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC time this session was last updated.</summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // --- Player choices ---

    /// <summary>Gets or sets the character name chosen by the player.</summary>
    public string? CharacterName { get; set; }

    /// <summary>Gets or sets the class selected by the player.</summary>
    public CharacterClass? SelectedClass { get; set; }

    /// <summary>Gets or sets the species selected by the player.</summary>
    public Species? SelectedSpecies { get; set; }

    /// <summary>Gets or sets the background selected by the player.</summary>
    public Background? SelectedBackground { get; set; }

    /// <summary>
    /// Gets or sets the point-buy stat allocations chosen by the player.
    /// Keys are stat names (e.g. "Strength"); values are the allocated base values (8–15).
    /// </summary>
    public Dictionary<string, int>? AttributeAllocations { get; set; }

    /// <summary>Gets or sets the preferred armor type (e.g. "leather", "plate").</summary>
    public string? PreferredArmorType { get; set; }

    /// <summary>Gets or sets the preferred weapon type (e.g. "sword", "staff").</summary>
    public string? PreferredWeaponType { get; set; }

    /// <summary>Gets or sets a value indicating whether a shield should be included in starting equipment.</summary>
    public bool IncludeShield { get; set; } = false;

    /// <summary>Gets or sets the ID of the selected starting location.</summary>
    public string? SelectedLocationId { get; set; }

    /// <summary>Gets or sets the account that owns this session, set when the session is created via the server.</summary>
    public Guid? AccountId { get; set; }
}

/// <summary>The lifecycle status of a <see cref="CharacterCreationSession"/>.</summary>
public enum CreationSessionStatus
{
    /// <summary>The session is still in progress.</summary>
    Draft,

    /// <summary>The session was successfully finalized and a character was created.</summary>
    Finalized,

    /// <summary>The session was abandoned before finalization.</summary>
    Abandoned,
}
