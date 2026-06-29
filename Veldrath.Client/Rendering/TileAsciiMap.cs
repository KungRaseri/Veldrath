using Avalonia.Media;
using RealmEngine.Shared.Models;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Static mapping of tile index → ASCII character for text-based map rendering.
/// Uses the canonical <see cref="TileIndex"/> constants from <c>RealmEngine.Shared</c>.
/// </summary>
public static class TileAsciiMap
{
    /// <summary>
    /// Returns the ASCII character for the given tile index.
    /// Returns <c>' '</c> for transparent (-1), <c>'.'</c> for blank (0), and <c>'?'</c> for unknown indices.
    /// </summary>
    /// <param name="tileIndex">The tile index from the tileset.</param>
    /// <returns>The display character for the tile.</returns>
    public static char GetChar(int tileIndex) => tileIndex switch
    {
        // Special
        -1                                 => ' ',
        TileIndex.Blank                    => '.',
        TileIndex.Pending                  => '?',

        // Ground textures
        TileIndex.Ground.DeadLeaves        => ',',
        TileIndex.Ground.LightGravel       => ':',
        TileIndex.Ground.Cobblestone       => ';',
        TileIndex.Ground.StoneTile         => '=',
        TileIndex.Ground.LightFoliage      => '"',
        TileIndex.Ground.MedFoliage        => '\'',
        TileIndex.Ground.GrassFill         => '"',

        // Terrain
        TileIndex.Terrain.Stone.M          => '#',
        TileIndex.Terrain.Sand.M           => '~',

        // Water
        TileIndex.Water.Deep               => '≈',

        // Flora — trees
        TileIndex.Flora.TreeA              => 'T',
        TileIndex.Flora.TreeB              => 'T',
        TileIndex.Flora.TreeC              => 'T',
        TileIndex.Flora.TreeD              => 'T',
        TileIndex.Flora.TreeE              => 'T',
        TileIndex.Flora.Pine               => '▲',
        TileIndex.Flora.DualPine           => '▲',
        TileIndex.Flora.BigTree            => 'T',

        // Flora — cacti
        TileIndex.Flora.Cactus             => 'Y',
        TileIndex.Flora.CactusDual         => 'Y',

        // Flora — ground cover
        TileIndex.Flora.TallGrass          => 'i',
        TileIndex.Flora.Vines              => 'v',
        TileIndex.Flora.ClimbingVine       => 'v',
        TileIndex.Flora.DeadVines          => 'v',
        TileIndex.Flora.Boulder            => 'O',
        TileIndex.Flora.Mushroom           => '*',

        // Dirt paths
        TileIndex.DirtPath.FourWay         => '+',
        TileIndex.DirtPath.Circle          => 'o',
        TileIndex.DirtPath.StraightH       => '-',
        TileIndex.DirtPath.StraightV       => '|',
        TileIndex.DirtPath.CornerTL        => '+',
        TileIndex.DirtPath.CornerTR        => '+',
        TileIndex.DirtPath.CornerBL        => '+',
        TileIndex.DirtPath.CornerBR        => '+',
        TileIndex.DirtPath.TJunctionLBR    => '+',
        TileIndex.DirtPath.TJunctionLTR    => '+',
        TileIndex.DirtPath.TJunctionTBR    => '+',
        TileIndex.DirtPath.TJunctionLTB    => '+',
        TileIndex.DirtPath.EndTop          => '|',
        TileIndex.DirtPath.EndBottom       => '|',
        TileIndex.DirtPath.EndRight        => '-',

        _ => '?',
    };

    /// <summary>
    /// Returns the foreground <see cref="Color"/> for the given tile index.
    /// Used to colorise ASCII characters per tile type.
    /// </summary>
    /// <param name="tileIndex">The tile index from the tileset.</param>
    /// <returns>A display colour for the tile character.</returns>
    public static Color GetColor(int tileIndex) => tileIndex switch
    {
        -1 => Color.FromRgb(15, 23, 42), // dark background

        // Ground — green tones
        TileIndex.Blank                     => Color.FromRgb(74, 222, 128),
        TileIndex.Ground.DeadLeaves         => Color.FromRgb(74, 222, 128),
        TileIndex.Ground.LightGravel        => Color.FromRgb(148, 163, 184),
        TileIndex.Ground.Cobblestone        => Color.FromRgb(148, 163, 184),
        TileIndex.Ground.StoneTile          => Color.FromRgb(148, 163, 184),
        TileIndex.Ground.LightFoliage       => Color.FromRgb(34, 197, 94),
        TileIndex.Ground.MedFoliage         => Color.FromRgb(34, 197, 94),
        TileIndex.Ground.GrassFill          => Color.FromRgb(74, 222, 128),

        // Terrain
        TileIndex.Terrain.Stone.M           => Color.FromRgb(148, 163, 184),
        TileIndex.Terrain.Sand.M            => Color.FromRgb(251, 191, 36),

        // Water
        TileIndex.Water.Deep                => Color.FromRgb(96, 165, 250),

        // Flora
        TileIndex.Flora.TreeA               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.TreeB               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.TreeC               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.TreeD               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.TreeE               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.Pine                => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.DualPine            => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.BigTree             => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.Cactus              => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.CactusDual          => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.TallGrass           => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.Vines               => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.ClimbingVine        => Color.FromRgb(34, 197, 94),
        TileIndex.Flora.DeadVines           => Color.FromRgb(148, 163, 184),
        TileIndex.Flora.Boulder             => Color.FromRgb(148, 163, 184),
        TileIndex.Flora.Mushroom            => Color.FromRgb(34, 197, 94),

        // Paths — tan
        TileIndex.DirtPath.FourWay          => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.Circle           => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.StraightH        => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.StraightV        => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.CornerTL         => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.CornerTR         => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.CornerBL         => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.CornerBR         => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.TJunctionLBR     => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.TJunctionLTR     => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.TJunctionTBR     => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.TJunctionLTB     => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.EndTop           => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.EndBottom        => Color.FromRgb(212, 165, 116),
        TileIndex.DirtPath.EndRight         => Color.FromRgb(212, 165, 116),

        _ => Color.FromRgb(255, 0, 255), // magenta = unknown
    };
}
