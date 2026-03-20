using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class MainMenuViewModel : ViewModelBase
{
    private bool _isLoggedIn;

    public string Title => "RealmUnbound";
    public string Subtitle => "An Epic Adventure Awaits";

    /// <summary>True when a valid access token is present — drives which buttons are shown.</summary>
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        private set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
    }

    // Guest buttons (shown when not logged in)
    public ICommand RegisterCommand { get; }
    public ICommand LoginCommand { get; }

    // Authenticated buttons (shown when logged in)
    public ICommand SelectCharacterCommand { get; }
    public ICommand LogoutCommand { get; }

    // Always visible
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public MainMenuViewModel(INavigationService navigation, TokenStore tokenStore, IAuthService auth, Action? exit = null)
    {
        var doExit = exit ?? (() => System.Environment.Exit(0));

        // Mirror token state reactively so XAML bindings update automatically
        IsLoggedIn = tokenStore.IsAuthenticated;
        tokenStore.WhenAnyValue(x => x.AccessToken)
            .Subscribe(token => IsLoggedIn = token is not null);

        RegisterCommand       = ReactiveCommand.Create(() => navigation.NavigateTo<RegisterViewModel>());
        LoginCommand          = ReactiveCommand.Create(() => navigation.NavigateTo<LoginViewModel>());
        SelectCharacterCommand = ReactiveCommand.Create(() => navigation.NavigateTo<CharacterSelectViewModel>());
        LogoutCommand         = ReactiveCommand.CreateFromTask(async () =>
        {
            await auth.LogoutAsync();
            // IsLoggedIn updates automatically via the WhenAnyValue subscription above
        });
        SettingsCommand = ReactiveCommand.Create(() => navigation.NavigateTo<SettingsViewModel>());
        ExitCommand     = ReactiveCommand.Create(doExit);
    }
}
