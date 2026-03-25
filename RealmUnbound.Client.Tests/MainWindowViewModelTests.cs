using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class MainWindowViewModelTests : TestBase
{
    private static MainWindowViewModel MakeVm(FakeNavigationService? nav = null, FakeServerStatusService? status = null)
    {
        nav ??= new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore(), new TokenStore(), new FakeAuthService());
        return new MainWindowViewModel(nav, splash, new ClientSettings("http://localhost:8080"), status ?? new FakeServerStatusService());
    }

    [Fact]
    public void CurrentPage_Should_Start_With_Splash_ViewModel()
    {
        var nav    = new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore(), new TokenStore(), new FakeAuthService());
        var vm     = new MainWindowViewModel(nav, splash, new ClientSettings("http://localhost:8080"), new FakeServerStatusService());

        vm.CurrentPage.Should().BeSameAs(splash);
    }

    [Fact]
    public void CurrentPage_Should_Update_When_Navigation_Fires_Generic()
    {
        var nav    = new FakeNavigationService();
        var splash = new SplashViewModel(nav, new FakeAssetStore(), new TokenStore(), new FakeAuthService());
        var vm     = new MainWindowViewModel(nav, splash, new ClientSettings("http://localhost:8080"), new FakeServerStatusService());

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
        var splash = new SplashViewModel(nav, new FakeAssetStore(), new TokenStore(), new FakeAuthService());
        var vm     = new MainWindowViewModel(nav, splash, new ClientSettings("http://localhost:8080"), new FakeServerStatusService());

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
        var splash   = new SplashViewModel(nav, new FakeAssetStore(), new TokenStore(), new FakeAuthService());
        var vm       = new MainWindowViewModel(nav, splash, new ClientSettings("http://localhost:8080"), new FakeServerStatusService());
        var changes  = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        nav.NavigateTo(new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService()));

        changes.Should().Contain(nameof(MainWindowViewModel.CurrentPage));
    }

    [Fact]
    public void IsServerOnline_Should_Default_To_True_When_Status_Is_Online()
    {
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status);

        vm.IsServerOnline.Should().BeTrue();
    }

    [Fact]
    public void IsServerOnline_Should_Be_False_When_Status_Is_Offline()
    {
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status);

        vm.IsServerOnline.Should().BeFalse();
    }

    [Fact]
    public void IsServerOnline_Should_React_To_Status_Changes()
    {
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status);

        status.IsOnline = false;

        vm.IsServerOnline.Should().BeFalse();
    }

    [Fact]
    public void ServerStatusMessage_Should_Be_Empty_When_Online()
    {
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status);

        vm.ServerStatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void ServerStatusMessage_Should_Be_Set_When_Offline()
    {
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status);

        vm.ServerStatusMessage.Should().NotBeEmpty();
    }
}
