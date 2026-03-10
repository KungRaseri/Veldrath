using ReactiveUI;
using System.Windows.Input;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class MainMenuViewModel : ViewModelBase
{
    public string Title => "RealmUnbound";
    public string Subtitle => "An Epic Adventure Awaits";

    public ICommand RegisterCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public MainMenuViewModel(INavigationService navigation, Action? exit = null)
    {
        var doExit      = exit ?? (() => System.Environment.Exit(0));
        RegisterCommand = ReactiveCommand.Create(() => navigation.NavigateTo<RegisterViewModel>());
        LoginCommand    = ReactiveCommand.Create(() => navigation.NavigateTo<LoginViewModel>());
        SettingsCommand = ReactiveCommand.Create(() => { /* TODO: settings screen */ });
        ExitCommand     = ReactiveCommand.Create(doExit);
    }
}
