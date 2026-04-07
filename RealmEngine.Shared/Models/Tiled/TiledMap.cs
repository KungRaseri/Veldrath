using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealmEngine.Shared.Models.Tiled;

// ── Root Map ─────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a Tiled map in TMX/TMJ format.
/// For TMX files this is populated by <c>TiledMapParser</c>; for TMJ files by <c>System.Text.Json</c>.
/// The tile layer <see cref="TiledLayer.Data"/> arrays use 1-based Global Tile IDs (GIDs)
/// as defined by Tiled — GID 0 means transparent/empty.
/// Use <see cref="RealmEngine.Shared.Models.Tiled.TiledMapGameExtensions"/> for engine-logic helpers.
/// </summary>
public class TiledMap
{
    /// <summary>Number of tile columns.</summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>Number of tile rows.</summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>Map grid tile width in pixels.</summary>
    [JsonPropertyName("tilewidth")]
    public int TileWidth { get; set; } = 16;

    /// <summary>Map grid tile height in pixels.</summary>
    [JsonPropertyName("tileheight")]
    public int TileHeight { get; set; } = 16;

    /// <summary>Map orientation: <c>orthogonal</c>, <c>isometric</c>, <c>staggered</c>, or <c>hexagonal</c>.</summary>
    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "orthogonal";

    /// <summary>Render order: <c>right-down</c> (default), <c>right-up</c>, <c>left-down</c>, or <c>left-up</c>.</summary>
    [JsonPropertyName("renderorder")]
    public string RenderOrder { get; set; } = "right-down";

    /// <summary>Whether the map uses infinite dimensions with a chunked tile layer format.</summary>
    [JsonPropertyName("infinite")]
    public bool Infinite { get; set; }

    /// <summary>The Tiled application version used to save this file (e.g. <c>"1.12.1"</c>).</summary>
    [JsonPropertyName("tiledversion")]
    public string TiledVersion { get; set; } = string.Empty;

    /// <summary>The Tiled map format version (e.g. <c>"1.10"</c>).</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Always <c>"map"</c> for map files (TMJ).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "map";

    /// <summary>User-assigned class for the map (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>Hex-formatted background colour (<c>#RRGGBB</c> or <c>#AARRGGBB</c>). Optional.</summary>
    [JsonPropertyName("backgroundcolor")]
    public string? BackgroundColor { get; set; }

    /// <summary>Auto-incremented counter used to assign unique layer IDs.</summary>
    [JsonPropertyName("nextlayerid")]
    public int NextLayerId { get; set; }

    /// <summary>Auto-incremented counter used to assign unique object IDs.</summary>
    [JsonPropertyName("nextobjectid")]
    public int NextObjectId { get; set; }

    /// <summary>X-coordinate of the parallax origin in pixels (since Tiled 1.8).</summary>
    [JsonPropertyName("parallaxoriginx")]
    public double ParallaxOriginX { get; set; }

    /// <summary>Y-coordinate of the parallax origin in pixels (since Tiled 1.8).</summary>
    [JsonPropertyName("parallaxoriginy")]
    public double ParallaxOriginY { get; set; }

    /// <summary>
    /// X stagger axis offset applied per tile row (oblique maps only).
    /// Zero for orthogonal maps.
    /// </summary>
    [JsonPropertyName("skewx")]
    public int SkewX { get; set; }

    /// <summary>
    /// Y stagger axis offset applied per tile column (oblique maps only).
    /// Zero for orthogonal maps.
    /// </summary>
    [JsonPropertyName("skewy")]
    public int SkewY { get; set; }

    /// <summary>Hex-tile side length in pixels. Only used for hexagonal maps.</summary>
    [JsonPropertyName("hexsidelength")]
    public int HexSideLength { get; set; }

    /// <summary>Stagger axis (<c>x</c> or <c>y</c>). Only used for staggered/hexagonal maps.</summary>
    [JsonPropertyName("staggeraxis")]
    public string? StaggerAxis { get; set; }

    /// <summary>Stagger index (<c>odd</c> or <c>even</c>). Only used for staggered/hexagonal maps.</summary>
    [JsonPropertyName("staggerindex")]
    public string? StaggerIndex { get; set; }

    /// <summary>Compression level for tile layer data. <c>-1</c> means algorithm default.</summary>
    [JsonPropertyName("compressionlevel")]
    public int CompressionLevel { get; set; } = -1;

    /// <summary>Ordered set of layers (tile layers, object groups, image layers, or layer groups).</summary>
    [JsonPropertyName("layers")]
    public List<TiledLayer> Layers { get; set; } = [];

    /// <summary>
    /// Tileset references used by this map. Each entry has a <see cref="TiledTileset.FirstGid"/>
    /// and either inline tileset data or a <see cref="TiledTileset.Source"/> path to an external <c>.tsx</c> file.
    /// </summary>
    [JsonPropertyName("tilesets")]
    public List<TiledTileset> Tilesets { get; set; } = [];

    /// <summary>Custom properties defined in Tiled's Properties panel.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}

// ── Layer ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A single layer within a <see cref="TiledMap"/>. Covers all Tiled layer types:
/// <c>tilelayer</c>, <c>objectgroup</c>, <c>imagelayer</c>, and <c>group</c>.
/// Use the <see cref="Type"/> discriminator to determine which fields are populated.
/// </summary>
public class TiledLayer
{
    /// <summary>Unique layer ID, auto-incremented within the map.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Layer type: <c>tilelayer</c>, <c>objectgroup</c>, <c>imagelayer</c>, or <c>group</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Layer name as set in the Tiled editor.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>User-assigned class for this layer (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>Whether the layer is visible in the editor (and at runtime when rendering).</summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    /// <summary>Opacity of the layer (0.0–1.0, default 1).</summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    /// <summary>Horizontal layer offset in pixels relative to the map origin.</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>Vertical layer offset in pixels relative to the map origin.</summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>Additional horizontal pixel offset (decimal, default 0).</summary>
    [JsonPropertyName("offsetx")]
    public double OffsetX { get; set; }

    /// <summary>Additional vertical pixel offset (decimal, default 0).</summary>
    [JsonPropertyName("offsety")]
    public double OffsetY { get; set; }

    /// <summary>Horizontal parallax factor (default 1, since Tiled 1.5).</summary>
    [JsonPropertyName("parallaxx")]
    public double ParallaxX { get; set; } = 1.0;

    /// <summary>Vertical parallax factor (default 1, since Tiled 1.5).</summary>
    [JsonPropertyName("parallaxy")]
    public double ParallaxY { get; set; } = 1.0;

    /// <summary>Hex-formatted tint colour multiplied with rendered graphics. Optional.</summary>
    [JsonPropertyName("tintcolor")]
    public string? TintColor { get; set; }

    /// <summary>Whether this layer is locked (not editable) in the Tiled editor.</summary>
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    /// <summary>Blend mode for rendering this layer (e.g. <c>normal</c>, <c>add</c>, <c>multiply</c>).</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>Custom properties on this layer.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];

    // ── tilelayer fields ──────────────────────────────────────────────────────

    /// <summary>Column count. Same as map width for fixed-size maps. <c>tilelayer</c> only.</summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>Row count. Same as map height for fixed-size maps. <c>tilelayer</c> only.</summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// Flat array of 1-based Global Tile IDs (GIDs), row-major order. GID 0 = empty/transparent.
    /// The upper 3 bits encode flip flags (horizontal/vertical/anti-diagonal).
    /// <c>tilelayer</c> only.
    /// </summary>
    [JsonPropertyName("data")]
    public List<int>? Data { get; set; }

    /// <summary>
    /// Encoding for the tile data: <c>csv</c> (default) or <c>base64</c>. <c>tilelayer</c> only.
    /// <c>null</c> means raw array (TMJ with no explicit encoding).
    /// </summary>
    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    /// <summary>
    /// Compression for base64 data: <c>zlib</c>, <c>gzip</c>, <c>zstd</c>, or <c>""</c> (none).
    /// <c>tilelayer</c> only. Only applicable when <see cref="Encoding"/> is <c>base64</c>.
    /// </summary>
    [JsonPropertyName("compression")]
    public string? Compression { get; set; }

    /// <summary>
    /// Array of chunks for infinite maps. Each chunk has its own tile data, position, and size.
    /// <c>tilelayer</c> only. Null for fixed-size maps.
    /// </summary>
    [JsonPropertyName("chunks")]
    public List<TiledChunk>? Chunks { get; set; }

    /// <summary>
    /// X coordinate where layer content starts (infinite maps only).
    /// </summary>
    [JsonPropertyName("startx")]
    public int? StartX { get; set; }

    /// <summary>
    /// Y coordinate where layer content starts (infinite maps only).
    /// </summary>
    [JsonPropertyName("starty")]
    public int? StartY { get; set; }

    // ── objectgroup fields ────────────────────────────────────────────────────

    /// <summary>
    /// Draw order for object rendering: <c>topdown</c> (default) or <c>index</c>.
    /// <c>objectgroup</c> only.
    /// </summary>
    [JsonPropertyName("draworder")]
    public string? DrawOrder { get; set; }

    /// <summary>Objects contained in this layer. <c>objectgroup</c> only.</summary>
    [JsonPropertyName("objects")]
    public List<TiledObject> Objects { get; set; } = [];

    // ── imagelayer fields ─────────────────────────────────────────────────────

    /// <summary>Path to the image used by this layer. <c>imagelayer</c> only.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>Width of the image in pixels (since Tiled 1.11.1). <c>imagelayer</c> only.</summary>
    [JsonPropertyName("imagewidth")]
    public int? ImageWidth { get; set; }

    /// <summary>Height of the image in pixels (since Tiled 1.11.1). <c>imagelayer</c> only.</summary>
    [JsonPropertyName("imageheight")]
    public int? ImageHeight { get; set; }

    /// <summary>Whether the image is tiled along the X axis (since Tiled 1.8). <c>imagelayer</c> only.</summary>
    [JsonPropertyName("repeatx")]
    public bool? RepeatX { get; set; }

    /// <summary>Whether the image is tiled along the Y axis (since Tiled 1.8). <c>imagelayer</c> only.</summary>
    [JsonPropertyName("repeaty")]
    public bool? RepeatY { get; set; }

    /// <summary>Hex-formatted transparent colour for the image. <c>imagelayer</c> only.</summary>
    [JsonPropertyName("transparentcolor")]
    public string? TransparentColor { get; set; }

    // ── group fields ──────────────────────────────────────────────────────────

    /// <summary>Child layers when this layer is of type <c>group</c>.</summary>
    [JsonPropertyName("layers")]
    public List<TiledLayer>? ChildLayers { get; set; }
}

// ── Chunk ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A chunk of tile data used in infinite maps.
/// Each chunk has its own position and data array.
/// </summary>
public class TiledChunk
{
    /// <summary>Array of GIDs in row-major order for this chunk.</summary>
    [JsonPropertyName("data")]
    public List<int> Data { get; set; } = [];

    /// <summary>Row count of this chunk.</summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>Column count of this chunk.</summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>X coordinate of this chunk in tiles.</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>Y coordinate of this chunk in tiles.</summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }
}

// ── Object ────────────────────────────────────────────────────────────────────

/// <summary>
/// A Tiled object placed in an <c>objectgroup</c> layer.
/// The shape is determined by the presence of <see cref="Point"/>, <see cref="Ellipse"/>,
/// <see cref="Polygon"/>, <see cref="Polyline"/>, <see cref="Text"/>, or <see cref="Gid"/> flags/properties.
/// A plain rectangle has none of those special flags.
/// </summary>
public class TiledObject
{
    /// <summary>Unique object ID within the map.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Name assigned to this object in the editor.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Object class (type). In Tiled 1.8 and earlier this was called <c>type</c>;
    /// in 1.9 it was renamed <c>class</c>; in 1.10 it reverted to <c>type</c> for objects.
    /// Both names are supported here to handle all versions.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>X coordinate in pixels from the map origin.</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>Y coordinate in pixels from the map origin.</summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>Width of the object in pixels.</summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>Height of the object in pixels.</summary>
    [JsonPropertyName("height")]
    public double Height { get; set; }

    /// <summary>Rotation in degrees clockwise (default 0).</summary>
    [JsonPropertyName("rotation")]
    public double Rotation { get; set; }

    /// <summary>Opacity of the object (0.0–1.0, default 1, since Tiled 1.12).</summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    /// <summary>Whether this object is visible in the editor.</summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    /// <summary>When <see langword="true"/> this object is a point (zero-size location marker).</summary>
    [JsonPropertyName("point")]
    public bool? Point { get; set; }

    /// <summary>When <see langword="true"/> this object is an ellipse.</summary>
    [JsonPropertyName("ellipse")]
    public bool? Ellipse { get; set; }

    /// <summary>When <see langword="true"/> this object is a capsule shape.</summary>
    [JsonPropertyName("capsule")]
    public bool? Capsule { get; set; }

    /// <summary>
    /// Global tile ID when this object represents a placed tile (a tile object).
    /// Null for non-tile objects.
    /// </summary>
    [JsonPropertyName("gid")]
    public int? Gid { get; set; }

    /// <summary>Polygon vertex list (relative to object position). Non-null only for polygon objects.</summary>
    [JsonPropertyName("polygon")]
    public List<TiledPoint>? Polygon { get; set; }

    /// <summary>Polyline vertex list (relative to object position). Non-null only for polyline objects.</summary>
    [JsonPropertyName("polyline")]
    public List<TiledPoint>? Polyline { get; set; }

    /// <summary>Text data. Non-null only for text objects.</summary>
    [JsonPropertyName("text")]
    public TiledText? Text { get; set; }

    /// <summary>Template path reference when this object is a template instance.</summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }

    /// <summary>Custom properties on this object.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}

// ── Text ──────────────────────────────────────────────────────────────────────

/// <summary>Text content and formatting for a Tiled text object.</summary>
public class TiledText
{
    /// <summary>The text string.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Font family (default <c>sans-serif</c>).</summary>
    [JsonPropertyName("fontfamily")]
    public string FontFamily { get; set; } = "sans-serif";

    /// <summary>Pixel size of the font (default 16).</summary>
    [JsonPropertyName("pixelsize")]
    public int PixelSize { get; set; } = 16;

    /// <summary>Whether text is wrapped within the object bounds.</summary>
    [JsonPropertyName("wrap")]
    public bool Wrap { get; set; }

    /// <summary>Hex-formatted text colour (default <c>#000000</c>).</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";

    /// <summary>Whether bold font is used.</summary>
    [JsonPropertyName("bold")]
    public bool Bold { get; set; }

    /// <summary>Whether italic font is used.</summary>
    [JsonPropertyName("italic")]
    public bool Italic { get; set; }

    /// <summary>Whether text is underlined.</summary>
    [JsonPropertyName("underline")]
    public bool Underline { get; set; }

    /// <summary>Whether text has a strikeout.</summary>
    [JsonPropertyName("strikeout")]
    public bool Strikeout { get; set; }

    /// <summary>Whether kerning is applied.</summary>
    [JsonPropertyName("kerning")]
    public bool Kerning { get; set; } = true;

    /// <summary>Horizontal alignment: <c>left</c> (default), <c>center</c>, <c>right</c>, or <c>justify</c>.</summary>
    [JsonPropertyName("halign")]
    public string HAlign { get; set; } = "left";

    /// <summary>Vertical alignment: <c>top</c> (default), <c>center</c>, or <c>bottom</c>.</summary>
    [JsonPropertyName("valign")]
    public string VAlign { get; set; } = "top";
}

// ── Point ─────────────────────────────────────────────────────────────────────

/// <summary>A 2D point used in polygon and polyline definitions.</summary>
public class TiledPoint
{
    /// <summary>X coordinate in pixels, relative to the parent object's position.</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>Y coordinate in pixels, relative to the parent object's position.</summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }
}

// ── Property ─────────────────────────────────────────────────────────────────

/// <summary>
/// A custom property as defined in the Tiled Properties panel.
/// The <see cref="Value"/> is a <see cref="JsonElement"/> to support all property types
/// (string, int, float, bool, color, file, object reference, or class).
/// Use <see cref="RealmEngine.Shared.Models.Tiled.TiledPropertyExtensions"/> helpers for safe extraction.
/// </summary>
public class TiledProperty
{
    /// <summary>Property name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Property type: <c>string</c> (default), <c>int</c>, <c>float</c>, <c>bool</c>,
    /// <c>color</c>, <c>file</c>, <c>object</c>, or <c>class</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>Name of the custom property type when <see cref="Type"/> is <c>class</c> (since Tiled 1.8).</summary>
    [JsonPropertyName("propertytype")]
    public string? PropertyType { get; set; }

    /// <summary>
    /// The property value. Use <see cref="TiledPropertyExtensions.AsString"/>,
    /// <see cref="TiledPropertyExtensions.AsInt"/>, or related helpers for typed access.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

/// <summary>Extension helpers for reading <see cref="TiledProperty"/> values safely.</summary>
public static class TiledPropertyExtensions
{
    /// <summary>Returns the property value as a string, or <paramref name="defaultValue"/> if not a string.</summary>
    public static string? AsString(this TiledProperty prop, string? defaultValue = null) =>
        prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : defaultValue;

    /// <summary>Returns the property value as an <see cref="int"/>, or <paramref name="defaultValue"/> if not an integer.</summary>
    public static int AsInt(this TiledProperty prop, int defaultValue = 0) =>
        prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v) ? v : defaultValue;

    /// <summary>Returns the property value as a <see cref="float"/>, or <paramref name="defaultValue"/> if not a number.</summary>
    public static float AsFloat(this TiledProperty prop, float defaultValue = 0f) =>
        prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetSingle(out var v) ? v : defaultValue;

    /// <summary>Returns the property value as a <see cref="bool"/>, or <paramref name="defaultValue"/> if not a boolean.</summary>
    public static bool AsBool(this TiledProperty prop, bool defaultValue = false) =>
        prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? prop.Value.GetBoolean()
            : defaultValue;
}

// ── Tileset ───────────────────────────────────────────────────────────────────

/// <summary>
/// A Tiled tileset, either embedded inline in a map or referenced from an external <c>.tsx</c> file.
/// When <see cref="Source"/> is set the tileset data may be absent (the repository is responsible for
/// resolving and merging external tilesets before returning a <see cref="TiledMap"/>).
/// Tile IDs within this tileset are <em>local</em> (0-based); add <see cref="FirstGid"/> to convert to GIDs.
/// </summary>
public class TiledTileset
{
    /// <summary>
    /// The first Global Tile ID assigned to this tileset.
    /// Tile local ID 0 maps to GID <c>FirstGid</c>; local ID N maps to GID <c>FirstGid + N</c>.
    /// </summary>
    [JsonPropertyName("firstgid")]
    public int FirstGid { get; set; } = 1;

    /// <summary>
    /// Relative path to an external <c>.tsx</c> tileset file, or <see langword="null"/> for inline tilesets.
    /// When set, the tileset data is loaded separately and merged by the repository.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Tileset name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tiled application version used to save this tileset.</summary>
    [JsonPropertyName("tiledversion")]
    public string TiledVersion { get; set; } = string.Empty;

    /// <summary>Tileset format version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Always <c>"tileset"</c> for standalone tileset files.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Number of tile columns in the tileset image.</summary>
    [JsonPropertyName("columns")]
    public int Columns { get; set; }

    /// <summary>Total number of tiles in this tileset.</summary>
    [JsonPropertyName("tilecount")]
    public int TileCount { get; set; }

    /// <summary>Maximum tile width in pixels.</summary>
    [JsonPropertyName("tilewidth")]
    public int TileWidth { get; set; }

    /// <summary>Maximum tile height in pixels.</summary>
    [JsonPropertyName("tileheight")]
    public int TileHeight { get; set; }

    /// <summary>Buffer in pixels between the image edge and the first tile.</summary>
    [JsonPropertyName("margin")]
    public int Margin { get; set; }

    /// <summary>Spacing in pixels between adjacent tiles in the image.</summary>
    [JsonPropertyName("spacing")]
    public int Spacing { get; set; }

    /// <summary>Relative path to the tileset spritesheet image.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>Width of the tileset image in pixels.</summary>
    [JsonPropertyName("imagewidth")]
    public int ImageWidth { get; set; }

    /// <summary>Height of the tileset image in pixels.</summary>
    [JsonPropertyName("imageheight")]
    public int ImageHeight { get; set; }

    /// <summary>Hex-formatted background colour for the tileset. Optional.</summary>
    [JsonPropertyName("backgroundcolor")]
    public string? BackgroundColor { get; set; }

    /// <summary>Hex-formatted transparent colour treated as fully transparent when rendering. Optional.</summary>
    [JsonPropertyName("transparentcolor")]
    public string? TransparentColor { get; set; }

    /// <summary>User-assigned class for this tileset (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>
    /// Object alignment for tile objects placed with this tileset.
    /// Values: <c>unspecified</c>, <c>topleft</c>, <c>top</c>, <c>topright</c>, <c>left</c>,
    /// <c>center</c>, <c>right</c>, <c>bottomleft</c>, <c>bottom</c>, <c>bottomright</c>.
    /// </summary>
    [JsonPropertyName("objectalignment")]
    public string? ObjectAlignment { get; set; }

    /// <summary>
    /// Tile render size: <c>tile</c> (default) or <c>grid</c> (since Tiled 1.9).
    /// </summary>
    [JsonPropertyName("tilerendersize")]
    public string? TileRenderSize { get; set; }

    /// <summary>Fill mode: <c>stretch</c> (default) or <c>preserve-aspect-fit</c> (since Tiled 1.9).</summary>
    [JsonPropertyName("fillmode")]
    public string? FillMode { get; set; }

    /// <summary>Pixel offset applied to all tiles in this tileset when rendering.</summary>
    [JsonPropertyName("tileoffset")]
    public TiledTileOffset? TileOffset { get; set; }

    /// <summary>Grid settings for tiles in this tileset.</summary>
    [JsonPropertyName("grid")]
    public TiledGrid? Grid { get; set; }

    /// <summary>Allowed transformation flags for tiles in this tileset.</summary>
    [JsonPropertyName("transformations")]
    public TiledTransformations? Transformations { get; set; }

    /// <summary>Per-tile definitions carrying collision shapes, animations, custom properties, or sub-images.</summary>
    [JsonPropertyName("tiles")]
    public List<TiledTileDefinition> Tiles { get; set; } = [];

    /// <summary>Wang sets defined on this tileset (terrain brush data).</summary>
    [JsonPropertyName("wangsets")]
    public List<TiledWangSet> WangSets { get; set; } = [];

    /// <summary>Terrain definitions (legacy, superseded by Wang sets since Tiled 1.5).</summary>
    [JsonPropertyName("terrains")]
    public List<TiledTerrain> Terrains { get; set; } = [];

    /// <summary>Custom properties on this tileset.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}

// ── TileOffset / Grid / Transformations ──────────────────────────────────────

/// <summary>A pixel offset applied to all tiles of a tileset during rendering.</summary>
public class TiledTileOffset
{
    /// <summary>Horizontal offset in pixels.</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>Vertical offset in pixels (positive = down).</summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }
}

/// <summary>Grid settings specifying common tile dimensions and orientation for a tileset.</summary>
public class TiledGrid
{
    /// <summary>Grid cell width in pixels.</summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>Grid cell height in pixels.</summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>Grid orientation: <c>orthogonal</c> (default) or <c>isometric</c>.</summary>
    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "orthogonal";
}

/// <summary>Flags that allow tiles in a tileset to be flipped or rotated in the editor.</summary>
public class TiledTransformations
{
    /// <summary>Whether tiles can be flipped horizontally.</summary>
    [JsonPropertyName("hflip")]
    public bool HFlip { get; set; }

    /// <summary>Whether tiles can be flipped vertically.</summary>
    [JsonPropertyName("vflip")]
    public bool VFlip { get; set; }

    /// <summary>Whether tiles can be rotated in 90-degree increments.</summary>
    [JsonPropertyName("rotate")]
    public bool Rotate { get; set; }

    /// <summary>Whether un-transformed tiles are preferred over transformed alternatives.</summary>
    [JsonPropertyName("preferuntransformed")]
    public bool PreferUntransformed { get; set; }
}

// ── TileDefinition ────────────────────────────────────────────────────────────

/// <summary>
/// Per-tile definition within a <see cref="TiledTileset"/>.
/// May carry collision shapes (via <see cref="ObjectGroup"/>), animation frames,
/// custom properties, or a sub-image path for image-collection tilesets.
/// </summary>
public class TiledTileDefinition
{
    /// <summary>Local tile ID within the tileset (0-based).</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Tile class (was <c>type</c> in Tiled ≤1.8, renamed, then reverted; stored here regardless of name).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>User-assigned class (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>
    /// Probability used by the terrain/Wang brush when randomly placing this tile (default 1.0).
    /// </summary>
    [JsonPropertyName("probability")]
    public double Probability { get; set; } = 1.0;

    /// <summary>
    /// Object group layer carrying collision shapes for this tile.
    /// Each <see cref="TiledObject"/> in this layer defines a collision region
    /// relative to the tile's origin.
    /// </summary>
    [JsonPropertyName("objectgroup")]
    public TiledLayer? ObjectGroup { get; set; }

    /// <summary>Animation frame list. Non-null for animated tiles.</summary>
    [JsonPropertyName("animation")]
    public List<TiledFrame>? Animation { get; set; }

    /// <summary>Image path for image-collection tilesets (each tile is a separate image). Optional.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>Width of this tile's image in pixels. Used with image-collection tilesets.</summary>
    [JsonPropertyName("imagewidth")]
    public int? ImageWidth { get; set; }

    /// <summary>Height of this tile's image in pixels. Used with image-collection tilesets.</summary>
    [JsonPropertyName("imageheight")]
    public int? ImageHeight { get; set; }

    /// <summary>X position of the sub-rectangle within the tile image (default 0).</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>Y position of the sub-rectangle within the tile image (default 0).</summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>Width of the sub-rectangle (defaults to full image width).</summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>Height of the sub-rectangle (defaults to full image height).</summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>Custom properties on this tile.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];

    /// <summary>
    /// Legacy terrain corner indices: [top-left, top-right, bottom-left, bottom-right].
    /// Superseded by Wang sets since Tiled 1.5.
    /// </summary>
    [JsonPropertyName("terrain")]
    public int[]? Terrain { get; set; }
}

// ── Frame ─────────────────────────────────────────────────────────────────────

/// <summary>A single frame in a tile animation sequence.</summary>
public class TiledFrame
{
    /// <summary>Local tile ID representing this animation frame.</summary>
    [JsonPropertyName("tileid")]
    public int TileId { get; set; }

    /// <summary>Duration this frame is displayed, in milliseconds.</summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}

// ── Wang sets ─────────────────────────────────────────────────────────────────

/// <summary>A Wang set (terrain brush definition) within a tileset.</summary>
public class TiledWangSet
{
    /// <summary>Wang set name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Wang set type: <c>corner</c>, <c>edge</c>, or <c>mixed</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>User-assigned class (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>Local tile ID of the tile representing this Wang set.</summary>
    [JsonPropertyName("tile")]
    public int Tile { get; set; }

    /// <summary>Wang colours (terrain types) defined in this set.</summary>
    [JsonPropertyName("colors")]
    public List<TiledWangColor> Colors { get; set; } = [];

    /// <summary>Wang tile mappings — which Wang colour indices appear on each tile's edges/corners.</summary>
    [JsonPropertyName("wangtiles")]
    public List<TiledWangTile> WangTiles { get; set; } = [];

    /// <summary>Custom properties on this Wang set.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}

/// <summary>A Wang colour (terrain type) within a <see cref="TiledWangSet"/>.</summary>
public class TiledWangColor
{
    /// <summary>Colour name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex-formatted colour (<c>#RRGGBB</c> or <c>#AARRGGBB</c>).</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";

    /// <summary>Local tile ID of the representative tile for this colour.</summary>
    [JsonPropertyName("tile")]
    public int Tile { get; set; }

    /// <summary>Probability used when randomising terrain placement (default 1).</summary>
    [JsonPropertyName("probability")]
    public double Probability { get; set; } = 1.0;

    /// <summary>User-assigned class (since Tiled 1.9).</summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>Custom properties on this Wang colour.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}

/// <summary>Maps a tile to its Wang colour indices on all eight edges/corners.</summary>
public class TiledWangTile
{
    /// <summary>Local tile ID.</summary>
    [JsonPropertyName("tileid")]
    public int TileId { get; set; }

    /// <summary>
    /// Array of 8 Wang colour indices:
    /// [top, top-right, right, bottom-right, bottom, bottom-left, left, top-left].
    /// </summary>
    [JsonPropertyName("wangid")]
    public int[] WangId { get; set; } = [];
}

// ── Terrain (legacy) ─────────────────────────────────────────────────────────

/// <summary>
/// A legacy terrain definition (superseded by Wang sets since Tiled 1.5).
/// Kept for file compatibility with maps saved by older Tiled versions.
/// </summary>
public class TiledTerrain
{
    /// <summary>Terrain name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Local tile ID of the representative tile for this terrain.</summary>
    [JsonPropertyName("tile")]
    public int Tile { get; set; }

    /// <summary>Custom properties on this terrain.</summary>
    [JsonPropertyName("properties")]
    public List<TiledProperty> Properties { get; set; } = [];
}
