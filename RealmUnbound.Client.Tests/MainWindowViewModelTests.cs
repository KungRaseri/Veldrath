using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class MainWindowViewModelTests : TestBase
{
    [Fact]
    public void CurrentPage_Should_Start_With_Splash_ViewModel()
    {
        var nav    = new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore());
        var vm     = new MainWindowViewModel(nav, splash);

        vm.CurrentPage.Should().BeSameAs(splash);
    }

    [Fact]
    public void CurrentPage_Should_Update_When_Navigation_Fires_Generic()
    {
        var nav    = new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore());
        var vm     = new MainWindowViewModel(nav, splash);

        // Navigate using the typed overload — FakeNavigationService raises CurrentPageChanged
        // only when given a concrete ViewModelBase instance.
        var mainMenu = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());
        nav.NavigateTo(mainMenu);

        vm.CurrentPage.Should().BeSameAs(mainMenu);
    }

    [Fact]
    public void CurrentPage_Should_Update_For_Multiple_Navigations()
    {
        var nav    = new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore());
        var vm     = new MainWindowViewModel(nav, splash);

        var first  = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());
        var second = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());

        nav.NavigateTo(first);
        nav.NavigateTo(second);

        vm.CurrentPage.Should().BeSameAs(second);
    }

    [Fact]
    public void CurrentPage_Should_Raise_PropertyChanged()
    {
        var nav      = new FakeNavigationService();
        var splash   = new SplashViewModel(nav, new FakeAssetStore());
        var vm       = new MainWindowViewModel(nav, splash);
        var changes  = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        nav.NavigateTo(new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService()));

        changes.Should().Contain(nameof(MainWindowViewModel.CurrentPage));
    }
}
