using Avalonia.Controls;
using ReactiveUI;
using Veldrath.Client.Services;

namespace Veldrath.Client.ViewModels;

/// <summary>Root view model for <see cref="Views.MainWindow"/>. Drives page navigation and window state.</summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private ViewModelBase _currentPage;
    private WindowState _windowState = WindowState.Normal;
    private bool _isServerOnline = true;
    private string _serverStatusMessage = string.Empty;

    /// <summary>Gets the view model of the currently displayed page.</summary>
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    /// <summary>Gets the current window state, driven by <see cref="ClientSettings.FullScreen"/>.</summary>
    public WindowState WindowState
    {
        get => _windowState;
        private set => this.RaiseAndSetIfChanged(ref _windowState, value);
    }

    /// <summary>Gets a value indicating whether the game server is currently reachable.</summary>
    public bool IsServerOnline
    {
        get => _isServerOnline;
        private set => this.RaiseAndSetIfChanged(ref _isServerOnline, value);
    }

    /// <summary>Gets the server connectivity status message shown in the top banner when offline.</summary>
    public string ServerStatusMessage
    {
        get => _serverStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _serverStatusMessage, value);
    }

    /// <summary>Initializes a new instance of <see cref="MainWindowViewModel"/>.</summary>
    /// <param name="navigation">Navigation service.</param>
    /// <param name="splash">Initial splash page.</param>
    /// <param name="settings">Shared client settings; observed for full-screen changes.</param>
    /// <param name="serverStatus">Server status service; drives the connection banner.</param>
    public MainWindowViewModel(INavigationService navigation, SplashViewModel splash, ClientSettings settings, IServerStatusService serverStatus)
    {
        _navigation = navigation;
        _currentPage = splash;

        _navigation.CurrentPageChanged += page => CurrentPage = page;

        settings.WhenAnyValue(s => s.FullScreen)
            .Subscribe(fs => WindowState = fs ? WindowState.FullScreen : WindowState.Normal);

        // Mirror server status so the banner reacts to status changes.
        IsServerOnline    = serverStatus.IsOnline;
        ServerStatusMessage = serverStatus.StatusMessage;
        serverStatus.WhenAnyValue(s => s.Status)
            .Subscribe(_ =>
            {
                IsServerOnline      = serverStatus.IsOnline;
                ServerStatusMessage = serverStatus.StatusMessage;
            });
    }
}
