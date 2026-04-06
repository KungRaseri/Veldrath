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
    private bool _isChecking;
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

    /// <summary>True while a live server health check is in progress; all server-gated buttons are disabled during this time.</summary>
    public bool IsChecking
    {
        get => _isChecking;
        private set => this.RaiseAndSetIfChanged(ref _isChecking, value);
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
        : "Unable to connect to the server.";

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
        ClientSettings? settings = null,
        ISessionAlertService? sessionAlert = null)
    {
        var doExit = exit ?? (() => System.Environment.Exit(0));

        // Consume any pending alert (e.g. a version mismatch message) set before navigating here.
        if (sessionAlert?.PendingAlert is { } alert)
        {
            ErrorMessage = alert;
            sessionAlert.PendingAlert = null;
        }

        // Mirror token state reactively so XAML bindings update automatically
        IsLoggedIn = tokenStore.IsAuthenticated;
        tokenStore.WhenAnyValue(x => x.AccessToken)
            .Subscribe(token => IsLoggedIn = token is not null);

        // Mirror server status; default to online so commands aren't unexpectedly disabled
        // when constructed without the service (e.g. in tests).
        IsServerOnline = serverStatus?.IsOnline ?? true;

        // The polling loop calls CheckAsync on a background thread, so the WhenAnyValue
        // subscription fires there too. ObserveOn ensures all property mutations
        // (IsServerOnline, NewsPlaceholderText, announcement reload) happen on the UI
        // thread — matching what Avalonia bindings and ReactiveCommand CanExecute expect.
        serverStatus?.WhenAnyValue(s => s.Status)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(status =>
            {
                var wasOffline = !IsServerOnline;
                IsServerOnline = serverStatus.IsOnline;
                this.RaisePropertyChanged(nameof(NewsPlaceholderText));

                // Re-fetch announcements whenever the server comes back online so the
                // news panel reflects current content rather than the stale offline state.
                if (wasOffline && serverStatus.IsOnline && announcementService is not null)
                    _ = LoadAnnouncementsAsync(announcementService);
            });

        // Drive command canExecute from the VM's own IsServerOnline property, which is
        // always updated on the main thread via the ObserveOn subscription above.
        // Driving directly from the service would emit on the background polling thread,
        // bypassing the UI-thread guarantee that ReactiveCommand requires for CanExecute.
        var serverOnline = this.WhenAnyValue(x => x.IsServerOnline, x => x.IsChecking,
                               (online, checking) => online && !checking);
        var canEnterGame = this.WhenAnyValue(x => x.IsLoggedIn, x => x.IsServerOnline, x => x.IsChecking,
                               (loggedIn, online, checking) => loggedIn && online && !checking);

        // Performs a live health check immediately before any server-dependent navigation.
        // This catches the case where the server went offline after the last polling tick
        // (up to 30 s could have passed). If the check comes back offline, IsServerOnline
        // updates reactively and we skip navigation rather than sending the user to a broken page.
        async Task CheckThenNavigate<TViewModel>() where TViewModel : ViewModelBase
        {
            if (serverStatus is not null)
            {
                IsChecking = true;
                try
                {
                    await serverStatus.CheckAsync(settings?.ServerBaseUrl ?? "http://localhost:8080/");
                }
                finally
                {
                    IsChecking = false;
                }
            }
            if (IsServerOnline)
                navigation.NavigateTo<TViewModel>();
        }

        RegisterCommand        = ReactiveCommand.CreateFromTask(CheckThenNavigate<RegisterViewModel>, serverOnline);
        LoginCommand           = ReactiveCommand.CreateFromTask(CheckThenNavigate<LoginViewModel>, serverOnline);
        SelectCharacterCommand = ReactiveCommand.CreateFromTask(CheckThenNavigate<CharacterSelectViewModel>, canEnterGame);
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

        // Load announcements asynchronously only when the server is reachable.
        // When offline, announcements will be fetched automatically once the server comes back
        // via the WhenAnyValue subscription above.
        if (announcementService is not null && (serverStatus?.IsOnline ?? true))
            _ = LoadAnnouncementsAsync(announcementService);
    }

    private async Task LoadAnnouncementsAsync(IAnnouncementService announcementService)
    {
        Announcements = await announcementService.GetAnnouncementsAsync();
    }
}
