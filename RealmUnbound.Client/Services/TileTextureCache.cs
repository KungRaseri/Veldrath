using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media.Imaging;
using RealmUnbound.Assets.Manifest;

namespace RealmUnbound.Client.Services;

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
            return null;

        var fullPath = Path.Combine(AppContext.BaseDirectory, info.RelativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            var bitmap = new Bitmap(fullPath);
            _sheets[tilesetKey] = bitmap;
            return bitmap;
        }
        catch
        {
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
