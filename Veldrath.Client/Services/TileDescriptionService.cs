namespace Veldrath.Client.Services;

/// <summary>
/// Maps tile indices to human-readable location descriptions.
/// Currently uses a static lookup derived from the <c>TileIndex</c> constant names.
/// Long-term, descriptions will come from Tiled custom properties via the server DTO.
/// </summary>
public static class TileDescriptionService
{
    // Key tile index → description mapping for common/notable tiles.
    // The fallback for unknown tiles is derived from the constant name via Humanizer.
    private static readonly Dictionary<int, string> Descriptions = new()
    {
        // Ground textures
        { 0,   "Dark, packed earth." },
        { 1,   "A scattering of dead leaves carpets the ground." },
        { 2,   "Light gravel crunches underfoot." },
        { 3,   "Worn cobblestones pave the way." },
        { 4,   "Neat stone tiles form a sturdy floor." },
        { 5,   "Sparse light foliage dots the area." },
        { 6,   "Medium undergrowth rustles in the breeze." },
        { 7,   "Thick grass fills the space, soft and green." },

        // Terrain
        { 49,  "A patch of sturdy grass." },
        { 50,  "Hard-packed dirt, firm underfoot." },
        { 51,  "Bare stone rises from the ground." },
        { 52,  "Soft sand shifts with each step." },
        { 98,  "Dense grass sways in the wind." },
        { 99,  "Loose dirt and small stones." },
        { 100, "Solid rock face, cool to the touch." },

        // Water
        { 147, "Dark, deep water. The surface is still." },

        // Flora / Trees
        { 196, "A slender tree with pale bark." },
        { 197, "A broad-leafed tree stands here." },
        { 198, "A tall pine reaches toward the sky." },
        { 199, "A lone cactus stands in the dry earth." },
        { 200, "A pair of cacti cluster together." },
        { 201, "Tall grass rises high above your head." },
        { 202, "Thick vines hang down from above." },
        { 203, "A climbing vine wraps around a support." },
        { 204, "Twin pines stand side by side." },
        { 205, "An enormous ancient tree towers overhead." },
        { 206, "A large boulder sits heavily on the ground." },
        { 207, "Dead vines curl and crack underfoot." },
        { 208, "A cluster of mushrooms grows in the damp." },

        // Path tiles
        { 245, "A dirt path stretches in four directions." },
        { 246, "A circular dirt clearing." },
        { 247, "A straight dirt path running north–south." },
        { 248, "A straight dirt path running east–west." },
        { 249, "A dirt path curving from north to east." },
        { 250, "A dirt path curving from north to west." },
        { 251, "A dirt path curving from south to east." },
        { 252, "A dirt path curving from south to west." },
        { 253, "A dirt T-junction opening to the west." },
        { 254, "A dirt T-junction opening to the east." },
        { 255, "A dirt T-junction opening to the south." },
        { 256, "A dirt T-junction opening to the north." },
        { 257, "The northern end of a dirt path." },
        { 258, "The southern end of a dirt path." },
        { 259, "The eastern end of a dirt path." },
        { 260, "The western end of a dirt path." },

        // Structures (sample)
        { 294, "A weathered wooden wall." },
        { 343, "A sturdy stone wall." },
        { 392, "A rough-hewn wooden floor." },
        { 441, "A cold stone floor, worn smooth by countless steps." },
    };

    /// <summary>
    /// Returns a human-readable description for the given tile index.
    /// Falls back to a generated label based on the tile index value.
    /// </summary>
    public static string GetDescription(int tileIndex)
    {
        if (Descriptions.TryGetValue(tileIndex, out var description))
            return description;

        // Fallback: generate a label from the index range.
        return tileIndex switch
        {
            < 0   => "Void — nothingness.",
            >= 0 and < 49  => "Ground texture.",
            >= 49  and < 98  => "Terrain feature.",
            >= 98  and < 147 => "Dense terrain.",
            >= 147 and < 196 => "Water or liquid.",
            >= 196 and < 245 => "Flora or vegetation.",
            >= 245 and < 294 => "Path or roadway.",
            >= 294 and < 343 => "Wooden structure.",
            >= 343 and < 392 => "Stone structure.",
            >= 392 and < 441 => "Wooden floor.",
            >= 441 and < 490 => "Stone floor.",
            _ => "Unknown terrain."
        };
    }

    /// <summary>
    /// Returns a description that combines the base terrain tile and any notable
    /// object tile at the given position within a layer array.
    /// </summary>
    /// <param name="baseLayerData">The base terrain layer (flat array).</param>
    /// <param name="objectLayerData">The objects/decorations layer (flat array), or null.</param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    public static string GetLocationDescription(int[] baseLayerData, int[]? objectLayerData, int width, int x, int y)
    {
        var idx = y * width + x;

        // Get description for the base terrain
        var baseTile = idx < baseLayerData.Length ? baseLayerData[idx] : 0;
        var baseDesc = GetDescription(baseTile);

        // Check for a notable object on top
        if (objectLayerData is not null && idx < objectLayerData.Length)
        {
            var objTile = objectLayerData[idx];
            if (objTile > 0 && objTile != baseTile)
            {
                var objDesc = GetDescription(objTile);
                return $"{baseDesc} {objDesc}";
            }
        }

        return baseDesc;
    }
}
