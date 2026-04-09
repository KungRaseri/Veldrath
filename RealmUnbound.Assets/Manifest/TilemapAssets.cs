namespace RealmUnbound.Assets.Manifest;

/// <summary>Describes a single tileset spritesheet bundled with the game assets.</summary>
/// <param name="Key">Unique identifier for this tileset (e.g. <c>"onebit_packed"</c>).</param>
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
    /// Universal tileset from Kenney 1-Bit Pack — used for all zones.
    /// 49 columns × 22 rows, 16 px tiles, 0 px spacing, transparent background.
    /// Tile index constants are defined in <see cref="RealmEngine.Shared.Models.TileIndex"/>.
    /// </summary>
    public static readonly TilesetInfo OneBitPacked = new(
        Key: "onebit_packed",
        RelativePath: "GameAssets/tilemaps/sheets/onebit_packed.png",
        TileSize: 16,
        Spacing: 0,
        Columns: 49,
        Rows: 22);

    /// <summary>
    /// Roguelike tileset — used for all zone maps.
    /// 57 columns × 31 rows, 16 px tiles, 1 px spacing.
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
        [OneBitPacked.Key]  = OneBitPacked,
        [RoguelikeBase.Key] = RoguelikeBase,
    };
}
