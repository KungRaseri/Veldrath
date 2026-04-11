using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Veldrath.Assets;

/// <summary>
/// File-system backed implementation of <see cref="IAssetStore"/> that reads assets from the
/// <c>GameAssets</c> subdirectory and caches loaded image bytes in an <see cref="IMemoryCache"/>.
/// </summary>
public sealed class AssetStore : IAssetStore
{
    private static readonly Dictionary<AssetCategory, string> CategoryPaths = new()
    {
        [AssetCategory.Enemies]          = "enemies",
        [AssetCategory.Weapons]          = "items/weapons",
        [AssetCategory.Armor]            = "items/armor",
        [AssetCategory.Potions]          = "items/potions",
        [AssetCategory.Spells]           = "spells",
        [AssetCategory.Classes]          = "classes",
        [AssetCategory.Ui]               = "ui",
        [AssetCategory.AudioRpg]         = "audio/rpg",
        [AssetCategory.AudioMusic]       = "audio/music",
        [AssetCategory.AudioImpact]      = "audio/impact",
        [AssetCategory.AudioInterface]   = "audio/interface",
        [AssetCategory.CraftingMining]   = "crafting/mining",
        [AssetCategory.CraftingFishing]  = "crafting/fishing",
        [AssetCategory.CraftingHunting]  = "crafting/hunting",
        [AssetCategory.CraftingForest]       = "crafting/forest",
        [AssetCategory.Accessories]           = "items/accessories",
        [AssetCategory.Shields]               = "items/shields",
        [AssetCategory.Food]                  = "items/food",
        [AssetCategory.ItemMisc]              = "items/misc",
        [AssetCategory.CraftingIngredients]   = "items/crafting-ingredients",
    };

    private readonly string _assetsRoot;
    private readonly IMemoryCache _cache;

    /// <summary>Initializes a new instance of <see cref="AssetStore"/>.</summary>
    /// <param name="options">Options controlling the base path for the <c>GameAssets</c> folder.</param>
    /// <param name="cache">Memory cache used to store loaded image bytes.</param>
    public AssetStore(IOptions<AssetStoreOptions> options, IMemoryCache cache)
    {
        _cache = cache;
        _assetsRoot = Path.Combine(options.Value.BasePath, "GameAssets");
    }

    /// <inheritdoc/>
    public async Task<byte[]?> LoadImageAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"asset:{relativePath.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out byte[]? cached))
            return cached;

        var fullPath = BuildFullPath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);

        _cache.Set(cacheKey, bytes, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove,
        });

        return bytes;
    }

    /// <inheritdoc/>
    public string? ResolveAudioPath(string relativePath)
    {
        var fullPath = BuildFullPath(relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetPaths(AssetCategory category)
    {
        if (!CategoryPaths.TryGetValue(category, out var subdir))
            return [];

        var dir = Path.Combine(_assetsRoot, subdir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dir))
            return [];

        return Directory
            .EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_assetsRoot, f).Replace('\\', '/'));
    }

    /// <inheritdoc/>
    public bool Exists(string relativePath) => File.Exists(BuildFullPath(relativePath));

    private string BuildFullPath(string relativePath) =>
        Path.Combine(_assetsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
