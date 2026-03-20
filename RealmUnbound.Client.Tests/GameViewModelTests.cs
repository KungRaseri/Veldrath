using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class GameViewModelTests : TestBase
{
    private static GameViewModel MakeVm(
        FakeServerConnectionService? conn  = null,
        FakeZoneService?              zones = null,
        TokenStore?                   tokens = null,
        FakeNavigationService?        nav  = null)
    {
        return new GameViewModel(
            conn   ?? new FakeServerConnectionService(),
            zones  ?? new FakeZoneService(),
            tokens ?? new TokenStore(),
            nav    ?? new FakeNavigationService());
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void CharacterName_Should_Default_To_Adventurer_When_Token_Not_Set()
    {
        var vm = MakeVm();
        vm.CharacterName.Should().Be("Adventurer");
    }

    [Fact]
    public void CharacterName_Should_Use_Token_Username_When_Present()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "Hero", Guid.NewGuid());

        var vm = MakeVm(tokens: tokens);

        vm.CharacterName.Should().Be("Hero");
    }

    [Fact]
    public void OnlinePlayers_Should_Start_Empty()
    {
        var vm = MakeVm();
        vm.OnlinePlayers.Should().BeEmpty();
    }

    [Fact]
    public void ActionLog_Should_Start_Empty()
    {
        var vm = MakeVm();
        vm.ActionLog.Should().BeEmpty();
    }

    // ── InitializeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_Should_Set_CharacterName()
    {
        var vm = MakeVm();
        await vm.InitializeAsync("Aragorn", "starting-zone");
        vm.CharacterName.Should().Be("Aragorn");
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_ZoneName_From_Service()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("starting-zone", "The Starting Vale",
                "A peaceful valley.", "outdoor", 1, 50, true, 0)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "starting-zone");

        vm.ZoneName.Should().Be("The Starting Vale");
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_ZoneDescription_From_Service()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("starting-zone", "The Starting Vale",
                "A peaceful valley.", "outdoor", 1, 50, true, 0)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "starting-zone");

        vm.ZoneDescription.Should().Be("A peaceful valley.");
    }

    [Fact]
    public async Task InitializeAsync_Should_Append_Welcome_Message_To_ActionLog()
    {
        var vm = MakeVm();
        await vm.InitializeAsync("Frodo", "starting-zone");
        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("Frodo"));
    }

    [Fact]
    public async Task InitializeAsync_Should_Handle_Null_Zone_Gracefully()
    {
        var zones = new FakeZoneService { ZoneToReturn = null };
        var vm    = MakeVm(zones: zones);

        var act = () => vm.InitializeAsync("Hero", "missing-zone");
        await act.Should().NotThrowAsync();
    }

    // ── OnPlayerEntered ───────────────────────────────────────────────────────

    [Fact]
    public void OnPlayerEntered_Should_Add_Player_To_OnlinePlayers()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("Gandalf");
        vm.OnlinePlayers.Should().Contain("Gandalf");
    }

    [Fact]
    public void OnPlayerEntered_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("Gandalf");
        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("Gandalf") && msg.Contains("entered"));
    }

    [Fact]
    public void OnPlayerEntered_Should_Not_Duplicate_Players()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("Gandalf");
        vm.OnPlayerEntered("Gandalf");
        vm.OnlinePlayers.Should().ContainSingle();
    }

    // ── OnPlayerLeft ──────────────────────────────────────────────────────────

    [Fact]
    public void OnPlayerLeft_Should_Remove_Player_From_OnlinePlayers()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("Legolas");
        vm.OnPlayerLeft("Legolas");
        vm.OnlinePlayers.Should().NotContain("Legolas");
    }

    [Fact]
    public void OnPlayerLeft_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("Legolas");
        vm.OnPlayerLeft("Legolas");
        vm.ActionLog.Should().Contain(msg => msg.Contains("Legolas") && msg.Contains("left"));
    }

    // ── SetOccupants ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SetOccupants_Should_Populate_OnlinePlayers_Excluding_Self()
    {
        var vm = MakeVm();
        await vm.InitializeAsync("Hero", "zone1");

        vm.SetOccupants(new[] { "Hero", "Alice", "Bob" });

        vm.OnlinePlayers.Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public void SetOccupants_Should_Replace_Existing_Players()
    {
        var vm = MakeVm();
        vm.OnPlayerEntered("OldPlayer");

        vm.SetOccupants(new[] { "Alice" });

        vm.OnlinePlayers.Should().NotContain("OldPlayer");
        vm.OnlinePlayers.Should().Contain("Alice");
    }

    // ── ActionLog capacity ────────────────────────────────────────────────────

    [Fact]
    public void ActionLog_Should_Not_Exceed_100_Entries()
    {
        var vm = MakeVm();
        for (int i = 0; i < 110; i++)
            vm.OnPlayerEntered($"Player{i}");

        vm.ActionLog.Count.Should().BeLessThanOrEqualTo(100);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutCommand_Should_Navigate_To_MainMenu()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.LogoutCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(MainMenuViewModel));
    }

    [Fact]
    public async Task LogoutCommand_Should_Disconnect()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.LogoutCommand.Execute();

        conn.State.Should().Be(ConnectionState.Disconnected);
    }

    // ── StatusMessage property ────────────────────────────────────────────────

    [Fact]
    public void StatusMessage_Should_DefaultToEmpty()
    {
        var vm = MakeVm();
        vm.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void StatusMessage_Should_RaisePropertyChanged_When_Set()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.StatusMessage = "Reconnecting...";

        vm.StatusMessage.Should().Be("Reconnecting...");
        changes.Should().Contain(nameof(GameViewModel.StatusMessage));
    }

    // ── RestAtLocationCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task RestAtLocationCommand_Should_Send_RestAtLocation_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);
        await vm.InitializeAsync("Hero", "inn-millhaven");

        await vm.RestAtLocationCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "RestAtLocation");
    }

    [Fact]
    public async Task RestAtLocationCommand_Should_Send_Current_ZoneId()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);
        await vm.InitializeAsync("Hero", "inn-millhaven");

        await vm.RestAtLocationCommand.Execute();

        conn.SentCommands
            .Should().ContainSingle(c => c.Method == "RestAtLocation" && (string?)c.Arg == "inn-millhaven");
    }

    [Fact]
    public async Task RestAtLocationCommand_Should_Not_Throw_When_Hub_Returns_Null()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        Func<Task> act = async () => await vm.RestAtLocationCommand.Execute();
        await act.Should().NotThrowAsync();
    }

    // ── AllocateAttributePointsCommand ────────────────────────────────────────

    [Fact]
    public async Task AllocateAttributePointsCommand_Should_Send_AllocateAttributePoints_To_Hub()
    {
        var conn        = new FakeServerConnectionService();
        var vm          = MakeVm(conn: conn);
        var allocations = new Dictionary<string, int> { ["Strength"] = 2 };

        await vm.AllocateAttributePointsCommand.Execute(allocations);

        conn.SentCommands.Should().Contain(c => c.Method == "AllocateAttributePoints");
    }

    [Fact]
    public async Task AllocateAttributePointsCommand_Should_Send_Allocations_Dict()
    {
        var conn        = new FakeServerConnectionService();
        var vm          = MakeVm(conn: conn);
        var allocations = new Dictionary<string, int> { ["Strength"] = 1, ["Dexterity"] = 2 };

        await vm.AllocateAttributePointsCommand.Execute(allocations);

        var cmd  = conn.SentCommands.Should().ContainSingle(c => c.Method == "AllocateAttributePoints").Which;
        var dict = cmd.Arg.Should().BeOfType<Dictionary<string, int>>().Subject;
        dict["Strength"].Should().Be(1);
        dict["Dexterity"].Should().Be(2);
    }

    // ── OnAttributePointsAllocated ────────────────────────────────────────────

    [Fact]
    public void OnAttributePointsAllocated_Should_Update_UnspentAttributePoints()
    {
        var vm = MakeVm();
        vm.OnAttributePointsAllocated(remainingPoints: 3, newAttributes: []);

        vm.UnspentAttributePoints.Should().Be(3);
    }

    [Fact]
    public void OnAttributePointsAllocated_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnAttributePointsAllocated(remainingPoints: 5, newAttributes: []);

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("5"));
    }

    // ── OnCharacterRested ─────────────────────────────────────────────────────

    [Fact]
    public void OnCharacterRested_Should_Update_Health_Properties()
    {
        var vm = MakeVm();
        vm.OnCharacterRested(currentHealth: 80, maxHealth: 100, currentMana: 50, maxMana: 60, goldRemaining: 40);

        vm.CurrentHealth.Should().Be(80);
        vm.MaxHealth.Should().Be(100);
    }

    [Fact]
    public void OnCharacterRested_Should_Update_Mana_And_Gold_Properties()
    {
        var vm = MakeVm();
        vm.OnCharacterRested(currentHealth: 80, maxHealth: 100, currentMana: 50, maxMana: 60, goldRemaining: 40);

        vm.CurrentMana.Should().Be(50);
        vm.MaxMana.Should().Be(60);
        vm.Gold.Should().Be(40);
    }

    [Fact]
    public void OnCharacterRested_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnCharacterRested(currentHealth: 80, maxHealth: 100, currentMana: 50, maxMana: 60, goldRemaining: 40);

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("Rested"));
    }
}
