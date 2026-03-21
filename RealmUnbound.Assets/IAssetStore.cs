namespace RealmUnbound.Assets;

/// <summary>Provides access to game assets stored in the <c>GameAssets</c> directory.</summary>
public interface IAssetStore
{
    /// <summary>
    /// Asynchronously loads an image asset as a byte array.
    /// The result is cached in memory so subsequent calls for the same path are instant.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to the <c>GameAssets</c> root, using forward slashes,
    /// e.g. <c>"enemies/goblin_01.png"</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw image bytes, or <see langword="null"/> if the asset does not exist.</returns>
    Task<byte[]?> LoadImageAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fully-qualified file-system path for an audio asset so a media player
    /// can stream it directly, or <see langword="null"/> if the asset does not exist.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to the <c>GameAssets</c> root, e.g. <c>"audio/rpg/bookOpen.ogg"</c>.
    /// </param>
    /// <returns>Absolute path suitable for passing to an audio player, or <see langword="null"/>.</returns>
    string? ResolveAudioPath(string relativePath);

    /// <summary>
    /// Returns all asset paths within a category, relative to the <c>GameAssets</c> root,
    /// using forward slashes.
    /// </summary>
    /// <param name="category">The category to enumerate.</param>
    /// <returns>Sequence of relative paths; empty if the category directory does not exist.</returns>
    IEnumerable<string> GetPaths(AssetCategory category);

    /// <summary>Returns <see langword="true"/> if the asset file exists on disk.</summary>
    /// <param name="relativePath">Path relative to the <c>GameAssets</c> root.</param>
    bool Exists(string relativePath);
}
