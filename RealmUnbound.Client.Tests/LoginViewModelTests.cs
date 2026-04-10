using System.Reactive.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class LoginViewModelTests : TestBase
{
    private static LoginViewModel MakeVm(
        FakeAuthService? auth = null,
        FakeNavigationService? nav = null)
    {
        return new LoginViewModel(
            auth ?? new FakeAuthService(),
            nav  ?? new FakeNavigationService(),
            SessionStoreFactory.Create());
    }

    // CanExecute behaviour
    [Fact]
    public void LoginCommand_Should_Be_Disabled_When_Email_Is_Empty()
    {
        var vm = MakeVm();
        vm.Email    = string.Empty; // override any email saved to disk by SessionStore
        vm.Password = "Password1!";
        bool canExecute = false;
        vm.LoginCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void LoginCommand_Should_Be_Disabled_When_Password_Is_Empty()
    {
        var vm = MakeVm();
        vm.Email = "user@test.com";
        // Password is empty by default
        bool canExecute = false;
        vm.LoginCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void LoginCommand_Should_Be_Enabled_When_Email_And_Password_Filled()
    {
        var vm = MakeVm();
        vm.Email    = "user@test.com";
        vm.Password = "Password1!";

        bool canExecute = false;
        vm.LoginCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    // Successful login
    [Fact]
    public async Task LoginCommand_Should_Call_AuthService_LoginAsync()
    {
        var auth = new FakeAuthService();
        var vm   = MakeVm(auth: auth);
        vm.Email    = "user@test.com";
        vm.Password = "Password1!";

        await vm.LoginCommand.Execute();

        auth.LoginCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoginCommand_Should_Navigate_To_CharacterSelect_On_Success()
    {
        var auth = new FakeAuthService(); // defaults to success
        var nav  = new FakeNavigationService();
        var vm   = MakeVm(auth: auth, nav: nav);
        vm.Email    = "user@test.com";
        vm.Password = "Password1!";

        await vm.LoginCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task LoginCommand_Should_Show_Error_On_Failure()
    {
        var auth = new FakeAuthService
        {
            LoginResult = (null, new AppError("Invalid credentials"))
        };
        var vm = MakeVm(auth: auth);
        vm.Email    = "wrong@test.com";
        vm.Password = "wrongpass";

        await vm.LoginCommand.Execute();

        vm.ErrorMessage.Should().Be("Invalid credentials");
        vm.ErrorDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginCommand_Should_Use_Fallback_Error_When_No_Message()
    {
        var auth = new FakeAuthService { LoginResult = (null, null) };
        var vm   = MakeVm(auth: auth);
        vm.Email    = "user@test.com";
        vm.Password = "Password1!";

        await vm.LoginCommand.Execute();

        vm.ErrorMessage.Should().Be("Login failed.");
    }

    [Fact]
    public async Task LoginCommand_Should_Clear_ErrorMessage_Before_Attempt()
    {
        var auth = new FakeAuthService { LoginResult = (null, new AppError("First error")) };
        var vm   = MakeVm(auth: auth);
        vm.Email        = "user@test.com";
        vm.Password     = "Password1!";
        vm.ErrorMessage = "Previous error";

        await vm.LoginCommand.Execute();

        vm.ErrorMessage.Should().Be("First error"); // old error replaced, not prepended
    }

    [Fact]
    public async Task LoginCommand_Should_Clear_IsBusy_After_Completion()
    {
        var vm = MakeVm();
        vm.Email    = "user@test.com";
        vm.Password = "Password1!";

        await vm.LoginCommand.Execute();

        vm.IsBusy.Should().BeFalse();
    }

    // Back navigation
    [Fact]
    public async Task BackCommand_Should_Navigate_To_MainMenu()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.BackCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // OAuth external sign-in
    [Fact]
    public async Task LoginExternalCommand_Should_Navigate_To_CharacterSelect_On_Success()
    {
        var auth = new FakeAuthService(); // LoginResult defaults to success
        var nav  = new FakeNavigationService();
        var vm   = MakeVm(auth: auth, nav: nav);

        await vm.LoginExternalCommand.Execute("discord");

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task LoginExternalCommand_Should_Set_Error_On_Failure()
    {
        var auth = new FakeAuthService { LoginResult = (null, new AppError("OAuth failed")) };
        var vm   = MakeVm(auth: auth);

        await vm.LoginExternalCommand.Execute("google");

        vm.ErrorMessage.Should().Be("OAuth failed");
    }

    [Fact]
    public async Task LoginExternalCommand_Should_Clear_IsBusy_After_Completion()
    {
        var vm = MakeVm();

        await vm.LoginExternalCommand.Execute("microsoft");

        vm.IsBusy.Should().BeFalse();
    }

    // Property change notifications
    [Fact]
    public void Email_Should_Raise_PropertyChanged()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Email = "new@test.com";

        changes.Should().Contain(nameof(vm.Email));
    }

    [Fact]
    public void Password_Should_Raise_PropertyChanged()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Password = "secret";

        changes.Should().Contain(nameof(vm.Password));
    }

    // SessionStore integration (pre-fill & remember-me)
    [Fact]
    public void Email_Should_Be_Prefilled_When_SessionStore_HasSavedEmail()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"realm-login-prefill-{Guid.NewGuid()}.json");
        var session = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        session.SaveEmail("saved@test.com");

        var vm = new LoginViewModel(new FakeAuthService(), new FakeNavigationService(), session);

        vm.Email.Should().Be("saved@test.com");
        vm.RememberEmail.Should().BeTrue();
    }

    [Fact]
    public void Email_Should_Not_Be_Prefilled_When_SessionStore_Empty()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"realm-login-empty-{Guid.NewGuid()}.json");
        var session = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);

        var vm = new LoginViewModel(new FakeAuthService(), new FakeNavigationService(), session);

        vm.Email.Should().BeEmpty();
        vm.RememberEmail.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommand_Should_Save_Email_When_RememberEmail_Is_True()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"realm-login-save-{Guid.NewGuid()}.json");
        var session = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        var vm      = new LoginViewModel(new FakeAuthService(), new FakeNavigationService(), session);
        vm.Email        = "user@test.com";
        vm.Password     = "Password1!";
        vm.RememberEmail = true;

        await vm.LoginCommand.Execute();

        session.SavedEmail.Should().Be("user@test.com");
    }

    [Fact]
    public async Task LoginCommand_Should_Clear_Email_When_RememberEmail_Is_False()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"realm-login-clear-{Guid.NewGuid()}.json");
        var session = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        session.SaveEmail("old@test.com"); // pre-populate

        var vm = new LoginViewModel(new FakeAuthService(), new FakeNavigationService(), session);
        vm.Email         = "user@test.com";
        vm.Password      = "Password1!";
        vm.RememberEmail = false;

        await vm.LoginCommand.Execute();

        session.HasSavedEmail.Should().BeFalse();
    }

    [Fact]
    public void RememberEmail_Should_Raise_PropertyChanged()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.RememberEmail = true;

        changes.Should().Contain(nameof(vm.RememberEmail));
    }
}
