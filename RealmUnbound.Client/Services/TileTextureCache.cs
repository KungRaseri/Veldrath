using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media.Imaging;
using Veldrath.Assets.Manifest;
using Serilog;

namespace Veldrath.Client.Services;

/// <summary>
/// Loads and caches tileset spritesheet <see cref="Bitmap"/> objects keyed by tileset key.
/// The renderer uses <c>DrawingContext.DrawImage(sheet, sourceRect, destRect)</c> to draw tiles
/// directly from the sheet without needing individual tile bitmaps.
/// </summary>
[ExcludeFromCodeCoverage]
public class TileTextureCache : IDisposable
{
    private readonly Dictionary<string, Bitmap> _sheets = [];
    private bool _disposed;

    /// <summary>
    /// Returns the full spritesheet <see cref="Bitmap"/> for the given tileset,
    /// loading it from disk on first access. Returns <see langword="null"/> if not found.
    /// </summary>
    public Bitmap? GetSheet(string tilesetKey)
    {
        if (_sheets.TryGetValue(tilesetKey, out var cached))
            return cached;

        if (!TilemapAssets.All.TryGetValue(tilesetKey, out var info))
        {
            Log.Warning("TileTextureCache: unknown tileset key {TilesetKey}", tilesetKey);
            return null;
        }

        var fullPath = Path.Combine(AppContext.BaseDirectory, info.RelativePath);
        if (!File.Exists(fullPath))
        {
            Log.Warning("TileTextureCache: spritesheet not found at {Path}", fullPath);
            return null;
        }

        try
        {
            // Use stream-based load — Avalonia's path-based Bitmap ctor can fail silently
            // on some platforms when the path contains non-ASCII characters or is resolved
            // differently by the runtime. Stream load is always reliable.
            using var fs = File.OpenRead(fullPath);
            var bitmap = new Bitmap(fs);
            _sheets[tilesetKey] = bitmap;
            Log.Debug("TileTextureCache: loaded {TilesetKey} from {Path} ({W}x{H})",
                tilesetKey, fullPath, bitmap.Size.Width, bitmap.Size.Height);
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TileTextureCache: failed to load spritesheet {TilesetKey} from {Path}", tilesetKey, fullPath);
            return null;
        }
    }

    /// <summary>
    /// Computes the source <see cref="Rect"/> within the spritesheet for the given tile index.
    /// Returns <see langword="null"/> when the tileset is unknown or the index is out of range.
    /// </summary>
    public static Rect? GetSourceRect(string tilesetKey, int tileIndex)
    {
        if (tileIndex < 0)
            return null;

        if (!TilemapAssets.All.TryGetValue(tilesetKey, out var info))
            return null;

        var col  = tileIndex % info.Columns;
        var row  = tileIndex / info.Columns;
        var srcX = col * (info.TileSize + info.Spacing);
        var srcY = row * (info.TileSize + info.Spacing);

        return new Rect(srcX, srcY, info.TileSize, info.TileSize);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var b in _sheets.Values) b.Dispose();
        _sheets.Clear();
    }
}
