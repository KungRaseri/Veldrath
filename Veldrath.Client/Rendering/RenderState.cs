using Avalonia;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Rendering;

/// <summary>Complete render-state snapshot for a single frame.</summary>
/// <param name="Bounds">Pixel size of the control's drawable area.</param>
/// <param name="CameraX">Tile column of the viewport top-left corner.</param>
/// <param name="CameraY">Tile row of the viewport top-left corner.</param>
/// <param name="ViewportWidthTiles">Number of tile columns visible in the viewport.</param>
/// <param name="ViewportHeightTiles">Number of tile rows visible in the viewport.</param>
/// <param name="MapWidth">Total map width in tiles.</param>
/// <param name="MapHeight">Total map height in tiles.</param>
/// <param name="Layers">All tile layers, each with a flat Data array and ZIndex.</param>
/// <param name="CollisionMask">Flat Width×Height array: true = blocked.</param>
/// <param name="FogMask">Flat Width×Height array: true = fogged. May be empty (all false).</param>
/// <param name="RevealedTiles">Set of "x:y" keys for fog-revealed tiles.</param>
/// <param name="ExitHighlights">Exit tile positions to highlight (zone maps).</param>
/// <param name="ZoneEntryHighlights">Zone-entry positions to highlight (region maps).</param>
/// <param name="RegionExitHighlights">Region-exit positions to highlight (region maps).</param>
/// <param name="Entities">Live entity positions to draw.</param>
/// <param name="SelfEntityId">Local player's entity ID for distinct rendering.</param>
/// <param name="Labels">Zone labels (region maps only).</param>
/// <param name="IsMiniMapOpen">Whether the minimap overlay is active.</param>
/// <param name="TilesetKey">Spritesheet key (e.g. "onebit_packed"). Used by sprite renderer.</param>
/// <param name="ShowGrid">Whether tile-boundary grid lines should be drawn.</param>
/// <param name="MapType">Discriminator: "zone" or "region".</param>
public readonly record struct RenderState(
    Size Bounds,
    int CameraX, int CameraY,
    int ViewportWidthTiles, int ViewportHeightTiles,
    int MapWidth, int MapHeight,
    IReadOnlyList<TileLayerDto> Layers,
    bool[] CollisionMask,
    bool[] FogMask,
    IReadOnlySet<string> RevealedTiles,
    IReadOnlyList<(int X, int Y)> ExitHighlights,
    IReadOnlyList<(int X, int Y)> ZoneEntryHighlights,
    IReadOnlyList<(int X, int Y)> RegionExitHighlights,
    IReadOnlyList<RenderEntity> Entities,
    Guid? SelfEntityId,
    IReadOnlyList<RenderLabel> Labels,
    bool IsMiniMapOpen,
    string TilesetKey,
    string MapType,
    bool ShowGrid = false);

/// <summary>Lightweight entity snapshot for the renderer (avoids VM coupling).</summary>
/// <param name="EntityId">Unique identifier for this entity instance.</param>
/// <param name="EntityType">Type discriminator: <c>"player"</c> or <c>"enemy"</c>.</param>
/// <param name="SpriteKey">Named sprite key used to look up the sprite bitmap.</param>
/// <param name="TileX">Current tile column.</param>
/// <param name="TileY">Current tile row.</param>
/// <param name="Direction">Cardinal facing direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.</param>
public readonly record struct RenderEntity(
    Guid EntityId, string EntityType, string SpriteKey,
    int TileX, int TileY, string Direction);

/// <summary>Lightweight map label for the renderer.</summary>
/// <param name="TileX">Tile column of the label anchor.</param>
/// <param name="TileY">Tile row of the label anchor.</param>
/// <param name="Text">Display text of the label.</param>
/// <param name="IsHidden">Whether the label is hidden (e.g. unrevealed zone).</param>
public readonly record struct RenderLabel(
    int TileX, int TileY, string Text, bool IsHidden);
