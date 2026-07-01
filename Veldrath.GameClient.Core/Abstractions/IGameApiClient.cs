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

    /// <summary>Creates a new character for the authenticated account.</summary>
    /// <param name="name">The character's display name.</param>
    /// <param name="className">The class display name (e.g. "Warrior", "Mage").</param>
    /// <param name="difficultyMode">The difficulty mode: "normal" or "hardcore".</param>
    /// <returns>The created character DTO, or <c>null</c> if the request was rejected.</returns>
    Task<CharacterDto?> CreateCharacterAsync(string name, string className, string difficultyMode = "normal", CancellationToken ct = default);

    /// <summary>Checks whether a character name is available.</summary>
    /// <param name="name">The desired character name.</param>
    /// <returns>A response indicating availability, or <c>null</c> on failure.</returns>
    Task<CheckNameAvailabilityResponse?> CheckCharacterNameAsync(string name, CancellationToken ct = default);

    /// <summary>Returns all active actor classes available for character creation.</summary>
    Task<List<ActorClassDto>> GetClassesAsync(CancellationToken ct = default);
}
