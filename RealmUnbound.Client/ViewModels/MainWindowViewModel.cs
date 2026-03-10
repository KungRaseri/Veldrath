using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private ViewModelBase _currentPage;

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public MainWindowViewModel(INavigationService navigation, SplashViewModel splash)
    {
        _navigation = navigation;
        _currentPage = splash;

        _navigation.CurrentPageChanged += page => CurrentPage = page;
    }
}
