using System.Reactive.Linq;
using RealmUnbound.Client;
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
        GameViewModel?                gameVm = null,
        FakeAuthService?              auth = null,
        TokenStore?                   tokens = null)
    {
        conn   ??= new FakeServerConnectionService();
        nav    ??= new FakeNavigationService();
        gameVm ??= MakeGameVm(conn: conn, nav: nav);
        return new CharacterSelectViewModel(
            chars ?? new FakeCharacterService(),
            conn,
            nav,
            gameVm,
            auth ?? new FakeAuthService(),
            tokens ?? new TokenStore(),
            new ClientSettings("http://localhost:8080"));
    }

    // Initial load
    [Fact]
    public async Task Characters_Should_Be_Loaded_On_Construction()
    {
        var fake = new FakeCharacterService
        {
            Characters =
            [
                new CharacterDto(Guid.NewGuid(), 1, "Alice", "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone"),
                new CharacterDto(Guid.NewGuid(), 2, "Bob",   "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone")
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

    [Fact]
    public async Task LoadAsync_Should_Refresh_Token_When_Expiring_Then_Load_Characters()
    {
        var tokens  = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddMinutes(-5)); // expired
        var auth  = new FakeAuthService { RefreshResult = true };
        var chars = new FakeCharacterService
        {
            Characters = [new CharacterDto(Guid.NewGuid(), 1, "Hero", "Warrior", 1, 0, DateTimeOffset.UtcNow, "zone")]
        };
        var nav = new FakeNavigationService();

        var vm = MakeVm(chars: chars, nav: nav, auth: auth, tokens: tokens);
        await Task.Yield();

        auth.RefreshCallCount.Should().Be(1);
        vm.Characters.Should().HaveCount(1);
        nav.NavigationLog.Should().NotContain(typeof(MainMenuViewModel));
    }

    [Fact]
    public async Task LoadAsync_Should_Navigate_To_MainMenu_When_Token_Expiring_And_Refresh_Fails()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid(),
                   expiry: DateTimeOffset.UtcNow.AddMinutes(-5)); // expired
        var auth = new FakeAuthService { RefreshResult = false };
        var nav  = new FakeNavigationService();

        var vm = MakeVm(nav: nav, auth: auth, tokens: tokens);
        await Task.Yield();

        auth.RefreshCallCount.Should().Be(1);
        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // ShowCreate
    [Fact]
    public async Task ShowCreateCommand_Should_Navigate_To_CreateCharacterViewModel()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.ShowCreateCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(CreateCharacterViewModel));
    }

    // Character deletion
    [Fact]
    public async Task DeleteCommand_Should_Remove_Character_On_Success()
    {
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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

    // Select character (connection error)
    [Fact]
    public async Task SelectCommand_Should_Show_Error_When_Connection_Fails()
    {
        var conn      = new FakeServerConnectionService { ConnectShouldThrow = true };
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);
        var vm = MakeVm(conn: conn);

        await vm.SelectCommand.Execute(entry);

        vm.IsBusy.Should().BeFalse();
    }

    // SelectCommand happy-path hub callbacks
    [Fact]
    public async Task SelectCommand_Should_Navigate_To_GameViewModel_When_ZoneEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var nav    = new FakeNavigationService();
        var gameVm = MakeGameVm(conn: conn, nav: nav);
        var vm     = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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

        gameVm.OnlinePlayers.Select(p => p.Name).Should().BeEquivalentTo(["Alice", "Bob"]);
    }

    [Fact]
    public async Task SelectCommand_Should_Add_Player_When_PlayerEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("PlayerEntered",
            new CharacterSelectViewModel.PlayerEventPayload("Gandalf"));

        gameVm.OnlinePlayers.Should().Contain(p => p.Name == "Gandalf");
    }

    [Fact]
    public async Task SelectCommand_Should_Remove_Player_When_PlayerLeft_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("PlayerEntered", new CharacterSelectViewModel.PlayerEventPayload("Legolas"));
        conn.FireEvent("PlayerLeft",    new CharacterSelectViewModel.PlayerEventPayload("Legolas"));

        gameVm.OnlinePlayers.Should().NotContain(p => p.Name == "Legolas");
    }

    [Fact]
    public async Task SelectCommand_Should_Use_DefaultZone_When_CurrentZoneId_Is_Empty()
    {
        var conn      = new FakeServerConnectionService();
        var gameVm    = MakeGameVm(conn: conn);
        var vm        = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, ""); // empty zone
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);

        // No error = success path was taken with "starting-zone" fallback
        vm.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_DoesNotClearErrorFromUnrelatedCreate()
    {
        // If create failed and then user deletes a different character,
        // the delete error (if any) overrides, but a successful delete clears the board.
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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

    // LogoutCommand
    [Fact]
    public async Task LogoutCommand_Should_Call_AuthService_LogoutAsync()
    {
        var auth = new FakeAuthService();
        var conn = new FakeServerConnectionService();
        var nav  = new FakeNavigationService();
        var gameVm = MakeGameVm(conn: conn, nav: nav);
        var vm     = new CharacterSelectViewModel(
            new FakeCharacterService(), conn, nav, gameVm, auth, new TokenStore(),
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
            new TokenStore(),
            new ClientSettings("http://localhost:8080"));

        await vm.LogoutCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    // Hub Error event
    [Fact]
    public async Task SelectCommand_Should_Set_ErrorMessage_When_Hub_Sends_Error()
    {
        var conn      = new FakeServerConnectionService();
        var nav       = new FakeNavigationService();
        var gameVm    = MakeGameVm(conn: conn, nav: nav);
        var vm        = MakeVm(conn: conn, nav: nav, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Alice",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("Error", "Zone not found");

        nav.NavigationLog.Should().NotContain(typeof(GameViewModel));
    }

    // Hub callbacks — GoldChanged
    [Fact]
    public async Task SelectCommand_Should_Update_Gold_When_GoldChanged_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("GoldChanged",
            new CharacterSelectViewModel.GoldChangedPayload(character.Id, 100, 200, "Loot"));

        gameVm.ActionLog.Should().NotBeEmpty();
    }

    // Hub callbacks — DamageTaken
    [Fact]
    public async Task SelectCommand_Should_Update_Health_When_DamageTaken_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
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
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("DamageTaken",
            new CharacterSelectViewModel.DamageTakenPayload(character.Id, 30, 70, 100, false, "Trap"));

        gameVm.ActionLog.Should().NotBeEmpty();
    }

    // Hub callbacks — ExperienceGained
    [Fact]
    public async Task SelectCommand_Should_Update_Level_When_ExperienceGained_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ExperienceGained",
            new CharacterSelectViewModel.ExperienceGainedPayload(character.Id, 3, 50L, true, 3, "Combat"));

        gameVm.Level.Should().Be(3);
    }

    [Fact]
    public async Task SelectCommand_Should_Update_Experience_When_ExperienceGained_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ExperienceGained",
            new CharacterSelectViewModel.ExperienceGainedPayload(character.Id, 2, 75L, false, null, "Quest"));

        gameVm.Experience.Should().Be(75L);
    }

    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_ExperienceGained_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ExperienceGained",
            new CharacterSelectViewModel.ExperienceGainedPayload(character.Id, 1, 40L, false, null, "Explore"));

        gameVm.ActionLog.Should().NotBeEmpty();
    }

    // Hub callbacks — CharacterSelected (initial stat seeding)
    [Fact]
    public async Task SelectCommand_Should_Seed_All_Stats_When_CharacterSelected_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 3, 50L, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("CharacterSelected",
            new CharacterSelectViewModel.CharacterSelectedPayload(
                character.Id, character.Name, character.ClassName,
                Level: 3, Experience: 50L,
                CurrentZoneId: "starting-zone",
                CurrentHealth: 80, MaxHealth: 100,
                CurrentMana: 40, MaxMana: 50,
                Gold: 200, UnspentAttributePoints: 2,
                Strength: 10, Dexterity: 10, Constitution: 10,
                Intelligence: 10, Wisdom: 10, Charisma: 10,
                LearnedAbilities: [],
                SelectedAt: DateTimeOffset.UtcNow));

        gameVm.Level.Should().Be(3);
        gameVm.Experience.Should().Be(50L);
        gameVm.CurrentHealth.Should().Be(80);
        gameVm.MaxHealth.Should().Be(100);
        gameVm.CurrentMana.Should().Be(40);
        gameVm.MaxMana.Should().Be(50);
        gameVm.Gold.Should().Be(200);
        gameVm.UnspentAttributePoints.Should().Be(2);
    }

    [Fact]
    public async Task SelectCommand_Should_Seed_Level_And_Experience_From_CharacterSelected()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 7, 300L, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("CharacterSelected",
            new CharacterSelectViewModel.CharacterSelectedPayload(
                character.Id, character.Name, character.ClassName,
                Level: 7, Experience: 300L,
                CurrentZoneId: "starting-zone",
                CurrentHealth: 70, MaxHealth: 70,
                CurrentMana: 35, MaxMana: 35,
                Gold: 0, UnspentAttributePoints: 0,
                Strength: 10, Dexterity: 10, Constitution: 10,
                Intelligence: 10, Wisdom: 10, Charisma: 10,
                LearnedAbilities: [],
                SelectedAt: DateTimeOffset.UtcNow));

        gameVm.Level.Should().Be(7);
        gameVm.Experience.Should().Be(300L);
    }

    // Hub callbacks — ItemCrafted
    [Fact]
    public async Task SelectCommand_Should_Update_Gold_When_ItemCrafted_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ItemCrafted",
            new CharacterSelectViewModel.ItemCraftedPayload(character.Id, "iron-sword", 50, 150));

        gameVm.Gold.Should().Be(150);
    }

    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_ItemCrafted_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ItemCrafted",
            new CharacterSelectViewModel.ItemCraftedPayload(character.Id, "magic-staff", 50, 200));

        gameVm.ActionLog.Should().ContainSingle(msg => msg.Contains("magic-staff"));
    }

    // Hub callbacks — DungeonEntered
    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_DungeonEntered_Fires()
    {
        var conn   = new FakeServerConnectionService();
        var gameVm = MakeGameVm(conn: conn);
        var vm     = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("DungeonEntered",
            new CharacterSelectViewModel.DungeonEnteredPayload(character.Id, "dungeon-grotto", "dungeon-grotto"));

        gameVm.ActionLog.Should().ContainSingle(msg => msg.Contains("dungeon-grotto"));
    }

    // GetActiveCharacters on load
    [Fact]
    public async Task LoadAsync_Should_Call_GetActiveCharacters_On_Hub()
    {
        var conn = new FakeServerConnectionService();
        _ = MakeVm(conn: conn);
        await Task.Yield(); // allow fire-and-forget LoadAsync to complete

        conn.SentCommands.Should().Contain(c => c.Method == "GetActiveCharacters");
    }

    [Fact]
    public async Task LoadAsync_Should_Mark_Active_Characters_IsOnline()
    {
        var charId = Guid.NewGuid();
        var chars  = new FakeCharacterService
        {
            Characters =
            [
                new CharacterDto(charId, 1, "Alice", "Warrior", 1, 0, DateTimeOffset.UtcNow, "fenwick-crossing")
            ]
        };

        var conn = new FakeServerConnectionService { ActiveCharacterIds = [charId] };
        var vm   = MakeVm(chars: chars, conn: conn);
        await Task.Yield();

        vm.Characters.Should().ContainSingle(e => e.Character.Id == charId && e.IsOnline);
    }

    // Hub callbacks – ShopVisited
    [Fact]
    public async Task SelectCommand_Should_Append_Log_When_ShopVisited_Fires()
    {
        var conn      = new FakeServerConnectionService();
        var gameVm    = MakeGameVm(conn: conn);
        var vm        = MakeVm(conn: conn, gameVm: gameVm);
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero",
            "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var entry = new CharacterEntryViewModel(character);

        await vm.SelectCommand.Execute(entry);
        conn.FireEvent("ShopVisited",
            new CharacterSelectViewModel.ShopVisitedPayload(character.Id, "fenwick-crossing", "Fenwick Crossing"));

        gameVm.ActionLog.Should().Contain(msg => msg.Contains("Welcome to the shop at Fenwick Crossing"));
    }

}
