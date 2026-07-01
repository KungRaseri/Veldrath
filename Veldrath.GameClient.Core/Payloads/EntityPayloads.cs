namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when a character moves within the zone.
/// Used to update the player's own position or track another character's movement.
/// </summary>
/// <param name="CharacterId">The character that moved.</param>
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction (<c>"N"</c>, <c>"S"</c>, <c>"E"</c>, <c>"W"</c>).</param>
public sealed record CharacterMovedPayload(Guid CharacterId, int TileX, int TileY, string Direction);

/// <summary>
/// Hub event payload received with a snapshot of all live entities in the zone.
/// Typically sent on zone entry and periodically to sync state.
/// </summary>
/// <param name="Entities">Snapshot of all live entities currently in the zone.</param>
public sealed record ZoneEntitiesSnapshotPayload(IReadOnlyList<TileEntityDtoEntry> Entities);

/// <summary>
/// A single entity entry within a <see cref="ZoneEntitiesSnapshotPayload"/>.
/// </summary>
/// <param name="EntityId">Unique instance identifier.</param>
/// <param name="EntityType">Broad category: <c>"player"</c>, <c>"enemy"</c>, or <c>"npc"</c>.</param>
/// <param name="SpriteKey">Key into the client sprite registry for rendering.</param>
/// <param name="TileX">Current tile column.</param>
/// <param name="TileY">Current tile row.</param>
/// <param name="Direction">Facing direction (<c>"N"</c>, <c>"S"</c>, <c>"E"</c>, <c>"W"</c>).</param>
public sealed record TileEntityDtoEntry(
    Guid EntityId,
    string EntityType,
    string SpriteKey,
    int TileX,
    int TileY,
    string Direction);

/// <summary>
/// Hub event payload received when an enemy is defeated.
/// Contains the character identifier for context.
/// </summary>
/// <param name="CharacterId">The character that defeated the enemy.</param>
public sealed record EnemyDefeatedPayload(Guid CharacterId);
