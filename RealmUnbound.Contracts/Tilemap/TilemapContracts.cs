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

// ── Region Map Definition ──────────────────────────────────────────────────

/// <summary>Full tilemap definition for a region overview map, sent to the client on character selection.</summary>
/// <param name="RegionId">The region this map belongs to (e.g. <c>"thornveil"</c>).</param>
/// <param name="TilesetKey">Identifier for the spritesheet used by this map.</param>
/// <param name="Width">Map width in tiles.</param>
/// <param name="Height">Map height in tiles.</param>
/// <param name="TileSize">Native tile size in pixels.</param>
/// <param name="Layers">Ordered render layers (ground first, decoration on top).</param>
/// <param name="CollisionMask">
/// Flat array of length <c>Width × Height</c>. <see langword="true"/> at index <c>y * Width + x</c>
/// means tile (x,y) is solid and blocks movement.
/// </param>
/// <param name="ZoneEntries">Zone-entry points placed on this region map (from the <c>zones</c> objectgroup layer).</param>
/// <param name="RegionExits">Border crossings to adjacent regions (from the <c>region_exits</c> objectgroup layer).</param>
/// <param name="Labels">Zone-label overlays for this region map (from the <c>labels</c> objectgroup layer).</param>
/// <param name="Paths">Road and path polylines for this region map (from the <c>paths</c> objectgroup layer).</param>
public record RegionMapDto(
    string RegionId,
    string TilesetKey,
    int Width,
    int Height,
    int TileSize,
    IReadOnlyList<TileLayerDto> Layers,
    bool[] CollisionMask,
    IReadOnlyList<ZoneObjectDto> ZoneEntries,
    IReadOnlyList<RegionExitDto> RegionExits,
    IReadOnlyList<ZoneLabelDto> Labels,
    IReadOnlyList<RegionPathDto> Paths);

/// <summary>A zone-entry point on a region map.</summary>
/// <param name="TileX">Tile column of the entry point.</param>
/// <param name="TileY">Tile row of the entry point.</param>
/// <param name="ZoneSlug">Slug of the zone to enter (e.g. <c>"fenwick-crossing"</c>).</param>
/// <param name="DisplayName">Human-readable zone name.</param>
/// <param name="MinLevel">Minimum suggested character level. 0 when unset.</param>
/// <param name="MaxLevel">Maximum suggested character level. 0 when unset.</param>
public record ZoneObjectDto(int TileX, int TileY, string ZoneSlug, string DisplayName, int MinLevel, int MaxLevel);

/// <summary>A border crossing on a region map that leads to an adjacent region.</summary>
/// <param name="TileX">Tile column of the crossing.</param>
/// <param name="TileY">Tile row of the crossing.</param>
/// <param name="TargetRegionId">Slug of the adjacent region (e.g. <c>"greymoor"</c>).</param>
public record RegionExitDto(int TileX, int TileY, string TargetRegionId);

/// <summary>A zone-label overlay on a region map.</summary>
/// <param name="TileX">Tile column of the label anchor point.</param>
/// <param name="TileY">Tile row of the label anchor point.</param>
/// <param name="Text">Display text for the label.</param>
/// <param name="ZoneSlug">Zone slug this label refers to. Empty for region-exit labels.</param>
public record ZoneLabelDto(int TileX, int TileY, string Text, string ZoneSlug);

/// <summary>A road or path polyline on a region map.</summary>
/// <param name="Name">Unique name of the path.</param>
/// <param name="Points">Ordered tile-space points that make up the polyline.</param>
public record RegionPathDto(string Name, IReadOnlyList<RegionPathPointDto> Points);

/// <summary>A single tile-space point on a <see cref="RegionPathDto"/>.</summary>
/// <param name="TileX">Tile column.</param>
/// <param name="TileY">Tile row.</param>
public record RegionPathPointDto(float TileX, float TileY);

// ── Region Movement Payloads ───────────────────────────────────────────────

/// <summary>Hub broadcast payload emitted after a character successfully moves one tile on the region map.</summary>
/// <param name="CharacterId">The character that moved.</param>
/// <param name="TileX">New tile column.</param>
/// <param name="TileY">New tile row.</param>
/// <param name="Direction">New facing direction.</param>
public record RegionPlayerMovedPayload(Guid CharacterId, int TileX, int TileY, string Direction);
