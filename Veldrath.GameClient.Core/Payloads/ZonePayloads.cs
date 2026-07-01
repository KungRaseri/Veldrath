using RealmEngine.Shared.Models;

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

// ── G37-G47 Zone/Region/Location payloads ─────────────────────────────────

/// <summary>
/// Hub event payload received when the player exits a zone and transitions to a region map.
/// </summary>
/// <param name="RegionId">The region the player is exiting into.</param>
/// <param name="TileX">The tile column of the exit position on the region map.</param>
/// <param name="TileY">The tile row of the exit position on the region map.</param>
public sealed record ZoneExitedPayload(string RegionId, int TileX, int TileY);

/// <summary>
/// Hub event payload received when the player's current region changes.
/// </summary>
/// <param name="RegionId">The new region identifier.</param>
/// <param name="TileX">The tile column of the entry position on the region map.</param>
/// <param name="TileY">The tile row of the entry position on the region map.</param>
public sealed record RegionChangedPayload(string RegionId, int TileX, int TileY);

/// <summary>
/// Hub event payload received when a zone entry point is triggered on the region map.
/// </summary>
/// <param name="Slug">The zone entry point slug.</param>
/// <param name="ZoneId">The zone identifier to enter.</param>
/// <param name="Name">The display name of the zone.</param>
public sealed record ZoneEntryTriggeredPayload(string Slug, string ZoneId, string Name);

/// <summary>
/// Hub event payload received when a region exit (border crossing) is triggered.
/// </summary>
/// <param name="ExitId">The exit point identifier.</param>
/// <param name="TargetRegion">The target region identifier.</param>
public sealed record RegionExitTriggeredPayload(string ExitId, string TargetRegion);

/// <summary>
/// Hub event payload received when a tile-based zone exit is triggered.
/// </summary>
/// <param name="ToZoneId">The destination zone identifier.</param>
public sealed record TileExitTriggeredPayload(string ToZoneId);

/// <summary>
/// A lightweight reference to an enemy that can appear at a zone location.
/// </summary>
/// <param name="Slug">The enemy identifier slug.</param>
/// <param name="Name">The enemy display name.</param>
/// <param name="Level">The enemy's level.</param>
public sealed record EnemyReference(string Slug, string Name, int Level);

/// <summary>
/// A traversal connection from the current zone location to another zone or location.
/// </summary>
/// <param name="Slug">The connection identifier slug.</param>
/// <param name="TargetZoneId">The target zone identifier, or empty string if intra-zone.</param>
/// <param name="IsCrossZone">Whether this connection crosses zone boundaries.</param>
public sealed record ZoneConnectionLink(string Slug, string TargetZoneId, bool IsCrossZone);

/// <summary>
/// Hub event payload received when the player enters a named location within a zone.
/// </summary>
/// <param name="Slug">The location identifier slug.</param>
/// <param name="Name">The location display name.</param>
/// <param name="Type">The location type (e.g. "cave", "ruins", "camp").</param>
/// <param name="Enemies">The enemies present at this location.</param>
/// <param name="Connections">The traversal connections from this location.</param>
public sealed record LocationEnteredPayload(
    string Slug,
    string Name,
    string Type,
    IReadOnlyList<EnemyReference> Enemies,
    IReadOnlyList<ZoneConnectionLink> Connections);

/// <summary>
/// Hub event payload received when a new zone location is discovered or unlocked.
/// </summary>
/// <param name="Slug">The location identifier slug.</param>
/// <param name="Name">The location display name.</param>
/// <param name="Type">The location type.</param>
/// <param name="Source">How the location was discovered (e.g. "explore", "quest", "rumor").</param>
public sealed record ZoneLocationUnlockedPayload(string Slug, string Name, string Type, string Source);

/// <summary>
/// Hub event payload received when the player searches the current area.
/// </summary>
/// <param name="Roll">The search check result value.</param>
/// <param name="AnyFound">Whether anything was found.</param>
public sealed record AreaSearchedPayload(int Roll, bool AnyFound);

/// <summary>
/// Hub event payload received when the player traverses a connection between locations or zones.
/// </summary>
/// <param name="Slug">The connection identifier slug.</param>
/// <param name="ZoneId">The zone identifier after traversal.</param>
/// <param name="IsCrossZone">Whether this traversal crossed zone boundaries.</param>
/// <param name="Connections">The connections available at the new location.</param>
public sealed record ConnectionTraversedPayload(
    string Slug,
    string ZoneId,
    bool IsCrossZone,
    IReadOnlyList<ZoneConnectionLink> Connections);
