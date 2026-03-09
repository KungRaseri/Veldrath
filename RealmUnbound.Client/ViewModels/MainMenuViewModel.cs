using ReactiveUI;
using System.Windows.Input;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class MainMenuViewModel : ViewModelBase
{
    public string Title => "RealmUnbound";
    public string Subtitle => "An Epic Adventure Awaits";

    public ICommand NewGameCommand { get; }
    public ICommand LoadGameCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public MainMenuViewModel()
    {
        NewGameCommand = ReactiveCommand.Create(() => { /* Navigate to character creation */ });
        LoadGameCommand = ReactiveCommand.Create(() => { /* Navigate to load game */ });
        SettingsCommand = ReactiveCommand.Create(() => { /* Navigate to settings */ });
        ExitCommand = ReactiveCommand.Create(() => System.Environment.Exit(0));
    }
}
