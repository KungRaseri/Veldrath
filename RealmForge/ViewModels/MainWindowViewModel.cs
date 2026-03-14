using System.Reactive;
using ReactiveUI;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private object? _currentPage;
    private bool _isPaneOpen = true;
    private bool _isDarkMode = true;

    public MainWindowViewModel(EditorSettingsService settingsService)
    {
        TogglePaneCommand = ReactiveCommand.Create(() => { IsPaneOpen = !IsPaneOpen; });
        ToggleThemeCommand = ReactiveCommand.Create(() => { IsDarkMode = !IsDarkMode; });

        CurrentPage = new HomeViewModel();
        _ = LoadSettingsAsync(settingsService);
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => this.RaiseAndSetIfChanged(ref _isDarkMode, value);
    }

    public ReactiveCommand<Unit, Unit> TogglePaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }

    private async Task LoadSettingsAsync(EditorSettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.LoadSettingsAsync();
            IsDarkMode = settings.Theme == "Dark";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings for theme");
        }
    }
}
