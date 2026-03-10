using System.Reactive.Linq;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class MainMenuViewModelTests : TestBase
{
    private static MainMenuViewModel MakeVm(FakeNavigationService? nav = null)
        => new MainMenuViewModel(nav ?? new FakeNavigationService());

    [Fact]
    public void Title_Should_Be_RealmUnbound()
    {
        var vm = MakeVm();
        vm.Title.Should().Be("RealmUnbound");
    }

    [Fact]
    public void Subtitle_Should_Not_Be_Empty()
    {
        var vm = MakeVm();
        vm.Subtitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterCommand_Should_Navigate_To_RegisterViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav);

        await ((ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.RegisterCommand).Execute();

        nav.NavigationLog.Should().Contain(typeof(RegisterViewModel));
    }

    [Fact]
    public async Task LoginCommand_Should_Navigate_To_LoginViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav);

        await ((ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.LoginCommand).Execute();

        nav.NavigationLog.Should().Contain(typeof(LoginViewModel));
    }
}

public class SplashViewModelTests : TestBase
{
    [Fact]
    public void Title_Should_Be_RealmUnbound()
    {
        var nav = new FakeNavigationService();
        var vm  = new SplashViewModel(nav);
        vm.Title.Should().Be("RealmUnbound");
    }

    [Fact]
    public void Subtitle_Should_Not_Be_Empty()
    {
        var nav = new FakeNavigationService();
        var vm  = new SplashViewModel(nav);
        vm.Subtitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SplashViewModel_Should_Eventually_Navigate_To_MainMenu()
    {
        var nav = new FakeNavigationService();
        _ = new SplashViewModel(nav);

        // SplashViewModel.RunSplashAsync takes ~1.9s total (50 steps × 18ms + 4 × 120ms + 300ms)
        // We wait a bit beyond that to cover CI variance
        await Task.Delay(2500);

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }
}
