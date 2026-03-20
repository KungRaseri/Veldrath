using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// Settings screen view model. Exposes client configuration that the player can change at runtime.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ClientSettings _settings;

    /// <summary>Initializes a new instance of <see cref="SettingsViewModel"/>.</summary>
    /// <param name="navigation">Navigation service used to return to the previous screen.</param>
    /// <param name="settings">Shared client settings whose values are exposed for editing.</param>
    public SettingsViewModel(INavigationService navigation, ClientSettings settings)
    {
        _navigation = navigation;
        _settings = settings;
        BackCommand = ReactiveCommand.Create(() => _navigation.NavigateTo<MainMenuViewModel>());
    }

    /// <summary>Gets or sets the base URL of the game server.</summary>
    public string ServerUrl
    {
        get => _settings.ServerBaseUrl;
        set => _settings.ServerBaseUrl = value;
    }

    /// <summary>Navigates back to the main menu.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BackCommand { get; }
}
