using System.Reactive.Linq;
using RealmUnbound.Client;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Content;

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
        GameViewModel?                gameVm = null,
        FakeContentService?           content = null)
    {
        conn   ??= new FakeServerConnectionService();
        nav    ??= new FakeNavigationService();
        gameVm ??= MakeGameVm(conn: conn, nav: nav);
        return new CharacterSelectViewModel(
            chars ?? new FakeCharacterService(),
            conn,
            nav,
            gameVm,
            new FakeAuthService(),
            FakeContentCache.Create(content),
            new ClientSettings("http://localhost:8080"));
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
        vm.SelectedClass = "Fighter";
        bool canExecute = false;
        vm.CreateCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void CreateCommand_Should_Be_Enabled_When_Name_And_Class_Are_Set()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";
        vm.SelectedClass = "Fighter";
        bool canExecute = false;
        vm.CreateCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void CreateCommand_Should_Be_Disabled_When_Class_Is_Not_Set()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";
        bool canExecute = false;
        vm.CreateCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    // ── Character creation ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_Should_Add_Character_To_List_On_Success()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";
        vm.SelectedClass = "Fighter";

        await vm.CreateCommand.Execute();

        vm.Characters.Should().ContainSingle(c => c.Character.Name == "Hero");
    }

    [Fact]
    public async Task CreateCommand_Should_Reset_Name_And_IsCreating_On_Success()
    {
        var vm = MakeVm();
        vm.IsCreating       = true;
        vm.NewCharacterName = "Hero";
        vm.SelectedClass    = "Fighter";

        await vm.CreateCommand.Execute();

        vm.NewCharacterName.Should().BeEmpty();
        vm.IsCreating.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCommand_Should_Show_Error_On_Failure()
    {
        var fake = new FakeCharacterService
        {
            CreateResult = (null, new AppError("Name already taken"))
        };
        var vm = MakeVm(chars: fake);
        vm.NewCharacterName = "Hero";
        vm.SelectedClass    = "Fighter";

        await vm.CreateCommand.Execute();

        vm.ErrorMessage.Should().Be("Name already taken");
    }

    [Fact]
    public async Task CreateCommand_Should_Use_Fallback_Error_When_No_Message()
    {
        var fake = new FakeCharacterService { CreateResult = (null, null) };
        var vm   = MakeVm(chars: fake);
        vm.NewCharacterName = "Hero";
        vm.SelectedClass    = "Fighter";

        await vm.CreateCommand.Execute();

        vm.ErrorMessage.Should().Be("Failed to create character.");
    }

    [Fact]
    public async Task CreateCommand_Should_Clear_IsBusy_After_Completion()
    {
        var vm = MakeVm();
        vm.NewCharacterName = "Hero";
        vm.SelectedClass    = "Fighter";

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

        var entry = vm.Characters.Single(e => e.Character.Id == character.Id);
        await vm.DeleteCommand.Execute(entry);

        vm.Characters.Should().NotContain(entry);
    }

    [Fact]
    public async Task DeleteCommand_Should_Show_Error_When_Delete_Fails()
    {
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var fake = new FakeCharacterService
        {
            Characters  = [character],
            DeleteError = new AppError("Character not found")
        };
        var vm = MakeVm(chars: fake);
        await Task.Yield();

        var entry = vm.Characters.Single(e => e.Character.Id == character.Id);
        await vm.DeleteCommand.Execute(entry);

        vm.ErrorMessage.Should().Be("Character not found");
    }

    // ── Select character (connection error) ───────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Show_Error_When_Connection_Fails()
    {
        var conn      = new FakeServerConnectionService { ConnectShouldThrow = true };
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);
        var vm = MakeVm(conn: conn);

        await vm.SelectCommand.Execute(entry);

        vm.ErrorMessage.Should().StartWith("Failed to connect:");
    }

    [Fact]
    public async Task SelectCommand_Should_Clear_IsBusy_After_Connection_Error()
    {
        var conn      = new FakeServerConnectionService { ConnectShouldThrow = true };
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);
        var vm = MakeVm(conn: conn);

        await vm.SelectCommand.Execute(entry);

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
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);

        // Simulate server firing "ZoneEntered" event
        conn.FireEvent("ZoneEntered",
            new CharacterSelectViewModel.ZoneEnteredPayload(
                "starting-zone", "Starter Zone", "A starter zone", "Town",
                [
                    new CharacterSelectViewModel.OccupantInfo(Guid.NewGuid(), "Alice", DateTimeOffset.UtcNow),
                    new CharacterSelectViewModel.OccupantInfo(Guid.NewGuid(), "Bob", DateTimeOffset.UtcNow)
                ]));

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
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        await gameVm.InitializeAsync("Hero", "starting-zone");

        conn.FireEvent("ZoneEntered",
            new CharacterSelectViewModel.ZoneEnteredPayload(
                "starting-zone", "Starter Zone", "A starter zone", "Town",
                [
                    new CharacterSelectViewModel.OccupantInfo(Guid.NewGuid(), "Alice", DateTimeOffset.UtcNow),
                    new CharacterSelectViewModel.OccupantInfo(Guid.NewGuid(), "Bob", DateTimeOffset.UtcNow)
                ]));

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
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
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
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
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
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);

        // No error = success path was taken with "starting-zone" fallback
        vm.ErrorMessage.Should().BeEmpty();
    }

    // ── Lifecycle: create → delete → recreate ─────────────────────────────────

    [Fact]
    public async Task CreateCommand_ThenDeleteCommand_RemovesCharacterFromList()
    {
        var fake = new FakeCharacterService();
        var vm   = MakeVm(chars: fake);
        vm.NewCharacterName = "Hero";
        await vm.CreateCommand.Execute();

        var created = vm.Characters.Single();
        await vm.DeleteCommand.Execute(created);

        vm.Characters.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeleteCreate_SameNameSucceeds_CharacterInList()
    {
        // First create succeeds; after delete, second create should also succeed
        var fake = new FakeCharacterService();
        var vm   = MakeVm(chars: fake);

        // Create "Hero"
        vm.NewCharacterName = "Hero";
        await vm.CreateCommand.Execute();
        var first = vm.Characters.Single();

        // Delete it
        await vm.DeleteCommand.Execute(first);
        vm.Characters.Should().BeEmpty();

        // Recreate with same name — FakeCharacterService defaults to success
        vm.NewCharacterName = "Hero";
        await vm.CreateCommand.Execute();

        vm.Characters.Should().ContainSingle(c => c.Character.Name == "Hero");
    }

    [Fact]
    public async Task CreateDeleteCreate_SameName_ErrorIsCleared()
    {
        var fake = new FakeCharacterService();
        var vm   = MakeVm(chars: fake);

        vm.NewCharacterName = "Hero";
        await vm.CreateCommand.Execute();
        var first = vm.Characters.Single();

        // Inject an error into the ViewModel, then go through the lifecycle
        vm.ErrorMessage = "Some stale error";
        await vm.DeleteCommand.Execute(first);

        vm.NewCharacterName = "Hero";
        await vm.CreateCommand.Execute();

        // Successful create should have cleared the error
        vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCommand_OnNameConflict_ShowsFriendlyError()
    {
        var fake = new FakeCharacterService
        {
            CreateResult = (null, new AppError("That character name is already taken. Please choose a different name."))
        };
        var vm = MakeVm(chars: fake);
        vm.NewCharacterName = "Taken";

        await vm.CreateCommand.Execute();

        vm.ErrorMessage.Should().Contain("already taken");
    }

    [Fact]
    public async Task ShowCreateCommand_ClearsErrorFromPreviousFailedCreate()
    {
        var fake = new FakeCharacterService
        {
            CreateResult = (null, new AppError("Name conflict"))
        };
        var vm = MakeVm(chars: fake);
        vm.NewCharacterName = "Bad";
        await vm.CreateCommand.Execute(); // sets vm.ErrorMessage

        // Open the create panel again
        await vm.CancelCreateCommand.Execute();
        await vm.ShowCreateCommand.Execute();

        vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_DoesNotClearErrorFromUnrelatedCreate()
    {
        // If create failed and then user deletes a different character,
        // the delete error (if any) overrides, but a successful delete clears the board.
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var fake = new FakeCharacterService
        {
            Characters  = [character],
            DeleteError = null   // delete succeeds
        };
        var vm = MakeVm(chars: fake);
        await Task.Yield();

        var entryToDelete = vm.Characters.Single(e => e.Character.Id == character.Id);
        vm.ErrorMessage = "Stale create error";
        await vm.DeleteCommand.Execute(entryToDelete);

        // A successful delete removes the character but does not clear unrelated errors
        vm.ErrorMessage.Should().Be("Stale create error");
    }

    [Fact]
    public async Task MultipleDeletesThenCreates_AllSucceed()
    {
        var fake = new FakeCharacterService();
        var vm   = MakeVm(chars: fake);

        // Create three characters
        foreach (var name in new[] { "Alpha", "Beta", "Gamma" })
        {
            vm.NewCharacterName = name;
            await vm.CreateCommand.Execute();
        }
        vm.Characters.Should().HaveCount(3);

        // Delete all three
        foreach (var c in vm.Characters.ToList())
            await vm.DeleteCommand.Execute(c);
        vm.Characters.Should().BeEmpty();

        // Recreate each — none should conflict in the fake service
        foreach (var name in new[] { "Alpha", "Beta", "Gamma" })
        {
            vm.NewCharacterName = name;
            await vm.CreateCommand.Execute();
        }
        vm.Characters.Should().HaveCount(3);
    }

    // ── LogoutCommand ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutCommand_Should_Call_AuthService_LogoutAsync()
    {
        var auth = new FakeAuthService();
        var conn = new FakeServerConnectionService();
        var nav  = new FakeNavigationService();
        var gameVm = MakeGameVm(conn: conn, nav: nav);
        var vm     = new CharacterSelectViewModel(
            new FakeCharacterService(), conn, nav, gameVm, auth, FakeContentCache.Create(),
            new ClientSettings("http://localhost:8080"));

        await vm.LogoutCommand.Execute();

        auth.LogoutCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LogoutCommand_Should_Navigate_To_MainMenuViewModel()
    {
        var auth = new FakeAuthService();
        var nav  = new FakeNavigationService();
        var vm   = new CharacterSelectViewModel(
            new FakeCharacterService(),
            new FakeServerConnectionService(),
            nav,
            MakeGameVm(nav: nav),
            auth,
            FakeContentCache.Create(),
            new ClientSettings("http://localhost:8080"));

        await vm.LogoutCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // ── PanelTitle ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PanelTitle_Should_Be_SelectYourCharacter_By_Default()
    {
        var vm = MakeVm();
        vm.PanelTitle.Should().Be("Select Your Character");
    }

    [Fact]
    public async Task PanelTitle_Should_Be_NewCharacter_When_IsCreating_Is_True()
    {
        var vm = MakeVm();
        await vm.ShowCreateCommand.Execute();
        vm.PanelTitle.Should().Be("New Character");
    }

    // ── SelectedClass / AvailableClasses ──────────────────────────────────────

    [Fact]
    public async Task CancelCreateCommand_Should_Reset_SelectedClass()
    {
        var vm = MakeVm();
        vm.SelectedClass = "Fighter";

        await vm.CancelCreateCommand.Execute();

        vm.SelectedClass.Should().BeEmpty();
    }

    [Fact]
    public async Task AvailableClasses_Should_Contain_Expected_Classes_When_Catalog_Empty()
    {
        // When the content catalog returns no classes the fallback hardcoded list is preserved.
        var vm = MakeVm(content: new FakeContentService { Classes = [] });
        await Task.Delay(50);
        vm.AvailableClasses.Should().Contain(["Fighter", "Mage", "Rogue", "Cleric", "Ranger", "Paladin"]);
    }

    // ── Hub Error event ───────────────────────────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Set_ErrorMessage_When_Hub_Sends_Error()
    {
        var conn      = new FakeServerConnectionService();
        var nav       = new FakeNavigationService();
        var gameVm    = MakeGameVm(conn: conn, nav: nav);
        var vm        = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("Error", "Character does not belong to this account");

        vm.ErrorMessage.Should().Be("Character does not belong to this account");
    }

    [Fact]
    public async Task SelectCommand_Should_Not_Navigate_To_Game_When_Hub_Sends_Error()
    {
        var conn      = new FakeServerConnectionService();
        var nav       = new FakeNavigationService();
        var gameVm    = MakeGameVm(conn: conn, nav: nav);
        var vm        = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("Error", "Zone not found");

        nav.NavigationLog.Should().NotContain(typeof(GameViewModel));
    }

    // ── AvailableClasses — content catalog loading ────────────────────────────

    [Fact]
    public async Task AvailableClasses_Should_Be_Populated_From_Content_Catalog()
    {
        var content = new FakeContentService
        {
            Classes =
            [
                new ActorClassDto("@classes/warriors:fighter", "Fighter", "class", 10, "Strength",     10),
                new ActorClassDto("@classes/mages:mage",       "Mage",    "class",  6, "Intelligence", 10),
            ]
        };

        var vm = MakeVm(content: content);
        await Task.Delay(50); // allow fire-and-forget LoadAsync to complete

        vm.AvailableClasses.Should().BeEquivalentTo(new[] { "Fighter", "Mage" });
    }

    [Fact]
    public async Task AvailableClasses_Should_Fall_Back_To_Builtins_When_Catalog_Empty()
    {
        var content = new FakeContentService { Classes = [] };
        var vm = MakeVm(content: content);
        await Task.Delay(50);

        // Built-in fallback list must contain at least one entry
        vm.AvailableClasses.Should().NotBeEmpty();
    }

    // ── Hub callbacks — GoldChanged ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Update_Gold_When_GoldChanged_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("GoldChanged",
            new CharacterSelectViewModel.GoldChangedPayload(character.Id, 50, 150, "Quest"));

        gameVm.Gold.Should().Be(150);
    }

    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_GoldChanged_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("GoldChanged",
            new CharacterSelectViewModel.GoldChangedPayload(character.Id, 100, 200, "Loot"));

        gameVm.ActionLog.Should().NotBeEmpty();
    }

    // ── Hub callbacks — DamageTaken ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectCommand_Should_Update_Health_When_DamageTaken_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("DamageTaken",
            new CharacterSelectViewModel.DamageTakenPayload(character.Id, 30, 70, 100, false, "Goblin"));

        gameVm.CurrentHealth.Should().Be(70);
    }

    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_DamageTaken_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "@classes/warriors:fighter", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("DamageTaken",
            new CharacterSelectViewModel.DamageTakenPayload(character.Id, 30, 70, 100, false, "Trap"));

        gameVm.ActionLog.Should().NotBeEmpty();
    }
}
