using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media.Imaging;
using RealmUnbound.Assets.Manifest;
using Serilog;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Loads and caches entity sprite sheet <see cref="Bitmap"/> objects keyed by sprite key.
/// Uses the same stream-based load pattern as <see cref="TileTextureCache"/> to ensure
/// reliable loading on all platforms.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class EntityTextureCache : IDisposable
{
    // Both key → bitmap AND path → bitmap are stored so multiple keys sharing
    // the same sheet file (e.g. village-blacksmith + tavern-keeper → npc1.png)
    // only load the file once.
    private readonly Dictionary<string, Bitmap> _byKey  = [];
    private readonly Dictionary<string, Bitmap> _byPath = [];
    private bool _disposed;

    /// <summary>
    /// Returns the cached <see cref="Bitmap"/> for the given <paramref name="spriteKey"/>,
    /// loading it from disk on first access. Returns <see langword="null"/> when the key is
    /// unknown or the file cannot be found or loaded.
    /// </summary>
    public Bitmap? GetSheet(string spriteKey)
    {
        if (_byKey.TryGetValue(spriteKey, out var cached))
            return cached;

        if (!EntitySpriteAssets.All.TryGetValue(spriteKey, out var info))
            return null; // unknown key — renderer falls back to coloured box

        // Share the bitmap when another key already loaded the same sheet file.
        if (_byPath.TryGetValue(info.RelativePath, out var shared))
        {
            _byKey[spriteKey] = shared;
            return shared;
        }

        var fullPath = Path.Combine(AppContext.BaseDirectory, info.RelativePath);
        if (!File.Exists(fullPath))
        {
            Log.Warning("EntityTextureCache: sheet not found at {Path}", fullPath);
            return null;
        }

        try
        {
            using var fs = File.OpenRead(fullPath);
            var bitmap = new Bitmap(fs);
            _byPath[info.RelativePath] = bitmap;
            _byKey[spriteKey]          = bitmap;
            Log.Debug("EntityTextureCache: loaded {Key} from {Path} ({W}×{H})",
                spriteKey, fullPath, bitmap.Size.Width, bitmap.Size.Height);
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EntityTextureCache: failed to load sheet for {Key}", spriteKey);
            return null;
        }
    }

    /// <summary>
    /// Computes the source <see cref="Rect"/> for the idle frame of the given
    /// <paramref name="spriteKey"/> facing <paramref name="direction"/>.
    /// Returns <see langword="null"/> when the key is not registered.
    /// </summary>
    public static Rect? GetSourceRect(string spriteKey, string direction)
    {
        if (!EntitySpriteAssets.All.TryGetValue(spriteKey, out var info))
            return null;

        var (fx, fy) = info.GetFrameOffset(direction);
        return new Rect(fx, fy, info.FrameWidth, info.FrameHeight);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var bmp in _byPath.Values)
            bmp.Dispose();
        _byPath.Clear();
        _byKey.Clear();
    }
}
