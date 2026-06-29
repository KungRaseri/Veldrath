namespace Veldrath.Client.Rendering;

/// <summary>Broad classification of a tile for rendering and gameplay purposes.</summary>
public enum TileCategory
{
    /// <summary>Unknown or unmapped tile index.</summary>
    Unknown = 0,

    /// <summary>Transparent / empty cell (-1).</summary>
    Empty,

    /// <summary>Ground texture overlay (dead leaves, gravel, cobblestone, foliage).</summary>
    Ground,

    /// <summary>Solid terrain fill (stone wall, sand).</summary>
    Terrain,

    /// <summary>Water tiles (deep water).</summary>
    Water,

    /// <summary>Flora — trees, cacti, ground cover, boulders, mushrooms.</summary>
    Flora,

    /// <summary>Dirt path / road tiles.</summary>
    Path,

    /// <summary>Special / system tiles (blank, pending).</summary>
    Special,
}
