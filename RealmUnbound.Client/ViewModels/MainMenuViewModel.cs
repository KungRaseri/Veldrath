using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using RealmUnbound.Assets;
using RealmUnbound.Assets.Manifest;
using RealmUnbound.Client.Services;
using RealmUnbound.Contracts.Announcements;

namespace RealmUnbound.Client.ViewModels;

public class MainMenuViewModel : ViewModelBase
{
    private bool _isLoggedIn;
    private bool _isServerOnline;
    private IReadOnlyList<AnnouncementDto> _announcements = [];

    public string Title    => "RealmUnbound";
    public string Subtitle => "An Epic Adventure Awaits";

    /// <summary>True when a valid access token is present — drives which buttons are shown.</summary>
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        private set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
    }

    /// <summary>True when the game server responded to its last health check.</summary>
    public bool IsServerOnline
    {
        get => _isServerOnline;
        private set => this.RaiseAndSetIfChanged(ref _isServerOnline, value);
    }

    /// <summary>Active announcements fetched from the server; empty list when offline.</summary>
    public IReadOnlyList<AnnouncementDto> Announcements
    {
        get => _announcements;
        private set
        {
            this.RaiseAndSetIfChanged(ref _announcements, value);
            this.RaisePropertyChanged(nameof(HasAnnouncements));
            this.RaisePropertyChanged(nameof(NewsPlaceholderText));
        }
    }

    /// <summary>True when there is at least one announcement to display.</summary>
    public bool HasAnnouncements => _announcements.Count > 0;

    /// <summary>Placeholder text shown in the news panel when there are no announcements.</summary>
    public string NewsPlaceholderText => IsServerOnline
        ? "No announcements at this time."
        : "Unable to connect to the server.\nCheck that the server is running and try again.";

    // Guest buttons (shown when not logged in)
    public ICommand RegisterCommand { get; }
    public ICommand LoginCommand    { get; }

    // Authenticated buttons (shown when logged in)
    public ICommand SelectCharacterCommand { get; }
    public ICommand LogoutCommand { get; }

    // Always visible
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand     { get; }

    public MainMenuViewModel(
        INavigationService navigation,
        TokenStore tokenStore,
        IAuthService auth,
        Action? exit = null,
        IAssetStore? assetStore = null,
        IAudioPlayer? audioPlayer = null,
        IServerStatusService? serverStatus = null,
        IAnnouncementService? announcementService = null,
        ClientSettings? settings = null)
    {
        var doExit = exit ?? (() => System.Environment.Exit(0));

        // Mirror token state reactively so XAML bindings update automatically
        IsLoggedIn = tokenStore.IsAuthenticated;
        tokenStore.WhenAnyValue(x => x.AccessToken)
            .Subscribe(token => IsLoggedIn = token is not null);

        // Mirror server status; default to online so commands aren't unexpectedly disabled
        // when constructed without the service (e.g. in tests).
        IsServerOnline = serverStatus?.IsOnline ?? true;
        serverStatus?.WhenAnyValue(s => s.Status)
            .Subscribe(_ =>
            {
                IsServerOnline = serverStatus.IsOnline;
                this.RaisePropertyChanged(nameof(NewsPlaceholderText));
            });

        // Commands that require the server to be reachable are gated on IsServerOnline.
        var serverOnline = this.WhenAnyValue(x => x.IsServerOnline);
        var canEnterGame = this.WhenAnyValue(x => x.IsLoggedIn, x => x.IsServerOnline,
                               (loggedIn, online) => loggedIn && online);

        RegisterCommand        = ReactiveCommand.Create(() => navigation.NavigateTo<RegisterViewModel>(), serverOnline);
        LoginCommand           = ReactiveCommand.Create(() => navigation.NavigateTo<LoginViewModel>(), serverOnline);
        SelectCharacterCommand = ReactiveCommand.Create(() => navigation.NavigateTo<CharacterSelectViewModel>(), canEnterGame);
        LogoutCommand          = ReactiveCommand.CreateFromTask(async () =>
        {
            await auth.LogoutAsync();
            // IsLoggedIn updates automatically via the WhenAnyValue subscription above
        });
        SettingsCommand = ReactiveCommand.Create(() => navigation.NavigateTo<SettingsViewModel>());
        ExitCommand     = ReactiveCommand.Create(doExit);

        if (assetStore is not null && audioPlayer is not null)
        {
            var townMusicPath = assetStore.ResolveAudioPath(AudioAssets.MusicTown);
            if (townMusicPath is not null)
                _ = audioPlayer.PlayMusicAsync(townMusicPath);
        }

        // Load announcements asynchronously; silently no-ops if service is not provided.
        if (announcementService is not null)
            _ = LoadAnnouncementsAsync(announcementService);
    }

    private async Task LoadAnnouncementsAsync(IAnnouncementService announcementService)
    {
        Announcements = await announcementService.GetAnnouncementsAsync();
    }
}
