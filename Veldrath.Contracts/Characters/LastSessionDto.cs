namespace Veldrath.Contracts.Characters;

/// <summary>
/// DTO returned by the <c>GET /api/characters/last-session</c> endpoint.
/// Contains the authenticated account's most recently played character
/// and its last known location, enabling the client to restore game state
/// after a page refresh without manual character selection.
/// </summary>
/// <param name="CharacterId">The character ID the player was last using.</param>
/// <param name="CharacterName">The character's display name.</param>
/// <param name="ZoneId">
/// The zone ID the character was last in, or <see langword="null"/> if on the region map.
/// </param>
/// <param name="RegionId">
/// The region ID derived from the character's last zone, or <see langword="null"/>
/// if the zone could not be resolved.
/// </param>
/// <param name="CurrentMapId">Stable identifier of the map the character was last on, or <see langword="null"/>.</param>
/// <param name="CurrentNodeId">Stable identifier of the node the character occupied, or <see langword="null"/>.</param>
public sealed record LastSessionDto(
    Guid CharacterId,
    string CharacterName,
    string? ZoneId,
    string? RegionId,
    string? CurrentMapId = null,
    string? CurrentNodeId = null);
