namespace RealmUnbound.Contracts.Tilemap;

// ── Tile Map Definition ────────────────────────────────────────────────────

/// <summary>Full tilemap definition for a single zone, sent to the client on zone entry.</summary>
/// <param name="ZoneId">The zone this map belongs to.</param>
/// <param name="TilesetKey">Identifier for the spritesheet used by this map. All zones use <c>"onebit_packed"</c>.</param>
/// <param name="Width">Map width in tiles.</param>
/// <param name="Height">Map height in tiles.</param>
/// <param name="TileSize">Native tile size in pixels (always 16 for Kenney Tiny packs).</param>
/// <param name="Layers">Ordered render layers (ground first, decoration on top).</param>
/// <param name="CollisionMask">
/// Flat array of length <c>Width × Height</c>. <see langword="true"/> at index <c>y * Width + x</c>
/// means tile (x,y) is solid and blocks movement.
/// </param>
/// <param name="FogMask">
/// Flat array of length <c>Width × Height</c>. <see langword="true"/> at index <c>y * Width + x</c>
/// means the tile starts hidden under fog of war and is revealed when the player approaches.
/// All <see langword="false"/> for town zones.
/// </param>
/// <param name="ExitTiles">Tiles that trigger a zone transition when stepped on.</param>
/// <param name="SpawnPoints">Default player spawn positions for this map.</param>
public record TileMapDto(
    string ZoneId,
    string TilesetKey,
    int Width,
    int Height,
    int TileSize,
    IReadOnlyList<TileLayerDto> Layers,
    bool[] CollisionMask,
    bool[] FogMask,
    IReadOnlyList<ExitTileDto> ExitTiles,
    IReadOnlyList<SpawnPointDto> SpawnPoints);

/// <summary>A single render layer within a <see cref="TileMapDto"/>.</summary>
/// <param name="Name">Logical layer name (e.g. <c>"base"</c>, <c>"objects"</c>).</param>
/// <param name="Data">
/// Flat tile-index array of length <c>Width × Height</c>, row-major.
/// Index into the spritesheet: <c>row * columnCount + col</c>. -1 means no tile (transparent).
/// </param>
/// <param name="ZIndex">
/// Render z-order. Layers with <c>ZIndex &lt; 2</c> paint below entities;
/// <c>ZIndex &gt;= 2</c> paints above entities (roofs, canopies). Default 0.
/// </param>
public record TileLayerDto(string Name, int[] Data, int ZIndex = 0);

/// <summary>A tile that transitions the player to another zone when stepped on.</summary>
/// <param name="TileX">Column of the exit tile.</param>
/// <param name="TileY">Row of the exit tile.</param>
/// <param name="ToZoneId">Destination zone identifier.</param>
public record ExitTileDto(int TileX, int TileY, string ToZoneId);

/// <summary>A default spawn position for players entering the zone for the first time.</summary>
/// <param name="TileX">Column of the spawn tile.</param>
/// <param name="TileY">Row of the spawn tile.</param>
public record SpawnPointDto(int TileX, int TileY);

// ── Live Entity ────────────────────────────────────────────────────────────

/// <summary>
/// A live entity (player character, enemy, or NPC) positioned on the tile grid.
/// Broadcast to zone group members to keep position state in sync.
/// </summary>
/// <param name="EntityId">Unique instance identifier.</param>
/// <param name="EntityType">Broad category: <c>"player"</c>, <c>"enemy"</c>, or <c>"npc"</c>.</param>
/// <param name="SpriteKey">Key into the client sprite registry for rendering.</param>
/// <param name="TileX">Current tile column.</param>
/// <param name="TileY">Current tile row.</param>
/// <param name="Direction">Facing direction: <c>"N"</c>, <c>"S"</c>, <c>"E"</c>, or <c>"W"</c>.</param>
public record TileEntityDto(
    Guid EntityId,
    string EntityType,
    string SpriteKey,
    int TileX,
    int TileY,
    string Direction);

// ── Movement Payloads ─────────────────────────────────────────────────────

/// <summary>Hub broadcast payload emitted after a character successfully moves one tile.</summary>
/// <param name="CharacterId">The character that moved.</param>
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction.</param>
public record CharacterMovedPayload(Guid CharacterId, int TileX, int TileY, string Direction);

/// <summary>Hub broadcast payload emitted after an enemy moves one tile.</summary>
/// <param name="EntityId">Instance identifier of the enemy.</param>
/// <param name="SpriteKey">Sprite key used to render the entity; matches a key in <c>EntitySpriteAssets.All</c>.</param>
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction.</param>
public record EnemyMovedPayload(Guid EntityId, string SpriteKey, int TileX, int TileY, string Direction);

/// <summary>
/// Hub broadcast payload emitted when fog-of-war tiles are revealed for the requesting player.
/// Only sent to the caller, not the whole zone group.
/// </summary>
/// <param name="TileKeys">Collection of newly-revealed tile keys in <c>"x:y"</c> format.</param>
public record FogRevealedPayload(IReadOnlyList<string> TileKeys);

/// <summary>
/// Sent to a player who joins a zone that already has live entities (enemies/NPCs).
/// Also broadcast to the zone group when the first player enters and entities are spawned.
/// </summary>
/// <param name="Entities">Snapshot of all live entities currently in the zone.</param>
public record ZoneEntitiesSnapshotPayload(IReadOnlyList<TileEntityDto> Entities);

// ── Region Map ────────────────────────────────────────────────────────────────

/// <summary>Full tilemap definition for a region map, sent to the client after zone exit or login.</summary>
/// <param name="RegionId">Identifier of the region this map belongs to.</param>
/// <param name="TilesetKey">Identifier for the spritesheet (e.g. <c>"onebit_packed"</c>).</param>
/// <param name="Width">Map width in tiles.</param>
/// <param name="Height">Map height in tiles.</param>
/// <param name="TileSize">Native tile size in pixels.</param>
/// <param name="Layers">Ordered render layers (ground first, decoration on top).</param>
/// <param name="CollisionMask">
/// Flat array of length <c>Width × Height</c>. <see langword="true"/> at index <c>y * Width + x</c>
/// means the tile blocks movement.
/// </param>
/// <param name="Zones">Zone entry objects extracted from the <c>zones</c> object group.</param>
/// <param name="RegionExits">Region exit objects extracted from the <c>region_exits</c> object group.</param>
public record RegionMapDto(
    string RegionId,
    string TilesetKey,
    int Width,
    int Height,
    int TileSize,
    IReadOnlyList<TileLayerDto> Layers,
    bool[] CollisionMask,
    IReadOnlyList<ZoneObjectDto> Zones,
    IReadOnlyList<RegionExitDto> RegionExits);

/// <summary>A zone entry object placed on the region map.</summary>
/// <param name="ZoneId">Slug of the zone this object leads into.</param>
/// <param name="DisplayName">Human-readable zone name shown in the entry dialog.</param>
/// <param name="MinLevel">Minimum recommended level, or <see langword="null"/> if unrestricted.</param>
/// <param name="MaxLevel">Maximum recommended level, or <see langword="null"/> if unrestricted.</param>
/// <param name="TileX">Tile column of the entry object.</param>
/// <param name="TileY">Tile row of the entry object.</param>
public record ZoneObjectDto(string ZoneId, string DisplayName, int? MinLevel, int? MaxLevel, int TileX, int TileY);

/// <summary>A region exit object placed on the region map border.</summary>
/// <param name="ToRegionId">Slug of the adjacent region this exit leads to.</param>
/// <param name="TileX">Tile column of the exit object.</param>
/// <param name="TileY">Tile row of the exit object.</param>
public record RegionExitDto(string ToRegionId, int TileX, int TileY);

/// <summary>Hub broadcast payload emitted after a character moves one tile on the region map.</summary>
/// <param name="CharacterId">The character that moved.</param>
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction.</param>
public record PlayerMovedOnRegionPayload(Guid CharacterId, int TileX, int TileY, string Direction);

/// <summary>
/// Sent to the caller when they step onto a zone-entry tile on the region map.
/// The client should prompt the player with an "Enter Zone?" dialog.
/// </summary>
/// <param name="ZoneId">Slug of the zone the player is about to enter.</param>
/// <param name="DisplayName">Human-readable zone name to show in the prompt.</param>
public record ZoneEntryNearbyPayload(string ZoneId, string DisplayName);

/// <summary>
/// Sent to the caller when they step onto a region-exit tile on the region map.
/// The client should prompt the player with a "Travel to [region]?" dialog.
/// </summary>
/// <param name="ToRegionId">Slug of the adjacent region the player can travel to.</param>
public record RegionExitNearbyPayload(string ToRegionId);
