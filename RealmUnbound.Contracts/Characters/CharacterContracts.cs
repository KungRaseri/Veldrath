namespace RealmUnbound.Contracts.Characters;

/// <summary>Request to create a new character in the current player account.</summary>
/// <param name="Name">The display name for the new character.</param>
/// <param name="ClassName">The class display name to assign (e.g. "Warrior", "Mage").</param>
public record CreateCharacterRequest(string Name, string ClassName);

/// <summary>Represents a character belonging to the current player account.</summary>
/// <param name="Id">Unique identifier for this character.</param>
/// <param name="SlotIndex">Zero-based slot position in the player's character roster.</param>
/// <param name="Name">Display name of the character.</param>
/// <param name="ClassName">Display name of the class assigned to this character (e.g. "Warrior", "Mage").</param>
/// <param name="Level">Current level of the character.</param>
/// <param name="Experience">Total accumulated experience points.</param>
/// <param name="LastPlayedAt">UTC timestamp of the most recent session for this character.</param>
/// <param name="CurrentZoneId">Identifier of the zone the character is currently located in.</param>
/// <param name="IsOnline">True if this character is currently in an active session.</param>
public record CharacterDto(
    Guid Id,
    int SlotIndex,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset LastPlayedAt,
    string CurrentZoneId,
    bool IsOnline = false);
