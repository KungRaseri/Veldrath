namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the player enters a zone.
/// Contains zone metadata and the current occupant roster.
/// </summary>
/// <param name="Id">The zone's unique identifier (slug).</param>
/// <param name="Name">The zone's display name.</param>
/// <param name="Description">A description of the zone.</param>
/// <param name="ZoneType">The type of zone (e.g. "town", "wilderness", "dungeon").</param>
/// <param name="Occupants">The list of players currently in the zone.</param>
public sealed record ZoneEnteredPayload(
    string Id,
    string Name,
    string Description,
    string ZoneType,
    IReadOnlyList<OccupantEntry> Occupants);

/// <summary>
/// A single occupant entry within a <see cref="ZoneEnteredPayload"/>.
/// </summary>
/// <param name="CharacterId">The occupying character's identifier.</param>
/// <param name="CharacterName">The occupying character's display name.</param>
/// <param name="EnteredAt">When this character entered the zone.</param>
public sealed record OccupantEntry(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt);

/// <summary>
/// Hub event payload received when another player enters the current zone.
/// </summary>
/// <param name="CharacterId">The entering character's identifier.</param>
/// <param name="CharacterName">The entering character's display name.</param>
/// <param name="ZoneId">The zone they entered.</param>
public sealed record PlayerEnteredPayload(Guid CharacterId, string CharacterName, string ZoneId);

/// <summary>
/// Hub event payload received when another player leaves the current zone.
/// </summary>
/// <param name="CharacterId">The leaving character's identifier.</param>
/// <param name="CharacterName">The leaving character's display name.</param>
/// <param name="ZoneId">The zone they left.</param>
public sealed record PlayerLeftPayload(Guid CharacterId, string CharacterName, string ZoneId);
