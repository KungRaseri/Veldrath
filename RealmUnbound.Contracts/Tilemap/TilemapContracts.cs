namespace RealmUnbound.Contracts.Tilemap;

// ── Tile Map Definition ────────────────────────────────────────────────────

/// <summary>Full tilemap definition for a single zone, sent to the client on zone entry.</summary>
/// <param name="ZoneId">The zone this map belongs to.</param>
/// <param name="TilesetKey">Identifier for the spritesheet used by this map (e.g. <c>"tiny_town"</c>, <c>"tiny_dungeon"</c>).</param>
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
/// <param name="Name">Logical layer name (e.g. <c>"ground"</c>, <c>"decor"</c>).</param>
/// <param name="Data">
/// Flat tile-index array of length <c>Width × Height</c>, row-major.
/// Index into the spritesheet: <c>row * columnCount + col</c>. -1 means no tile (transparent).
/// </param>
public record TileLayerDto(string Name, int[] Data);

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
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction.</param>
public record EnemyMovedPayload(Guid EntityId, int TileX, int TileY, string Direction);

/// <summary>
/// Hub broadcast payload emitted when fog-of-war tiles are revealed for the requesting player.
/// Only sent to the caller, not the whole zone group.
/// </summary>
/// <param name="TileKeys">Collection of newly-revealed tile keys in <c>"x,y"</c> format.</param>
public record FogRevealedPayload(IReadOnlyList<string> TileKeys);
