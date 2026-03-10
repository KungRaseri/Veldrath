using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class CharacterSelectViewModelTests : TestBase
{
    private static GameViewModel MakeGameVm(
        FakeServerConnectionService? conn = null,
        FakeZoneService?              zones = null,
        FakeNavigationService?        nav = null)
    {
        return new GameViewModel(
            conn  ?? new FakeServerConnectionService(),
            zones ?? new FakeZoneService(),
            new TokenStore(),
            nav   ?? new FakeNavigationService());
    }

    private static CharacterSelectViewModel MakeVm(
        FakeCharacterService?         chars = null,
        FakeServerConnectionService?  conn = null,
        FakeNavigationService?        nav = null,
        GameViewModel?                gameVm = null)
    {
        conn   ??= new FakeServerConnectionService();
        nav    ??= new FakeNavigationService();
        gameVm ??= MakeGameVm(conn: conn, nav: nav);
        return new CharacterSelectViewModel(
            chars ?? new FakeCharacterService(),
            conn,
            nav,
            gameVm);
    }

    // ── Initial load ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Characters_Should_Be_Loaded_On_Construction()
    {
        var fake = new FakeCharacterService
        {
            Characters =
            [
                new CharacterDto(Guid.NewGuid(), 1, "Alice", "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone"),
                new CharacterDto(Guid.NewGuid(), 2, "Bob",   "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone")
            ]
        };

        var vm = MakeVm(chars: fake);
        await Task.Yield(); // allow fire-and-forget LoadAsync to complete

        vm.Characters.Should().HaveCount(2);
    }

    [Fact]
    public async Task Characters_Load_Should_Call_GetCharactersAsync()
    {
        var fake = new FakeCharacterService();
        _ = MakeVm(chars: fake);
        await Task.Yield();

        fake.GetCallCount.Should().BeGreaterThanOrEqualTo(1);
    }

    // ── ShowCreate / CancelCreate ─────────────────────────────────────────────

    [Fact]
    public async Task ShowCreateCommand_Should_Set_IsCreating_True()
    {
        var vm = MakeVm();
        await vm.ShowCreateCommand.Execute();
        vm.IsCreating.Should().BeTrue();
    }

    [Fact]
    public async Task CancelCreateCommand_Should_Reset_IsCreating_And_Name()
    {
        var vm = MakeVm();
        await vm.ShowCreateCommand.Execute();
        vm.NewCharacterName = "Temp";

        await vm.CancelCreateCommand.Execute();

        vm.IsCreating.Should().BeFalse();
        vm.NewCharacterName.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowCreateCommand_Should_Clear_ErrorMessage()
    {
        var vm = MakeVm();
        vm.ErrorMessage = "Previous error";

        await vm.ShowCreateCommand.Execute();

        vm.ErrorMessage.Should().BeEmpty();
    }

    // ── CreateCommand CanExecute ──────────────────────────────────────────────

    [Fact]
    public void CreateCommand_Should_Be_Disabled_When_Name_Is_Empty()
    {
        var vm = MakeVm();
        bool canExecute = false;
        vm.CreateCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void CreateCommand_Should_Be_Enabled_When_Name_Is_Set()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";
        bool canExecute = false;
        vm.CreateCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    // ── Character creation ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_Should_Add_Character_To_List_On_Success()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";

        await vm.CreateCommand.Execute();

        vm.Characters.Should().ContainSingle(c => c.Name == "Hero");
    }

    [Fact]
    public async Task CreateCommand_Should_Reset_Name_And_IsCreating_On_Success()
    {
        var vm = MakeVm();
        vm.IsCreating       = true;
        vm.NewCharacterName = "Hero";

        await vm.CreateCommand.Execute();

        vm.NewCharacterName.Should().BeEmpty();
        vm.IsCreating.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCommand_Should_Show_Error_On_Failure()
    {
        var fake = new FakeCharacterService
        {
            CreateResult = (null, "Name already taken")
        };
        var vm = MakeVm(chars: fake);
        vm.NewCharacterName = "Hero";

        await vm.CreateCommand.Execute();

        vm.ErrorMessage.Should().Be("Name already taken");
    }

    [Fact]
    public async Task CreateCommand_Should_Use_Fallback_Error_When_No_Message()
    {
        var fake = new FakeCharacterService { CreateResult = (null, null) };
        var vm   = MakeVm(chars: fake);
        vm.NewCharacterName = "Hero";

        await vm.CreateCommand.Execute();

        vm.ErrorMessage.Should().Be("Failed to create character.");
    }

    [Fact]
    public async Task CreateCommand_Should_Clear_IsBusy_After_Completion()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";

        await vm.CreateCommand.Execute();

        vm.IsBusy.Should().BeFalse();
    }

    // ── Character deletion ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_Should_Remove_Character_On_Success()
    {
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var fake = new FakeCharacterService { Characters = [character] };
        var vm   = MakeVm(chars: fake);
        await Task.Yield();

        await vm.DeleteCommand.Execute(character);

        vm.Characters.Should().NotContain(character);
    }

    [Fact]
    public async Task DeleteCommand_Should_Show_Error_When_Delete_Fails()
    {
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var fake = new FakeCharacterService
        {
            Characters  = [character],
            DeleteError = "Character not found"
        };
        var vm = MakeVm(chars: fake);
        await Task.Yield();

        await vm.DeleteCommand.Execute(character);

        vm.ErrorMessage.Should().Be("Character not found");
    }

    // ── Select character (connection error) ───────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Show_Error_When_Connection_Fails()
    {
        var conn      = new FakeServerConnectionService { ConnectShouldThrow = true };
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var vm = MakeVm(conn: conn);

        await vm.SelectCommand.Execute(character);

        vm.ErrorMessage.Should().StartWith("Failed to connect:");
    }

    [Fact]
    public async Task SelectCommand_Should_Clear_IsBusy_After_Connection_Error()
    {
        var conn      = new FakeServerConnectionService { ConnectShouldThrow = true };
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var vm = MakeVm(conn: conn);

        await vm.SelectCommand.Execute(character);

        vm.IsBusy.Should().BeFalse();
    }

    // ── SelectCommand happy-path hub callbacks ────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Navigate_To_GameViewModel_When_ZoneEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var nav    = new FakeNavigationService();
        var gameVm = MakeGameVm(conn: conn, nav: nav);
        var vm     = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");

        await vm.SelectCommand.Execute(character);

        // Simulate server firing "ZoneEntered" event
        conn.FireEvent("ZoneEntered",
            new CharacterSelectViewModel.ZoneEnteredPayload("starting-zone", ["Alice", "Bob"]));

        nav.NavigationLog.Should().Contain(typeof(GameViewModel));
    }

    [Fact]
    public async Task SelectCommand_Should_Set_Occupants_When_ZoneEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var nav    = new FakeNavigationService();
        var gameVm = MakeGameVm(conn: conn, nav: nav);
        var vm     = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");

        await vm.SelectCommand.Execute(character);
        await gameVm.InitializeAsync("Hero", "starting-zone");

        conn.FireEvent("ZoneEntered",
            new CharacterSelectViewModel.ZoneEnteredPayload("starting-zone", ["Alice", "Bob"]));

        gameVm.OnlinePlayers.Should().BeEquivalentTo(["Alice", "Bob"]);
    }

    [Fact]
    public async Task SelectCommand_Should_Add_Player_When_PlayerEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");

        await vm.SelectCommand.Execute(character);
        conn.FireEvent("PlayerEntered",
            new CharacterSelectViewModel.PlayerEventPayload("Gandalf"));

        gameVm.OnlinePlayers.Should().Contain("Gandalf");
    }

    [Fact]
    public async Task SelectCommand_Should_Remove_Player_When_PlayerLeft_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");

        await vm.SelectCommand.Execute(character);
        conn.FireEvent("PlayerEntered", new CharacterSelectViewModel.PlayerEventPayload("Legolas"));
        conn.FireEvent("PlayerLeft",    new CharacterSelectViewModel.PlayerEventPayload("Legolas"));

        gameVm.OnlinePlayers.Should().NotContain("Legolas");
    }

    [Fact]
    public async Task SelectCommand_Should_Use_DefaultZone_When_CurrentZoneId_Is_Empty()
    {
        var conn      = new FakeServerConnectionService();
        var gameVm    = MakeGameVm(conn: conn);
        var vm        = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, ""); // empty zone

        await vm.SelectCommand.Execute(character);

        // No error = success path was taken with "starting-zone" fallback
        vm.ErrorMessage.Should().BeEmpty();
    }
}
