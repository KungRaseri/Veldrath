using ReactiveUI;
using RealmUnbound.Assets;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Loading screen shown while the client initialises and warms the asset cache.</summary>
public class SplashViewModel : ViewModelBase
{
    /// <summary>Image asset categories to preload during the splash sequence (audio is excluded — streamed on demand).</summary>
    private static readonly AssetCategory[] ImageCategories =
    [
        AssetCategory.Enemies,
        AssetCategory.Weapons,
        AssetCategory.Armor,
        AssetCategory.Potions,
        AssetCategory.Spells,
        AssetCategory.Classes,
        AssetCategory.Ui,
        AssetCategory.CraftingMining,
        AssetCategory.CraftingFishing,
        AssetCategory.CraftingHunting,
        AssetCategory.CraftingForest,
    ];

    private readonly INavigationService _navigation;
    private readonly IAssetStore _assetStore;
    private readonly TokenStore _tokens;
    private readonly IAuthService _auth;
    private readonly IServerStatusService? _serverStatus;
    private readonly ClientSettings? _settings;
    private double _progress;
    private string _statusText = "Initializing...";

    /// <summary>Gets the loading progress as a value from 0 to 100.</summary>
    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    /// <summary>Gets the current status message shown below the progress bar.</summary>
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    /// <summary>Gets the game title displayed on the splash screen.</summary>
    public string Title => "RealmUnbound";

    /// <summary>Gets the subtitle tagline displayed on the splash screen.</summary>
    public string Subtitle => "An Epic Adventure Awaits";

    /// <summary>
    /// The task returned by the internal splash sequence.
    /// Exposed so tests can await actual completion without relying on wall-clock timing.
    /// </summary>
    public Task SplashTask { get; }

    /// <summary>Initializes a new instance of <see cref="SplashViewModel"/>.</summary>
    public SplashViewModel(INavigationService navigation, IAssetStore assetStore, TokenStore tokens, IAuthService auth, IServerStatusService? serverStatus = null, ClientSettings? settings = null)
    {
        _navigation    = navigation;
        _assetStore    = assetStore;
        _tokens        = tokens;
        _auth          = auth;
        _serverStatus  = serverStatus;
        _settings      = settings;
        SplashTask     = RunSplashAsync();
    }

    private async Task RunSplashAsync()
    {
        // Phase 1: brief init (0 → 10 %)
        StatusText = "Initializing...";
        await AnimateProgressTo(10, step: 2, delayMs: 18);
        await Task.Delay(80);

        // Phase 2: preload image assets, one category at a time (10 → 90 %)
        // Audio assets are streamed by path on demand — no preload needed.
        double progressPerCategory = 80.0 / ImageCategories.Length;
        double targetBase = 10;

        foreach (var category in ImageCategories)
        {
            StatusText = $"Loading assets... ({CategoryLabel(category)})";

            var paths = _assetStore.GetPaths(category).ToList();

            // Preload all images in this category and animate the progress bar in parallel.
            // If GameAssets/ is not populated (e.g. fresh clone before sync-assets.ps1 is run),
            // GetPaths returns empty and the loop completes instantly with cosmetic animation only.
            var target = Math.Min(targetBase + progressPerCategory, 90);
            await Task.WhenAll(
                paths.Count > 0
                    ? Task.WhenAll(paths.Select(p => _assetStore.LoadImageAsync(p)))
                    : Task.CompletedTask,
                AnimateProgressTo(target, step: 1, delayMs: 10));

            targetBase = target;
        }

        // Phase 3: ready (90 → 100 %)
        StatusText = "Ready.";
        await AnimateProgressTo(100, step: 2, delayMs: 18);
        await Task.Delay(300);

        // Check server reachability before attempting session restore.
        // This sets ServerStatusService.Status so the main menu banner is accurate from
        // the moment the user lands on it.
        if (_serverStatus is not null && _settings is not null)
        {
            StatusText = "Checking server...";
            await _serverStatus.CheckAsync(_settings.ServerBaseUrl);
        }

        // Attempt to exchange the stored refresh token for a fresh access token.
        // If the server is unreachable or the token is invalid, soft-logout so the
        // main menu shows the guest state rather than a half-authenticated limbo.
        if (_tokens.IsAuthenticated)
        {
            if (_serverStatus is not null && !_serverStatus.IsOnline)
            {
                // Server is down — clear local state immediately without attempting a
                // network call (which would just fail and log a warning anyway).
                StatusText = "Server unavailable. Continuing as guest...";
                await _auth.LogoutAsync();
            }
            else
            {
                StatusText = "Restoring session...";
                var refreshed = await _auth.RefreshAsync();
                if (!refreshed)
                {
                    // Token invalid or server rejected it — clear local state.
                    await _auth.LogoutAsync();
                }
            }
        }

        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private async Task AnimateProgressTo(double target, int step, int delayMs)
    {
        while (_progress < target)
        {
            Progress = Math.Min(_progress + step, target);
            await Task.Delay(delayMs);
        }
    }

    private static string CategoryLabel(AssetCategory category) => category switch
    {
        AssetCategory.Enemies        => "enemies",
        AssetCategory.Weapons        => "weapons",
        AssetCategory.Armor          => "armor",
        AssetCategory.Potions        => "potions",
        AssetCategory.Spells         => "spells",
        AssetCategory.Classes        => "classes",
        AssetCategory.Ui             => "UI",
        AssetCategory.CraftingMining => "mining",
        AssetCategory.CraftingFishing => "fishing",
        AssetCategory.CraftingHunting => "hunting",
        AssetCategory.CraftingForest  => "forest",
        _                            => category.ToString().ToLowerInvariant(),
    };
}
