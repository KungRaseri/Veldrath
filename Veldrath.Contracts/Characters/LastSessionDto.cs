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
/// <param name="TileX">Last known tile X position.</param>
/// <param name="TileY">Last known tile Y position.</param>
public sealed record LastSessionDto(
    Guid CharacterId,
    string CharacterName,
    string? ZoneId,
    string? RegionId,
    int TileX,
    int TileY);
