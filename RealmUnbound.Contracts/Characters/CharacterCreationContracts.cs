namespace RealmUnbound.Contracts.Characters;

/// <summary>Request body for setting the character class on a creation session.</summary>
/// <param name="ClassName">The slug or display name of the class to select.</param>
public record SetCreationClassRequest(string ClassName);

/// <summary>Request body for setting the character name on a creation session.</summary>
/// <param name="CharacterName">The desired name for the new character.</param>
public record SetCreationNameRequest(string CharacterName);

/// <summary>Request body for setting the species on a creation session.</summary>
/// <param name="SpeciesSlug">The slug of the species to select.</param>
public record SetCreationSpeciesRequest(string SpeciesSlug);

/// <summary>Request body for setting the background on a creation session.</summary>
/// <param name="BackgroundId">The identifier of the background to select.</param>
public record SetCreationBackgroundRequest(string BackgroundId);

/// <summary>Request body for setting attribute allocations (point-buy) on a creation session.</summary>
/// <param name="Allocations">
/// A mapping of attribute name (e.g. <c>"Strength"</c>) to the allocated value (8–15).
/// All six core attributes must be present.
/// </param>
public record SetCreationAttributesRequest(Dictionary<string, int> Allocations);

/// <summary>Request body for setting equipment preferences on a creation session.</summary>
/// <param name="PreferredArmorType">The preferred armor type slug, or <see langword="null"/> to skip.</param>
/// <param name="PreferredWeaponType">The preferred weapon type slug, or <see langword="null"/> to skip.</param>
/// <param name="IncludeShield">Whether to include a shield in the starting equipment selection.</param>
public record SetCreationEquipmentPreferencesRequest(
    string? PreferredArmorType,
    string? PreferredWeaponType,
    bool IncludeShield = false);

/// <summary>Request body for setting the starting location on a creation session.</summary>
/// <param name="LocationId">The identifier of the starting location to select.</param>
public record SetCreationLocationRequest(string LocationId);

/// <summary>Request body for finalizing a character creation session.</summary>
/// <param name="CharacterName">The display name for the new character, or <see langword="null"/> if the name was already set via the name step.</param>
/// <param name="DifficultyMode">The difficulty mode: <c>"normal"</c> or <c>"hardcore"</c>.</param>
public record FinalizeCreationSessionRequest(
    string? CharacterName,
    string DifficultyMode = "normal");
