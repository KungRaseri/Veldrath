using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class MainMenuViewModelTests : TestBase
{
    private static MainMenuViewModel MakeVm(FakeNavigationService? nav = null, Action? exit = null)
        => new MainMenuViewModel(nav ?? new FakeNavigationService(), new TokenStore(), new FakeAuthService(), exit);

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

    [Fact]
    public async Task SettingsCommand_Should_Execute_Without_Throwing()
    {
        var vm  = MakeVm();
        var cmd = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.SettingsCommand;
        // SettingsCommand is a no-op placeholder; just verify it completes without error
        await cmd.Execute();
    }

    [Fact]
    public async Task ExitCommand_Should_Invoke_Exit_Action()
    {
        var invoked = false;
        var vm  = MakeVm(exit: () => invoked = true);
        var cmd = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.ExitCommand;

        await cmd.Execute();

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task SelectCharacterCommand_Should_Navigate_To_CharacterSelectViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav);

        await ((ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.SelectCharacterCommand).Execute();

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task LogoutCommand_Should_Call_AuthService_LogoutAsync()
    {
        var auth = new FakeAuthService();
        var nav  = new FakeNavigationService();
        var vm   = new MainMenuViewModel(nav, new TokenStore(), auth);

        await ((ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.LogoutCommand).Execute();

        auth.LogoutCallCount.Should().Be(1);
    }

    [Fact]
    public void IsLoggedIn_Should_Be_False_When_TokenStore_Has_No_Token()
    {
        var tokens = new TokenStore();
        var vm     = new MainMenuViewModel(new FakeNavigationService(), tokens, new FakeAuthService());

        vm.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public void IsLoggedIn_Should_Be_True_When_TokenStore_Has_Token()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        var vm = new MainMenuViewModel(new FakeNavigationService(), tokens, new FakeAuthService());

        vm.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public void IsLoggedIn_Should_React_To_TokenStore_Changes()
    {
        var tokens = new TokenStore();
        var vm     = new MainMenuViewModel(new FakeNavigationService(), tokens, new FakeAuthService());

        vm.IsLoggedIn.Should().BeFalse();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        vm.IsLoggedIn.Should().BeTrue();

        tokens.Clear();
        vm.IsLoggedIn.Should().BeFalse();
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

    [Fact]
    public void Progress_Should_RaisePropertyChanged_When_Set()
    {
        var nav     = new FakeNavigationService();
        var vm      = new SplashViewModel(nav);
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Progress = 42.0;

        vm.Progress.Should().Be(42.0);
        changes.Should().Contain(nameof(SplashViewModel.Progress));
    }

    [Fact]
    public void StatusText_Should_RaisePropertyChanged_When_Set()
    {
        var nav     = new FakeNavigationService();
        var vm      = new SplashViewModel(nav);
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.StatusText = "Loading...";

        vm.StatusText.Should().Be("Loading...");
        changes.Should().Contain(nameof(SplashViewModel.StatusText));
    }
}
