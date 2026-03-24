using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class SettingsViewModelTests : TestBase
{
    private static (SettingsViewModel Vm, FakeAudioPlayer Audio, ClientSettings Settings) MakeVm(
        FakeNavigationService? nav             = null,
        ClientSettings? settings               = null,
        FakeAudioPlayer? audio                 = null,
        SettingsPersistenceService? persistence = null)
    {
        var s = settings ?? new ClientSettings("http://localhost:8080");
        var a = audio    ?? new FakeAudioPlayer();
        var p = persistence ?? new SettingsPersistenceService(Path.GetTempFileName());
        var vm = new SettingsViewModel(nav ?? new FakeNavigationService(), s, a, p);
        return (vm, a, s);
    }

    // BackCommand
    [Fact]
    public async Task BackCommand_Should_Navigate_To_MainMenuViewModel()
    {
        var nav = new FakeNavigationService();
        var (vm, _, _) = MakeVm(nav: nav);

        await vm.BackCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    [Fact]
    public async Task BackCommand_Should_Persist_Settings_Before_Navigating()
    {
        var tempFile    = Path.GetTempFileName();
        var persistence = new SettingsPersistenceService(tempFile);
        var settings    = new ClientSettings("http://testserver:9090") { MusicVolume = 42, Muted = true };
        var (vm, _, _) = MakeVm(settings: settings, persistence: persistence);

        await vm.BackCommand.Execute();

        var loaded = persistence.Load();
        loaded.Should().NotBeNull();
        loaded!.MusicVolume.Should().Be(42);
        loaded.Muted.Should().BeTrue();
        loaded.ServerBaseUrl.Should().Be("http://testserver:9090");
    }

    // Audio — volume properties
    [Fact]
    public void MasterVolume_Should_Reflect_ClientSettings_Default()
    {
        var (vm, _, _) = MakeVm();
        vm.MasterVolume.Should().Be(100);
    }

    [Fact]
    public void MusicVolume_Should_Reflect_ClientSettings_Default()
    {
        var (vm, _, _) = MakeVm();
        vm.MusicVolume.Should().Be(80);
    }

    [Fact]
    public void SfxVolume_Should_Reflect_ClientSettings_Default()
    {
        var (vm, _, _) = MakeVm();
        vm.SfxVolume.Should().Be(100);
    }

    [Fact]
    public void Setting_MusicVolume_Updates_AudioPlayer()
    {
        var (vm, audio, _) = MakeVm();

        vm.MusicVolume = 50;

        // master=100, music=50 → scaled = 100/100 * 50 = 50
        audio.MusicVolume.Should().Be(50);
    }

    [Fact]
    public void Setting_SfxVolume_Updates_AudioPlayer()
    {
        var (vm, audio, _) = MakeVm();

        vm.SfxVolume = 60;

        audio.SfxVolume.Should().Be(60);
    }

    [Fact]
    public void Setting_MasterVolume_Scales_Both_AudioChannels()
    {
        var settings = new ClientSettings("http://localhost:8080")
        {
            MusicVolume = 80,
            SfxVolume   = 100
        };
        var (vm, audio, _) = MakeVm(settings: settings);

        vm.MasterVolume = 50;

        // music: 50/100 * 80 = 40 ; sfx: 50/100 * 100 = 50
        audio.MusicVolume.Should().Be(40);
        audio.SfxVolume.Should().Be(50);
    }

    // Audio — mute
    [Fact]
    public void Muted_DefaultsTo_False()
    {
        var (vm, _, _) = MakeVm();
        vm.Muted.Should().BeFalse();
    }

    [Fact]
    public void Setting_Muted_True_Calls_SetMuted_On_AudioPlayer()
    {
        var (vm, audio, _) = MakeVm();

        vm.Muted = true;

        audio.Muted.Should().BeTrue();
    }

    [Fact]
    public void Setting_Muted_False_Unmutes_AudioPlayer()
    {
        var (vm, audio, _) = MakeVm();
        vm.Muted = true;

        vm.Muted = false;

        audio.Muted.Should().BeFalse();
    }

    // Display
    [Fact]
    public void FullScreen_DefaultsTo_False()
    {
        var (vm, _, _) = MakeVm();
        vm.FullScreen.Should().BeFalse();
    }

    [Fact]
    public void Setting_FullScreen_Updates_ClientSettings()
    {
        var settings = new ClientSettings("http://localhost:8080");
        var (vm, _, _) = MakeVm(settings: settings);

        vm.FullScreen = true;

        settings.FullScreen.Should().BeTrue();
    }
}

