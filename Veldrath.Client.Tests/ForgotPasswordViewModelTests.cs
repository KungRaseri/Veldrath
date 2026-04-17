using System.Reactive.Linq;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Tests;

public class ForgotPasswordViewModelTests : TestBase
{
    private static ForgotPasswordViewModel MakeVm(
        FakeAuthService?       auth = null,
        FakeNavigationService? nav  = null)
    {
        return new ForgotPasswordViewModel(
            auth ?? new FakeAuthService(),
            nav  ?? new FakeNavigationService());
    }

    // CanExecute behaviour
    [Fact]
    public void SendResetCommand_Should_Be_Disabled_When_Email_Is_Empty()
    {
        var vm = MakeVm();
        bool canExecute = false;
        vm.SendResetCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SendResetCommand_Should_Be_Enabled_When_Email_Is_Present()
    {
        var vm = MakeVm();
        vm.Email = "user@example.com";
        bool canExecute = false;
        vm.SendResetCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void SendResetCommand_Should_Be_Disabled_After_Email_Sent()
    {
        var vm = MakeVm();
        vm.Email     = "user@example.com";
        vm.EmailSent = true;
        bool canExecute = false;
        vm.SendResetCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    // After submit
    [Fact]
    public async Task SendResetCommand_Should_Set_EmailSent_True_On_Success()
    {
        var vm = MakeVm();
        vm.Email = "user@example.com";

        await vm.DoSendResetAsync();

        vm.EmailSent.Should().BeTrue();
    }

    [Fact]
    public async Task SendResetCommand_Should_Not_Set_ErrorMessage_On_Success()
    {
        var vm = MakeVm();
        vm.Email = "user@example.com";

        await vm.DoSendResetAsync();

        vm.ErrorMessage.Should().BeNullOrEmpty();
    }

    // Back navigation
    [Fact]
    public async Task BackCommand_Should_Navigate_To_LoginViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.BackCommand.Execute();

        nav.NavigationLog.Should().ContainSingle().Which.Should().Be(typeof(LoginViewModel));
    }
}
