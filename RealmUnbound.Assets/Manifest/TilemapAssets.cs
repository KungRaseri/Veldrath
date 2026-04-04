namespace RealmUnbound.Assets.Manifest;

/// <summary>Describes a single tileset spritesheet bundled with the game assets.</summary>
/// <param name="Key">Unique identifier for this tileset (e.g. <c>"tiny_town"</c>).</param>
/// <param name="RelativePath">Path to the PNG relative to the <c>GameAssets/tilemaps/sheets/</c> base directory.</param>
/// <param name="TileSize">Native tile size in pixels (width = height).</param>
/// <param name="Spacing">Gap in pixels between adjacent tiles in the sheet.</param>
/// <param name="Columns">Number of tile columns in the sheet.</param>
/// <param name="Rows">Number of tile rows in the sheet.</param>
public record TilesetInfo(
    string Key,
    string RelativePath,
    int TileSize,
    int Spacing,
    int Columns,
    int Rows);

/// <summary>
/// Compile-time catalog of all tileset spritesheets shipped with RealmUnbound.
/// The <see cref="RelativePath"/> on each entry is relative to the
/// <c>GameAssets/tilemaps/sheets/</c> output directory.
/// </summary>
public static class TilemapAssets
{
    /// <summary>
    /// Town / outdoor tileset from Kenney Tiny Town.
    /// 12 × 11 sheet, 16px tiles, 1px spacing, transparent background.
    /// </summary>
    public static readonly TilesetInfo TinyTown = new(
        Key: "tiny_town",
        RelativePath: "GameAssets/tilemaps/sheets/tiny_town.png",
        TileSize: 16,
        Spacing: 1,
        Columns: 12,
        Rows: 11);

    /// <summary>
    /// Dungeon tileset from Kenney Tiny Dungeon; includes character sprites in the lower rows.
    /// 12 × 11 sheet, 16px tiles, 1px spacing, transparent background.
    /// </summary>
    public static readonly TilesetInfo TinyDungeon = new(
        Key: "tiny_dungeon",
        RelativePath: "GameAssets/tilemaps/sheets/tiny_dungeon.png",
        TileSize: 16,
        Spacing: 1,
        Columns: 12,
        Rows: 11);

    /// <summary>
    /// Wilderness / mixed content tileset from Kenney Roguelike Base Pack.
    /// Large sheet (57 columns, 31 rows), 16px tiles, 1px spacing.
    /// </summary>
    public static readonly TilesetInfo RoguelikeBase = new(
        Key: "roguelike_base",
        RelativePath: "GameAssets/tilemaps/sheets/roguelike_base.png",
        TileSize: 16,
        Spacing: 1,
        Columns: 57,
        Rows: 31);

    /// <summary>All registered tilesets, keyed by <see cref="TilesetInfo.Key"/>.</summary>
    public static readonly IReadOnlyDictionary<string, TilesetInfo> All = new Dictionary<string, TilesetInfo>(StringComparer.OrdinalIgnoreCase)
    {
        [TinyTown.Key] = TinyTown,
        [TinyDungeon.Key] = TinyDungeon,
        [RoguelikeBase.Key] = RoguelikeBase,
    };
}
