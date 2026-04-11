using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Announcements;

namespace Veldrath.Client.Tests;

public class MainMenuViewModelTests : TestBase
{
    private static MainMenuViewModel MakeVm(
        FakeNavigationService?    nav    = null,
        Action?                   exit   = null,
        FakeServerStatusService?  status = null,
        FakeAnnouncementService?  ann    = null)
        => new MainMenuViewModel(
            nav ?? new FakeNavigationService(),
            new TokenStore(),
            new FakeAuthService(),
            exit,
            serverStatus: status,
            announcementService: ann);

    [Fact]
    public void Title_Should_Be_Veldrath()
    {
        var vm = MakeVm();
        vm.Title.Should().Be("Veldrath");
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
    public async Task SettingsCommand_Should_Navigate_To_SettingsViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav);
        var cmd = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.SettingsCommand;

        await cmd.Execute();

        nav.NavigationLog.Should().Contain(typeof(SettingsViewModel));
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
        var nav    = new FakeNavigationService();
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        var vm = new MainMenuViewModel(nav, tokens, new FakeAuthService(), serverStatus: new FakeServerStatusService { IsOnline = true });

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

    // ── Server status ────────────────────────────────────────────────────────

    [Fact]
    public void IsServerOnline_Should_Default_To_True_When_No_Status_Service_Provided()
    {
        var vm = MakeVm();
        vm.IsServerOnline.Should().BeTrue();
    }

    [Fact]
    public void IsServerOnline_Should_Be_False_When_Service_Reports_Offline()
    {
        var vm = MakeVm(status: new FakeServerStatusService { IsOnline = false });
        vm.IsServerOnline.Should().BeFalse();
    }

    [Fact]
    public void IsServerOnline_Should_React_To_Status_Service_Changes()
    {
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status);

        status.IsOnline = false;

        vm.IsServerOnline.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterCommand_Should_Be_Disabled_When_Server_Is_Offline()
    {
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status);
        var cmd    = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.RegisterCommand;

        var canExecute = await cmd.CanExecute.FirstAsync();

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommand_Should_Be_Disabled_When_Server_Is_Offline()
    {
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status);
        var cmd    = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.LoginCommand;

        var canExecute = await cmd.CanExecute.FirstAsync();

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task All_Server_Gated_Commands_Should_Be_Disabled_While_Check_Is_In_Progress()
    {
        var checkStarted = new TaskCompletionSource();
        var checkRelease = new TaskCompletionSource();

        var status = new FakeServerStatusService
        {
            IsOnline = true,
            CheckOverride = (_, _) =>
            {
                checkStarted.TrySetResult();
                return checkRelease.Task;
            }
        };
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav, status: status);

        // Start a command but don't await — it blocks inside CheckAsync.
        var registerCmd  = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.RegisterCommand;
        var loginCmd     = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.LoginCommand;
        var executeTask  = registerCmd.Execute().ToTask();

        // Wait until CheckAsync is actually running.
        await checkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While a check is in progress, every server-gated command should be disabled —
        // not just the one that was clicked.
        var loginCanExecute = await loginCmd.CanExecute.FirstAsync();
        loginCanExecute.Should().BeFalse("LoginCommand should be disabled while a check is in progress");
        vm.IsChecking.Should().BeTrue();

        // Release the check and let the command finish.
        checkRelease.SetResult();
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5));

        // After the check completes, commands should be enabled again.
        var loginCanExecuteAfter = await loginCmd.CanExecute.FirstAsync();
        loginCanExecuteAfter.Should().BeTrue();
        vm.IsChecking.Should().BeFalse();
    }

    [Fact]
    public async Task SelectCharacterCommand_Should_Be_Disabled_When_Server_Is_Offline()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = new MainMenuViewModel(new FakeNavigationService(), tokens, new FakeAuthService(), serverStatus: status);
        var cmd    = (ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>)vm.SelectCharacterCommand;

        var canExecute = await cmd.CanExecute.FirstAsync();

        canExecute.Should().BeFalse();
    }

    // ── Announcements ────────────────────────────────────────────────────────

    [Fact]
    public void Announcements_Should_Be_Empty_Initially()
    {
        var vm = MakeVm();
        vm.Announcements.Should().BeEmpty();
        vm.HasAnnouncements.Should().BeFalse();
    }

    [Fact]
    public void NewsPlaceholderText_Should_Mention_Server_When_Offline()
    {
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status);

        vm.NewsPlaceholderText.Should().Contain("server");
    }

    [Fact]
    public void NewsPlaceholderText_Should_Say_No_Announcements_When_Online_And_Empty()
    {
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status);

        vm.NewsPlaceholderText.Should().NotContain("server").And.NotBeEmpty();
    }

    [Fact]
    public async Task Announcements_Should_Be_Populated_After_Load_When_Service_Returns_Data()
    {
        var ann = new FakeAnnouncementService
        {
            Announcements =
            [
                new AnnouncementDto(1, "Test", "Body text", "News", false, DateTimeOffset.UtcNow)
            ]
        };
        var status = new FakeServerStatusService { IsOnline = true };
        var vm     = MakeVm(status: status, ann: ann);

        // Announcements are loaded asynchronously; poll briefly.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!vm.HasAnnouncements && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        vm.HasAnnouncements.Should().BeTrue();
        vm.Announcements.Should().HaveCount(1);
        vm.Announcements[0].Title.Should().Be("Test");
    }

    [Fact]
    public async Task Announcements_Should_Reload_When_Server_Comes_Back_Online()
    {
        // Start offline — announcements will not load initially.
        var ann = new FakeAnnouncementService
        {
            Announcements =
            [
                new AnnouncementDto(1, "Back Online", "Server recovered.", "News", false, DateTimeOffset.UtcNow)
            ]
        };
        var status = new FakeServerStatusService { IsOnline = false };
        var vm     = MakeVm(status: status, ann: ann);

        // Still offline — list should be empty.
        vm.HasAnnouncements.Should().BeFalse();

        // Simulate the server coming back online.
        status.IsOnline = true;

        // Announcements are reloaded asynchronously; poll briefly.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!vm.HasAnnouncements && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        vm.HasAnnouncements.Should().BeTrue();
        vm.Announcements[0].Title.Should().Be("Back Online");
        vm.NewsPlaceholderText.Should().NotContain("server");
    }
}

public class SplashViewModelTests : TestBase
{
    private static SplashViewModel MakeVm(
        FakeNavigationService? nav  = null,
        TokenStore?            tokens = null,
        FakeAuthService?       auth   = null,
        FakeServerStatusService? status = null)
        => new SplashViewModel(
            nav    ?? new FakeNavigationService(),
            new FakeAssetStore(),
            tokens ?? new TokenStore(),
            auth   ?? new FakeAuthService(),
            status);

    [Fact]
    public void Title_Should_Be_Veldrath()
    {
        var vm = MakeVm();
        vm.Title.Should().Be("Veldrath");
    }

    [Fact]
    public void Subtitle_Should_Not_Be_Empty()
    {
        var vm = MakeVm();
        vm.Subtitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SplashViewModel_Should_Navigate_To_MainMenu_When_Not_Authenticated()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav);

        await vm.SplashTask;

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
        nav.NavigationLog.Should().NotContain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task SplashViewModel_Should_Refresh_Token_And_Navigate_To_MainMenu_When_Authenticated()
    {
        var nav    = new FakeNavigationService();
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddHours(1));
        var auth   = new FakeAuthService { RefreshResult = true };
        var status = new FakeServerStatusService { IsOnline = true };

        var vm = MakeVm(nav, tokens, auth, status);
        await vm.SplashTask;

        auth.RefreshCallCount.Should().Be(1);
        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
        nav.NavigationLog.Should().NotContain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task SplashViewModel_Should_Logout_When_Server_Is_Offline_And_Token_Is_Present()
    {
        var nav    = new FakeNavigationService();
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddHours(1));
        var auth   = new FakeAuthService();
        var status = new FakeServerStatusService { IsOnline = false };

        var vm = MakeVm(nav, tokens, auth, status);
        await vm.SplashTask;

        // Server down — must call LogoutAsync to clear local state; must NOT call RefreshAsync.
        auth.LogoutCallCount.Should().Be(1);
        auth.RefreshCallCount.Should().Be(0);
        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    [Fact]
    public async Task SplashViewModel_Should_Logout_When_Refresh_Fails()
    {
        var nav    = new FakeNavigationService();
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddMinutes(-5));
        var auth   = new FakeAuthService { RefreshResult = false };
        var status = new FakeServerStatusService { IsOnline = true };

        var vm = MakeVm(nav, tokens, auth, status);
        await vm.SplashTask;

        auth.RefreshCallCount.Should().Be(1);
        auth.LogoutCallCount.Should().Be(1);
        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    [Fact]
    public async Task SplashViewModel_Should_Still_Navigate_To_MainMenu_When_Refresh_Fails()
    {
        var nav    = new FakeNavigationService();
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddMinutes(-5));
        var auth = new FakeAuthService { RefreshResult = false };

        var vm = MakeVm(nav, tokens, auth);
        await vm.SplashTask;

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
        nav.NavigationLog.Should().NotContain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public void Progress_Should_RaisePropertyChanged_When_Set()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Progress = 42.0;

        vm.Progress.Should().Be(42.0);
        changes.Should().Contain(nameof(SplashViewModel.Progress));
    }

    [Fact]
    public void StatusText_Should_RaisePropertyChanged_When_Set()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.StatusText = "Loading...";

        vm.StatusText.Should().Be("Loading...");
        changes.Should().Contain(nameof(SplashViewModel.StatusText));
    }
}
