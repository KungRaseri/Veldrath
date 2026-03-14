using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class SplashViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private double _progress;
    private string _statusText = "Initializing...";

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string Title => "RealmUnbound";
    public string Subtitle => "An Epic Adventure Awaits";

    // The task returned by RunSplashAsync; exposed so tests can await actual completion
    // instead of relying on wall-clock timing.
    public Task SplashTask { get; }

    public SplashViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        SplashTask = RunSplashAsync();
    }

    private async Task RunSplashAsync()
    {
        var steps = new (string Text, int Target)[]
        {
            ("Loading world data...",   30),
            ("Preparing your realm...", 65),
            ("Connecting services...",  90),
            ("Ready.",                 100),
        };

        foreach (var (text, target) in steps)
        {
            StatusText = text;
            while (_progress < target)
            {
                Progress = Math.Min(_progress + 2, target);
                await Task.Delay(18);
            }
            await Task.Delay(120);
        }

        await Task.Delay(300);
        _navigation.NavigateTo<MainMenuViewModel>();
    }
}
