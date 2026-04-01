using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Command to create a new character with full initialization (abilities, spells, equipment).
/// </summary>
public record CreateCharacterCommand : IRequest<CreateCharacterResult>
{
    /// <summary>
    /// Gets the name of the new character.
    /// </summary>
    public required string CharacterName { get; init; }
    
    /// <summary>
    /// Gets the character class for the new character.
    /// </summary>
    public required CharacterClass CharacterClass { get; init; }

    /// <summary>
    /// Gets the background ID for attribute bonuses (optional).
    /// Example: "backgrounds/strength:soldier"
    /// </summary>
    public string? BackgroundId { get; init; }

    /// <summary>
    /// Gets the difficulty level (optional, defaults to "Normal").
    /// Values: "Easy", "Normal", "Hard", "Very Hard"
    /// </summary>
    public string DifficultyLevel { get; init; } = "Normal";

    /// <summary>
    /// Gets the starting location ID (optional).
    /// Example: "locations/settlements:starting-village"
    /// </summary>
    public string? StartingLocationId { get; init; }

    /// <summary>
    /// Gets the species slug selected during character creation (optional).
    /// Example: "human", "elf", "dwarf"
    /// </summary>
    public string? SpeciesSlug { get; init; }

    /// <summary>
    /// Gets the point-buy attribute allocations chosen by the player (optional).
    /// Keys are stat names (e.g. "Strength"); values are the base values before class/species/background bonuses (8–15).
    /// When <see langword="null"/>, the handler defaults each stat to 10.
    /// </summary>
    public Dictionary<string, int>? AttributeAllocations { get; init; }

    /// <summary>
    /// Gets the preferred armor type for equipment selection (optional).
    /// Example: "cloth", "leather", "mail", "plate"
    /// </summary>
    public string? PreferredArmorType { get; init; }

    /// <summary>
    /// Gets the preferred weapon type for equipment selection (optional).
    /// Example: "sword", "axe", "bow", "staff"
    /// </summary>
    public string? PreferredWeaponType { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include a shield in starting equipment (optional).
    /// </summary>
    public bool IncludeShield { get; init; } = false;
}

/// <summary>
/// Result of creating a new character.
/// </summary>
public record CreateCharacterResult
{
    /// <summary>
    /// Gets the newly created and initialized character.
    /// </summary>
    public Character? Character { get; init; }
    
    /// <summary>
    /// Gets a value indicating whether character creation was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Gets a message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the number of starting abilities that were learned.
    /// </summary>
    public int AbilitiesLearned { get; init; }
    
    /// <summary>
    /// Gets the number of starting spells that were learned.
    /// </summary>
    public int SpellsLearned { get; init; }

    /// <summary>
    /// Gets the equipment items that were selected and equipped.
    /// </summary>
    public List<Item> EquipmentSelected { get; init; } = new();

    /// <summary>
    /// Gets the starting location that was assigned to the character.
    /// </summary>
    public Location? StartingLocation { get; init; }

    /// <summary>
    /// Gets the background that was applied to the character.
    /// </summary>
    public Background? BackgroundApplied { get; init; }
}
