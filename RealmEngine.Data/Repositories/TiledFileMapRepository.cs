using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Loads <see cref="TiledMap"/> instances from Tiled TMX (XML) asset files bundled with the game.
/// Each map lives at <c>{mapsBasePath}/{zoneId}.tmx</c>.
/// External tileset references (<c>.tsx</c>) are resolved relative to the map file and merged
/// into the returned map so callers always receive a fully-hydrated model with collision data.
/// </summary>
/// <remarks>
/// Supported tile-data encodings: <c>csv</c> and <c>base64</c> (uncompressed, <c>zlib</c>, <c>gzip</c>).
/// Inline (no encoding attribute) tile data is also accepted.
/// </remarks>
public class TiledFileMapRepository : ITileMapRepository
{
    private readonly string _mapsBasePath;
    private readonly ILogger<TiledFileMapRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TiledFileMapRepository"/>
    /// resolving map files from <paramref name="mapsBasePath"/>.
    /// </summary>
    /// <param name="mapsBasePath">Directory containing <c>{zoneId}.tmx</c> map files.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public TiledFileMapRepository(string mapsBasePath, ILogger<TiledFileMapRepository> logger)
    {
        _mapsBasePath = mapsBasePath;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task<TiledMap?> GetByZoneIdAsync(string zoneId)
    {
        var filePath = Path.Combine(_mapsBasePath, $"{zoneId}.tmx");
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("No TMX asset found for zone '{ZoneId}' at {Path}", zoneId, filePath);
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
        var map = ParseMap(doc, filePath);

        // Resolve and merge each external tileset reference
        await MergeExternalTilesetsAsync(map, filePath);

        return map;
    }

    /// <inheritdoc />
    public async Task<TiledMap?> GetByRegionIdAsync(string regionId)
    {
        var filePath = Path.Combine(_mapsBasePath, $"{regionId}.tmx");
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("No TMX asset found for region '{RegionId}' at {Path}", regionId, filePath);
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
        var map = ParseMap(doc, filePath);

        // Resolve and merge each external tileset reference
        await MergeExternalTilesetsAsync(map, filePath);

        return map;
    }

    // ── Map parsing ───────────────────────────────────────────────────────────

    private static TiledMap ParseMap(XDocument doc, string mapFilePath)
    {
        var root = doc.Root ?? throw new InvalidOperationException($"TMX file '{mapFilePath}' has no root element.");

        var map = new TiledMap
        {
            Width          = (int?)root.Attribute("width")          ?? 0,
            Height         = (int?)root.Attribute("height")         ?? 0,
            TileWidth      = (int?)root.Attribute("tilewidth")      ?? 16,
            TileHeight     = (int?)root.Attribute("tileheight")     ?? 16,
            Orientation    = (string?)root.Attribute("orientation") ?? "orthogonal",
            RenderOrder    = (string?)root.Attribute("renderorder") ?? "right-down",
            Infinite       = (string?)root.Attribute("infinite")    == "1",
            TiledVersion   = (string?)root.Attribute("tiledversion") ?? string.Empty,
            Version        = (string?)root.Attribute("version")      ?? string.Empty,
            NextLayerId    = (int?)root.Attribute("nextlayerid")     ?? 0,
            NextObjectId   = (int?)root.Attribute("nextobjectid")    ?? 0,
            BackgroundColor = (string?)root.Attribute("backgroundcolor"),
            Properties     = ParseProperties(root.Element("properties")),
        };

        // Tilesets (may be external references only — source is resolved later)
        foreach (var tsEl in root.Elements("tileset"))
        {
            var firstGid = (int?)tsEl.Attribute("firstgid") ?? 1;
            var source   = (string?)tsEl.Attribute("source");

            if (source is not null)
            {
                // External reference — placeholder resolved in MergeExternalTilesetsAsync
                map.Tilesets.Add(new TiledTileset { FirstGid = firstGid, Source = source });
            }
            else
            {
                map.Tilesets.Add(ParseInlineTileset(tsEl, firstGid));
            }
        }

        // Layers
        foreach (var child in root.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "layer":
                    map.Layers.Add(ParseTileLayer(child));
                    break;
                case "objectgroup":
                    map.Layers.Add(ParseObjectGroup(child));
                    break;
                // imagelayer and group are not consumed by the engine; skip them silently
            }
        }

        return map;
    }

    private static TiledLayer ParseTileLayer(XElement el)
    {
        var layer = new TiledLayer
        {
            Id         = (int?)el.Attribute("id")      ?? 0,
            Type       = "tilelayer",
            Name       = (string?)el.Attribute("name") ?? string.Empty,
            Width      = (int?)el.Attribute("width")   ?? 0,
            Height     = (int?)el.Attribute("height")  ?? 0,
            Visible    = (string?)el.Attribute("visible") != "0",
            Opacity    = ParseDouble(el.Attribute("opacity"), 1.0),
            OffsetX    = ParseDouble(el.Attribute("offsetx"), 0.0),
            OffsetY    = ParseDouble(el.Attribute("offsety"), 0.0),
            Properties = ParseProperties(el.Element("properties")),
        };

        var dataEl = el.Element("data");
        if (dataEl is not null)
            layer.Data = ParseTileData(dataEl);

        return layer;
    }

    private static TiledLayer ParseObjectGroup(XElement el)
    {
        var layer = new TiledLayer
        {
            Id         = (int?)el.Attribute("id")          ?? 0,
            Type       = "objectgroup",
            Name       = (string?)el.Attribute("name")     ?? string.Empty,
            DrawOrder  = (string?)el.Attribute("draworder") ?? "topdown",
            Visible    = (string?)el.Attribute("visible")  != "0",
            Opacity    = ParseDouble(el.Attribute("opacity"), 1.0),
            OffsetX    = ParseDouble(el.Attribute("offsetx"), 0.0),
            OffsetY    = ParseDouble(el.Attribute("offsety"), 0.0),
            Properties = ParseProperties(el.Element("properties")),
        };

        foreach (var objEl in el.Elements("object"))
            layer.Objects.Add(ParseObject(objEl));

        return layer;
    }

    private static TiledObject ParseObject(XElement el)
    {
        var obj = new TiledObject
        {
            Id         = (int?)el.Attribute("id")         ?? 0,
            Name       = (string?)el.Attribute("name")    ?? string.Empty,
            Type       = (string?)el.Attribute("type")    ?? (string?)el.Attribute("class") ?? string.Empty,
            X          = ParseDouble(el.Attribute("x"),      0.0),
            Y          = ParseDouble(el.Attribute("y"),      0.0),
            Width      = ParseDouble(el.Attribute("width"),  0.0),
            Height     = ParseDouble(el.Attribute("height"), 0.0),
            Rotation   = ParseDouble(el.Attribute("rotation"), 0.0),
            Visible    = (string?)el.Attribute("visible") != "0",
            Properties = ParseProperties(el.Element("properties")),
        };

        if (el.Element("point") is not null)   obj.Point   = true;
        if (el.Element("ellipse") is not null) obj.Ellipse = true;

        return obj;
    }

    // ── Tileset parsing ───────────────────────────────────────────────────────

    private static TiledTileset ParseInlineTileset(XElement el, int firstGid)
    {
        var ts = new TiledTileset
        {
            FirstGid    = firstGid,
            Name        = (string?)el.Attribute("name")       ?? string.Empty,
            TileWidth   = (int?)el.Attribute("tilewidth")     ?? 16,
            TileHeight  = (int?)el.Attribute("tileheight")    ?? 16,
            Spacing     = (int?)el.Attribute("spacing")       ?? 0,
            Margin      = (int?)el.Attribute("margin")        ?? 0,
            TileCount   = (int?)el.Attribute("tilecount")     ?? 0,
            Columns     = (int?)el.Attribute("columns")       ?? 0,
            TiledVersion = (string?)el.Attribute("tiledversion") ?? string.Empty,
            Version     = (string?)el.Attribute("version")    ?? string.Empty,
            Properties  = ParseProperties(el.Element("properties")),
        };

        var imgEl = el.Element("image");
        if (imgEl is not null)
        {
            ts.Image       = (string?)imgEl.Attribute("source");
            ts.ImageWidth  = (int?)imgEl.Attribute("width")  ?? 0;
            ts.ImageHeight = (int?)imgEl.Attribute("height") ?? 0;
        }

        foreach (var tileEl in el.Elements("tile"))
            ts.Tiles.Add(ParseTileDefinition(tileEl));

        return ts;
    }

    private static TiledTileDefinition ParseTileDefinition(XElement el)
    {
        var tile = new TiledTileDefinition
        {
            Id          = (int?)el.Attribute("id")   ?? 0,
            Type        = (string?)el.Attribute("type")  ?? (string?)el.Attribute("class"),
            Probability = ParseDouble(el.Attribute("probability"), 1.0),
            Properties  = ParseProperties(el.Element("properties")),
        };

        var ogEl = el.Element("objectgroup");
        if (ogEl is not null)
        {
            tile.ObjectGroup = new TiledLayer
            {
                Type      = "objectgroup",
                Name      = (string?)ogEl.Attribute("name")      ?? string.Empty,
                DrawOrder = (string?)ogEl.Attribute("draworder") ?? "index",
            };
            foreach (var objEl in ogEl.Elements("object"))
                tile.ObjectGroup.Objects.Add(ParseObject(objEl));
        }

        return tile;
    }

    // ── Tile data decoding ────────────────────────────────────────────────────

    private static List<int> ParseTileData(XElement dataEl)
    {
        var encoding    = (string?)dataEl.Attribute("encoding");
        var compression = (string?)dataEl.Attribute("compression");
        var raw         = dataEl.Value.Trim();

        if (string.IsNullOrEmpty(encoding) || encoding == "csv")
            return ParseCsv(raw);

        if (encoding == "base64")
        {
            var bytes       = Convert.FromBase64String(raw);
            var decompressed = compression switch
            {
                "zlib" => DecompressZlib(bytes),
                "gzip" => DecompressGzip(bytes),
                _      => bytes,   // no compression
            };
            return ReadGids(decompressed);
        }

        return [];
    }

    private static List<int> ParseCsv(string raw)
    {
        var parts  = raw.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<int>(parts.Length);
        foreach (var part in parts)
            if (int.TryParse(part, out var gid))
                result.Add(gid);
        return result;
    }

    private static List<int> ReadGids(byte[] bytes)
    {
        var count  = bytes.Length / 4;
        var result = new List<int>(count);
        for (var i = 0; i < bytes.Length - 3; i += 4)
            result.Add((int)BitConverter.ToUInt32(bytes, i));
        return result;
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        // Zlib format: 2-byte header + deflate stream + 4-byte Adler-32 checksum at end
        using var deflate = new DeflateStream(new MemoryStream(data, 2, data.Length - 2), CompressionMode.Decompress);
        using var ms      = new MemoryStream();
        deflate.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] DecompressGzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var ms   = new MemoryStream();
        gzip.CopyTo(ms);
        return ms.ToArray();
    }

    // ── External tileset resolution ───────────────────────────────────────────

    private async Task MergeExternalTilesetsAsync(TiledMap map, string mapFilePath)
    {
        var mapDir = Path.GetDirectoryName(mapFilePath) ?? string.Empty;

        for (var i = 0; i < map.Tilesets.Count; i++)
        {
            var placeholder = map.Tilesets[i];
            if (placeholder.Source is null) continue;

            var tsxPath = Path.GetFullPath(Path.Combine(mapDir, placeholder.Source));
            if (!File.Exists(tsxPath))
            {
                _logger.LogWarning("External tileset '{Source}' not found at '{Path}'", placeholder.Source, tsxPath);
                continue;
            }

            await using var stream = File.OpenRead(tsxPath);
            var doc     = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var tileset = ParseInlineTileset(doc.Root!, placeholder.FirstGid);
            tileset.Source   = placeholder.Source;
            tileset.FirstGid = placeholder.FirstGid;
            map.Tilesets[i]  = tileset;
        }
    }

    // ── Property parsing ──────────────────────────────────────────────────────

    private static List<TiledProperty> ParseProperties(XElement? propertiesEl)
    {
        if (propertiesEl is null) return [];

        var result = new List<TiledProperty>();
        foreach (var prop in propertiesEl.Elements("property"))
        {
            var name  = (string?)prop.Attribute("name") ?? string.Empty;
            var type  = (string?)prop.Attribute("type") ?? "string";
            // Tiled 1.9+: string values may appear as element content (multiline) instead of attribute
            var raw   = (string?)prop.Attribute("value") ?? prop.Value;

            var value = type switch
            {
                "int"   => JsonSerializer.SerializeToElement(int.TryParse(raw, out var iv) ? iv : 0),
                "float" => JsonSerializer.SerializeToElement(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv) ? fv : 0f),
                "bool"  => JsonSerializer.SerializeToElement(raw.Equals("true", StringComparison.OrdinalIgnoreCase)),
                _       => JsonSerializer.SerializeToElement(raw),  // string, color, file, object-ref, class
            };

            result.Add(new TiledProperty { Name = name, Type = type, Value = value });
        }

        return result;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static double ParseDouble(XAttribute? attr, double fallback) =>
        attr is not null && double.TryParse((string)attr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
}
