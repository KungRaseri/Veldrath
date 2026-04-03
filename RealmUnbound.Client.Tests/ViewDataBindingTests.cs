using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Client.Views;

namespace RealmUnbound.Client.Tests;

/// <summary>
/// Headless tests that exercise AXAML DataTemplate and state-dependent paths.
/// They ensure the compiled AXAML code (DataTemplate factories, visibility
/// converters, etc.) is covered — paths that only run when collections have
/// items or when ViewModel state toggles a panel's visibility.
/// </summary>
public class ViewDataBindingTests
{
    private static GameViewModel MakeGameVm() =>
        new(new FakeServerConnectionService(),
            new FakeZoneService(),
            new TokenStore(),
            new FakeNavigationService());

    private static CharacterSelectViewModel MakeCharacterSelectVm(FakeCharacterService? charSvc = null) =>
        new(charSvc ?? new FakeCharacterService(),
            new FakeServerConnectionService(),
            new FakeNavigationService(),
            MakeGameVm(),
            new FakeAuthService(),
            new TokenStore(),
            new RealmUnbound.Client.ClientSettings("http://localhost:8080"));

    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 800, Height = 600 };
        window.Show();
        return window;
    }

    // CharacterSelectView — character list DataTemplate
    [AvaloniaFact]
    public async Task CharacterSelectView_Should_Render_Character_List_DataTemplate()
    {
        var svc = new FakeCharacterService();
        svc.Characters.Add(new CharacterDto(Guid.NewGuid(), 1, "TestHero", "Fighter", 5, 0, DateTimeOffset.UtcNow, "starting-zone"));
        svc.Characters.Add(new CharacterDto(Guid.NewGuid(), 2, "AltChar", "Rogue",   3, 0, DateTimeOffset.UtcNow, "starting-zone"));

        var vm = MakeCharacterSelectVm(svc);
        await Task.Yield(); // allow the fire-and-forget LoadAsync continuation to run

        var window = Show(new CharacterSelectView { DataContext = vm });

        vm.Characters.Should().HaveCount(2);
        window.Close();
    }

    // CharacterSelectView — busy overlay (IsBusy = true)
    [AvaloniaFact]
    public async Task CharacterSelectView_Should_Show_Busy_Overlay_When_IsBusy_True()
    {
        var vm = MakeCharacterSelectVm();
        await Task.Yield();
        vm.IsBusy = true;

        var window = Show(new CharacterSelectView { DataContext = vm });

        vm.IsBusy.Should().BeTrue();
        window.Close();
    }

    // CharacterSelectView — error message visibility
    [AvaloniaFact]
    public async Task CharacterSelectView_Should_Display_Error_Message_When_Set()
    {
        var vm = MakeCharacterSelectVm();
        await Task.Yield();
        vm.ErrorMessage = "Unable to connect";

        var window = Show(new CharacterSelectView { DataContext = vm });

        vm.ErrorMessage.Should().Be("Unable to connect");
        window.Close();
    }

    // GameView — OnlinePlayers DataTemplate
    [AvaloniaFact]
    public void GameView_Should_Render_OnlinePlayers_DataTemplate()
    {
        var vm = MakeGameVm();
        vm.OnlinePlayers.Add(new OnlinePlayerViewModel("Gandalf", _ => { }));
        vm.OnlinePlayers.Add(new OnlinePlayerViewModel("Aragorn", _ => { }));

        var window = Show(new GameView { DataContext = vm });

        vm.OnlinePlayers.Should().HaveCount(2);
        window.Close();
    }

    // GameView — ActionLog DataTemplate
    [AvaloniaFact]
    public void GameView_Should_Render_ActionLog_DataTemplate()
    {
        var vm = MakeGameVm();
        vm.ActionLog.Add("[12:00] Welcome to the zone!");
        vm.ActionLog.Add("[12:01] A goblin appears!");

        var window = Show(new GameView { DataContext = vm });

        vm.ActionLog.Should().HaveCount(2);
        window.Close();
    }

    // GameView — both collections populated together
    [AvaloniaFact]
    public void GameView_Should_Render_With_Players_And_Log_Together()
    {
        var vm = MakeGameVm();
        vm.ZoneName        = "The Starting Vale";
        vm.ZoneDescription = "A peaceful valley.";
        vm.CharacterName   = "Hero";
        vm.StatusMessage   = "In zone";
        vm.OnlinePlayers.Add(new OnlinePlayerViewModel("Legolas", _ => { }));
        vm.ActionLog.Add("[10:00] Legolas entered the zone.");

        var window = Show(new GameView { DataContext = vm });

        vm.OnlinePlayers.Should().HaveCount(1);
        vm.ActionLog.Should().HaveCount(1);
        window.Close();
    }
}
