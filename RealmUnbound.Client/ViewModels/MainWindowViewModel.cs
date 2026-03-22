using Avalonia.Controls;
using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Root view model for <see cref="Views.MainWindow"/>. Drives page navigation and window state.</summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private ViewModelBase _currentPage;
    private WindowState _windowState = WindowState.Normal;

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

    /// <summary>Initializes a new instance of <see cref="MainWindowViewModel"/>.</summary>
    /// <param name="navigation">Navigation service.</param>
    /// <param name="splash">Initial splash page.</param>
    /// <param name="settings">Shared client settings; observed for full-screen changes.</param>
    public MainWindowViewModel(INavigationService navigation, SplashViewModel splash, ClientSettings settings)
    {
        _navigation = navigation;
        _currentPage = splash;

        _navigation.CurrentPageChanged += page => CurrentPage = page;

        settings.WhenAnyValue(s => s.FullScreen)
            .Subscribe(fs => WindowState = fs ? WindowState.FullScreen : WindowState.Normal);
    }
}
