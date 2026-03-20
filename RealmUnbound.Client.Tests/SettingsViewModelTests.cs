using System.Reactive.Linq;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class SettingsViewModelTests : TestBase
{
    private static SettingsViewModel MakeVm(FakeNavigationService? nav = null, RealmUnbound.Client.ClientSettings? settings = null)
        => new SettingsViewModel(nav ?? new FakeNavigationService(), settings ?? new RealmUnbound.Client.ClientSettings("http://localhost:8080"));

    // ── BackCommand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BackCommand_Should_Navigate_To_MainMenuViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.BackCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // ── ServerUrl ─────────────────────────────────────────────────────────────

    [Fact]
    public void ServerUrl_Should_Reflect_ClientSettings_ServerBaseUrl()
    {
        var settings = new RealmUnbound.Client.ClientSettings("https://my-server:9000");
        var vm       = MakeVm(settings: settings);

        vm.ServerUrl.Should().Be("https://my-server:9000");
    }

    [Fact]
    public void ServerUrl_Set_Should_Update_ClientSettings_ServerBaseUrl()
    {
        var settings = new RealmUnbound.Client.ClientSettings("http://localhost:8080");
        var vm       = MakeVm(settings: settings);

        vm.ServerUrl = "https://prod.example.com";

        settings.ServerBaseUrl.Should().Be("https://prod.example.com");
    }
}
