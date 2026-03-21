namespace RealmUnbound.Assets;

/// <summary>Configuration options for <see cref="AssetStore"/>.</summary>
public sealed class AssetStoreOptions
{
    /// <summary>
    /// The directory that contains the <c>GameAssets</c> folder.
    /// Defaults to <see cref="AppContext.BaseDirectory"/>, which is correct for deployed
    /// applications where assets are copied alongside the executable.
    /// </summary>
    public string BasePath { get; set; } = AppContext.BaseDirectory;
}
