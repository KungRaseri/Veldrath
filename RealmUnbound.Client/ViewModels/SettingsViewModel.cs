using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// Placeholder settings screen view model. Exposes client configuration that will be
/// expanded in future iterations (audio, graphics, keybindings, server URL, etc.).
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>Initializes a new instance of <see cref="SettingsViewModel"/>.</summary>
    /// <param name="navigation">Navigation service used to return to the previous screen.</param>
    public SettingsViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        BackCommand = ReactiveCommand.Create(() => _navigation.NavigateTo<MainMenuViewModel>());
    }

    /// <summary>Navigates back to the main menu.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BackCommand { get; }
}
