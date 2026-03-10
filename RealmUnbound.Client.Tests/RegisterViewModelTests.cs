using System.Reactive.Linq;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class RegisterViewModelTests : TestBase
{
    private static RegisterViewModel MakeVm(
        FakeAuthService?    auth = null,
        FakeNavigationService? nav = null)
    {
        return new RegisterViewModel(
            auth ?? new FakeAuthService(),
            nav  ?? new FakeNavigationService());
    }

    // ── CanExecute behaviour ──────────────────────────────────────────────────

    [Fact]
    public void RegisterCommand_Should_Be_Disabled_When_All_Fields_Empty()
    {
        var vm = MakeVm();
        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void RegisterCommand_Should_Be_Disabled_When_Passwords_Do_Not_Match()
    {
        var vm = MakeVm();
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Different!";

        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void RegisterCommand_Should_Be_Enabled_When_All_Fields_Valid_And_Passwords_Match()
    {
        var vm = MakeVm();
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void RegisterCommand_Should_Be_Disabled_After_IsBusy_Set_True()
    {
        var vm = MakeVm();
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";
        vm.IsBusy          = true;

        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    // ── Successful registration ───────────────────────────────────────────────

    [Fact]
    public async Task RegisterCommand_Should_Call_AuthService_RegisterAsync()
    {
        var auth = new FakeAuthService();
        var vm   = MakeVm(auth: auth);
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        await vm.RegisterCommand.Execute();

        auth.RegisterCallCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterCommand_Should_Navigate_To_CharacterSelect_On_Success()
    {
        var auth = new FakeAuthService(); // defaults to success
        var nav  = new FakeNavigationService();
        var vm   = MakeVm(auth: auth, nav: nav);
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        await vm.RegisterCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task RegisterCommand_Should_Show_Error_On_Failure()
    {
        var auth = new FakeAuthService { RegisterResult = (null, "Email already taken") };
        var vm   = MakeVm(auth: auth);
        vm.Email           = "dupe@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        await vm.RegisterCommand.Execute();

        vm.ErrorMessage.Should().Be("Email already taken");
    }

    [Fact]
    public async Task RegisterCommand_Should_Use_Fallback_Error_When_No_Message()
    {
        var auth = new FakeAuthService { RegisterResult = (null, null) };
        var vm   = MakeVm(auth: auth);
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        await vm.RegisterCommand.Execute();

        vm.ErrorMessage.Should().Be("Registration failed.");
    }

    [Fact]
    public async Task RegisterCommand_Should_Clear_IsBusy_After_Completion()
    {
        var vm = MakeVm();
        vm.Email           = "user@test.com";
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        await vm.RegisterCommand.Execute();

        vm.IsBusy.Should().BeFalse();
    }

    // ── Back navigation ───────────────────────────────────────────────────────

    [Fact]
    public async Task BackCommand_Should_Navigate_To_MainMenu()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.BackCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // ── Field validation ──────────────────────────────────────────────────────

    [Fact]
    public void RegisterCommand_Disabled_When_Only_Email_Missing()
    {
        var vm = MakeVm();
        vm.Username        = "Alice";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void RegisterCommand_Disabled_When_Only_Username_Missing()
    {
        var vm = MakeVm();
        vm.Email           = "user@test.com";
        vm.Password        = "Password1!";
        vm.ConfirmPassword = "Password1!";

        bool canExecute = false;
        vm.RegisterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }
}
