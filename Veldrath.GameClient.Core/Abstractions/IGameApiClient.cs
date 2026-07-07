using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Content;

namespace Veldrath.GameClient.Core.Abstractions;

/// <summary>
/// Abstraction for REST API calls needed by game components.
/// Implementations communicate with the Veldrath.Server REST API.
/// </summary>
public interface IGameApiClient
{
    /// <summary>Returns all characters belonging to the authenticated account.</summary>
    Task<List<CharacterDto>> GetCharactersAsync(CancellationToken ct = default);

    /// <summary>Checks whether a character name is available.</summary>
    /// <param name="name">The desired character name.</param>
    /// <returns>A response indicating availability, or <c>null</c> on failure.</returns>
    Task<CheckNameAvailabilityResponse?> CheckCharacterNameAsync(string name, CancellationToken ct = default);

    /// <summary>Returns all active actor classes available for character creation.</summary>
    Task<List<ActorClassDto>> GetClassesAsync(CancellationToken ct = default);

    // ── Session-based character creation ───────────────────────────────────────────

    /// <summary>Begins a new character creation session and returns the session identifier.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response with the session ID and success status, or <c>null</c> on failure.</returns>
    Task<BeginCreationSessionResponse?> BeginCreationSessionAsync(CancellationToken ct = default);

    /// <summary>Returns a non-persisted preview of the character being built in the session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The character preview, or <c>null</c> if the session has insufficient state or does not exist.</returns>
    Task<CharacterPreviewDto?> GetCreationPreviewAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Finalizes the session and creates the character.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The finalization request containing the character name and difficulty mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created character DTO, or <c>null</c> if the request was rejected.</returns>
    Task<CharacterDto?> FinalizeCreationSessionAsync(Guid sessionId, FinalizeCreationSessionRequest request, CancellationToken ct = default);

    /// <summary>Abandons the creation session and releases server-side resources.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AbandonCreationSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Sets the character name on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="characterName">The desired character name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationNameAsync(Guid sessionId, string characterName, CancellationToken ct = default);

    /// <summary>Sets the selected class on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="className">The class display name or slug to select.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationClassAsync(Guid sessionId, string className, CancellationToken ct = default);

    /// <summary>Sets the selected species on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="speciesSlug">The species slug to select.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationSpeciesAsync(Guid sessionId, string speciesSlug, CancellationToken ct = default);

    /// <summary>Sets the selected background on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="backgroundId">The background identifier to select.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationBackgroundAsync(Guid sessionId, string backgroundId, CancellationToken ct = default);

    /// <summary>Sets the attribute allocations (point-buy) on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="allocations">A mapping of attribute name to the allocated value (8–15). All six core attributes must be present.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure with remaining points, or <c>null</c> on network error.</returns>
    Task<AllocateCreationAttributesResponse?> SetCreationAttributesAsync(Guid sessionId, Dictionary<string, int> allocations, CancellationToken ct = default);

    /// <summary>Sets the equipment preferences on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="armorType">The preferred armor type slug, or <c>null</c> to skip.</param>
    /// <param name="weaponType">The preferred weapon type slug, or <c>null</c> to skip.</param>
    /// <param name="includeShield">Whether to include a shield in starting equipment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationEquipmentPreferencesAsync(Guid sessionId, string? armorType, string? weaponType, bool includeShield, CancellationToken ct = default);

    /// <summary>Sets the starting location on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="locationId">The location identifier to select.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A response indicating success or failure, or <c>null</c> on network error.</returns>
    Task<SetCreationChoiceResponse?> SetCreationLocationAsync(Guid sessionId, string locationId, CancellationToken ct = default);

    // ── Content lookups ────────────────────────────────────────────────────────────

    /// <summary>Returns all available playable species for character creation.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<List<SpeciesDto>> GetSpeciesAsync(CancellationToken ct = default);

    /// <summary>Returns all available backgrounds for character creation.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<List<BackgroundDto>> GetBackgroundsAsync(CancellationToken ct = default);
}
