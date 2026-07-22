namespace Veldrath.Contracts.Characters;

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

/// <summary>A non-persisted snapshot of the character as it would appear if the session were finalized now.</summary>
/// <param name="ClassName">Selected class display name, or <see langword="null"/> if not yet chosen.</param>
/// <param name="SpeciesName">Selected species display name, or <see langword="null"/> if not yet chosen.</param>
/// <param name="BackgroundName">Selected background display name, or <see langword="null"/> if not yet chosen.</param>
/// <param name="Strength">Allocated Strength value, or 0 if not yet allocated.</param>
/// <param name="Dexterity">Allocated Dexterity value, or 0 if not yet allocated.</param>
/// <param name="Constitution">Allocated Constitution value, or 0 if not yet allocated.</param>
/// <param name="Intelligence">Allocated Intelligence value, or 0 if not yet allocated.</param>
/// <param name="Wisdom">Allocated Wisdom value, or 0 if not yet allocated.</param>
/// <param name="Charisma">Allocated Charisma value, or 0 if not yet allocated.</param>
/// <param name="Health">Projected maximum health, or 0 if not yet calculable.</param>
/// <param name="Mana">Projected maximum mana, or 0 if not yet calculable.</param>
public record CharacterPreviewDto(
    string? ClassName,
    string? SpeciesName,
    string? BackgroundName,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    int Health,
    int Mana);

/// <summary>Response from the name-availability check endpoint.</summary>
/// <param name="Available"><see langword="true"/> when the name is both well-formed and not already taken.</param>
/// <param name="Error">Human-readable reason the name is unavailable, or <see langword="null"/> when available.</param>
public record CheckNameAvailabilityResponse(bool Available, string? Error);

/// <summary>Server-authoritative point-buy configuration for character creation.</summary>
/// <param name="TotalPoints">Total budget of points to spend across all stats.</param>
/// <param name="MinStatValue">Minimum allowed base value for any single stat.</param>
/// <param name="MaxStatValue">Maximum allowed base value for any single stat.</param>
/// <param name="CostTable">Cost lookup keyed by stat value (8–15), each entry giving the cumulative point cost.</param>
public record PointBuyConfigDto(
    int TotalPoints,
    int MinStatValue,
    int MaxStatValue,
    IReadOnlyDictionary<int, int> CostTable);

/// <summary>A single equipment type option with slug and display name.</summary>
/// <param name="Slug">The canonical slug used in API calls (e.g. <c>"light"</c>, <c>"sword"</c>).</param>
/// <param name="DisplayName">The human-readable label for UI display (e.g. <c>"Light Armor"</c>, <c>"Sword"</c>).</param>
public record EquipmentTypeOptionDto(string Slug, string DisplayName);

/// <summary>Catalog of available equipment types for character creation preferences.</summary>
/// <param name="ArmorTypes">Available armor type options, ordered by typical progression.</param>
/// <param name="WeaponTypes">Available weapon type options, ordered alphabetically.</param>
public record EquipmentTypeCatalogDto(
    IReadOnlyList<EquipmentTypeOptionDto> ArmorTypes,
    IReadOnlyList<EquipmentTypeOptionDto> WeaponTypes);

/// <summary>Response from beginning a character creation session.</summary>
/// <param name="SessionId">The session identifier to use in subsequent session commands.</param>
/// <param name="Success">Whether the session was created successfully.</param>
/// <param name="PointBuyConfig">The server-authoritative point-buy configuration, or <see langword="null"/> when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="EquipmentTypeCatalog">The available armor and weapon type catalog, or <see langword="null"/> when unavailable.</param>
/// <param name="SessionTimeoutMinutes">The session idle timeout in minutes. Defaults to 30 when not provided by the server.</param>
public record BeginCreationSessionResponse(
    Guid SessionId,
    bool Success,
    PointBuyConfigDto? PointBuyConfig = null,
    EquipmentTypeCatalogDto? EquipmentTypeCatalog = null,
    int SessionTimeoutMinutes = 30);

/// <summary>Response from a single-step session mutation (set name, class, species, etc.).</summary>
/// <param name="Success">Whether the update was applied.</param>
/// <param name="Message">A message describing the result.</param>
public record SetCreationChoiceResponse(bool Success, string Message);

/// <summary>Response from setting attribute allocations, including remaining point budget.</summary>
/// <param name="Success">Whether the allocation was accepted.</param>
/// <param name="Message">A message describing the result.</param>
/// <param name="RemainingPoints">The remaining point budget after this allocation.</param>
public record AllocateCreationAttributesResponse(bool Success, string Message, int RemainingPoints);
