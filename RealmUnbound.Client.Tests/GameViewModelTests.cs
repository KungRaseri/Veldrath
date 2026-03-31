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

    // Initial state
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

    // InitializeAsync
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

    // OnPlayerEntered
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

    // OnPlayerLeft
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

    // SetOccupants
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

    // ActionLog capacity
    [Fact]
    public void ActionLog_Should_Not_Exceed_100_Entries()
    {
        var vm = MakeVm();
        for (int i = 0; i < 110; i++)
            vm.OnPlayerEntered($"Player{i}");

        vm.ActionLog.Count.Should().BeLessThanOrEqualTo(100);
    }

    // Logout
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

    // StatusMessage property
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

    // RestAtLocationCommand
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
            .Should().ContainSingle(c => c.Method == "RestAtLocation");
        var cmd = conn.SentCommands.Single(c => c.Method == "RestAtLocation");
        var locationId = (string?)cmd.Arg!.GetType().GetProperty("LocationId")?.GetValue(cmd.Arg);
        locationId.Should().Be("inn-millhaven");
    }

    [Fact]
    public async Task RestAtLocationCommand_Should_Not_Throw_When_Hub_Returns_Null()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        Func<Task> act = async () => await vm.RestAtLocationCommand.Execute();
        await act.Should().NotThrowAsync();
    }

    // AllocateAttributePointsCommand
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

    // OnAttributePointsAllocated
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

    // OnCharacterRested
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

    // UseAbilityCommand
    [Fact]
    public async Task UseAbilityCommand_Should_Send_UseAbility_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.UseAbilityCommand.Execute("fireball");

        conn.SentCommands.Should().Contain(c => c.Method == "UseAbility");
    }

    [Fact]
    public async Task UseAbilityCommand_Should_Send_AbilityId_As_Arg()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.UseAbilityCommand.Execute("minor_heal");

        conn.SentCommands
            .Should().ContainSingle(c => c.Method == "UseAbility" && (string?)c.Arg == "minor_heal");
    }

    // OnAbilityUsed
    [Fact]
    public void OnAbilityUsed_Should_Update_CurrentMana()
    {
        var vm = MakeVm();
        vm.OnAbilityUsed(abilityId: "fireball", remainingMana: 30, healthRestored: 0);

        vm.CurrentMana.Should().Be(30);
    }

    [Fact]
    public void OnAbilityUsed_Should_Not_Change_CurrentHealth_When_No_Healing()
    {
        var vm = MakeVm();
        vm.OnCharacterRested(currentHealth: 80, maxHealth: 100, currentMana: 50, maxMana: 60, goldRemaining: 0);

        vm.OnAbilityUsed(abilityId: "fireball", remainingMana: 40, healthRestored: 0);

        vm.CurrentHealth.Should().Be(80);
    }

    [Fact]
    public void OnAbilityUsed_Should_Increase_CurrentHealth_For_Healing_Ability()
    {
        var vm = MakeVm();
        vm.OnCharacterRested(currentHealth: 60, maxHealth: 100, currentMana: 50, maxMana: 60, goldRemaining: 0);

        vm.OnAbilityUsed(abilityId: "minor_heal", remainingMana: 40, healthRestored: 25);

        vm.CurrentHealth.Should().Be(85);
    }

    [Fact]
    public void OnAbilityUsed_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnAbilityUsed(abilityId: "fireball", remainingMana: 30, healthRestored: 0);

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("fireball"));
    }

    // AwardSkillXpCommand
    [Fact]
    public async Task AwardSkillXpCommand_Should_Send_AwardSkillXp_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.AwardSkillXpCommand.Execute(("herbalism", 40));

        conn.SentCommands.Should().Contain(c => c.Method == "AwardSkillXp");
    }

    // OnSkillXpGained
    [Fact]
    public void OnSkillXpGained_Should_Append_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnSkillXpGained(skillId: "herbalism", totalXp: 40, currentRank: 0, rankedUp: false);

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("herbalism"));
    }

    [Fact]
    public void OnSkillXpGained_Should_Show_RankUp_In_Log_When_RankedUp()
    {
        var vm = MakeVm();
        vm.OnSkillXpGained(skillId: "swordsmanship", totalXp: 100, currentRank: 1, rankedUp: true);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("swordsmanship") && msg.Contains("ranked up"));
    }

    [Fact]
    public void OnSkillXpGained_Should_Show_Xp_Total_When_Not_Ranked_Up()
    {
        var vm = MakeVm();
        vm.OnSkillXpGained(skillId: "herbalism", totalXp: 40, currentRank: 0, rankedUp: false);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("herbalism") && msg.Contains("40"));
    }

    // EquipItemCommand
    [Fact]
    public async Task EquipItemCommand_Should_Send_EquipItem_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.EquipItemCommand.Execute(("MainHand", "iron_sword"));

        conn.SentCommands.Should().Contain(c => c.Method == "EquipItem");
    }

    // OnItemEquipped
    [Fact]
    public void OnItemEquipped_Should_Append_Equip_Message_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnItemEquipped(slot: "MainHand", itemRef: "iron_sword");

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("iron_sword") && msg.Contains("MainHand"));
    }

    [Fact]
    public void OnItemEquipped_Should_Append_Unequip_Message_When_ItemRef_Is_Null()
    {
        var vm = MakeVm();
        vm.OnItemEquipped(slot: "MainHand", itemRef: null);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("MainHand") && msg.Contains("Unequipped"));
    }

    [Fact]
    public void OnItemEquipped_WithAllEquippedItems_Should_Sync_ItemRef_On_Each_Matching_Slot()
    {
        var vm       = MakeVm();
        var allItems = new Dictionary<string, string>
        {
            ["MainHand"] = "iron_sword",
            ["Head"]     = "iron_helm",
        };

        vm.OnItemEquipped(slot: "MainHand", itemRef: "iron_sword", allEquippedItems: allItems);

        vm.EquipmentSlots.Single(s => s.SlotName == "MainHand").ItemRef.Should().Be("iron_sword");
        vm.EquipmentSlots.Single(s => s.SlotName == "Head").ItemRef.Should().Be("iron_helm");
    }

    [Fact]
    public void OnItemEquipped_WithAllEquippedItems_Should_Clear_Slots_Not_Present_In_Map()
    {
        var vm   = MakeVm();
        var chest = vm.EquipmentSlots.Single(s => s.SlotName == "Chest");
        chest.ItemRef = "leather_chest";

        var allItems = new Dictionary<string, string>
        {
            ["MainHand"] = "iron_sword",
        };

        vm.OnItemEquipped(slot: "MainHand", itemRef: "iron_sword", allEquippedItems: allItems);

        chest.ItemRef.Should().BeNull();
    }

    [Fact]
    public void OnItemEquipped_WithAllEquippedItems_Should_Leave_Unmapped_Slots_With_Null_ItemIcon()
    {
        var vm       = MakeVm();
        var allItems = new Dictionary<string, string>
        {
            ["MainHand"] = "iron_sword",
        };

        vm.OnItemEquipped(slot: "MainHand", itemRef: "iron_sword", allEquippedItems: allItems);

        vm.EquipmentSlots.Single(s => s.SlotName == "Chest").ItemIcon.Should().BeNull();
    }

    // OnZoneLeft
    [Fact]
    public void OnZoneLeft_Should_Append_Zone_Departure_Message_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnZoneLeft();

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("zone") || msg.Contains("Zone"));
    }

    // AddGoldCommand
    [Fact]
    public async Task AddGoldCommand_Should_Send_AddGold_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.AddGoldCommand.Execute((100, "Loot"));

        conn.SentCommands.Should().Contain(c => c.Method == "AddGold");
    }

    // OnGoldChanged
    [Fact]
    public void OnGoldChanged_Should_Update_Gold_Property()
    {
        var vm = MakeVm();
        vm.OnGoldChanged(goldAdded: 50, newGoldTotal: 150);

        vm.Gold.Should().Be(150);
    }

    [Fact]
    public void OnGoldChanged_Should_Append_Gain_Message_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnGoldChanged(goldAdded: 50, newGoldTotal: 150);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("50") && msg.Contains("150"));
    }

    [Fact]
    public void OnGoldChanged_Should_Append_Spend_Message_When_Amount_Is_Negative()
    {
        var vm = MakeVm();
        vm.OnGoldChanged(goldAdded: -30, newGoldTotal: 70);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("Spent") && msg.Contains("30") && msg.Contains("70"));
    }

    // TakeDamageCommand
    [Fact]
    public async Task TakeDamageCommand_Should_Send_TakeDamage_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.TakeDamageCommand.Execute((25, "Trap"));

        conn.SentCommands.Should().Contain(c => c.Method == "TakeDamage");
    }

    // OnDamageTaken
    [Fact]
    public void OnDamageTaken_Should_Update_CurrentHealth_Property()
    {
        var vm = MakeVm();
        vm.OnDamageTaken(damageAmount: 30, currentHealth: 70, maxHealth: 100, isDead: false);

        vm.CurrentHealth.Should().Be(70);
    }

    [Fact]
    public void OnDamageTaken_Should_Append_Damage_Message_To_ActionLog()
    {
        var vm = MakeVm();
        vm.OnDamageTaken(damageAmount: 30, currentHealth: 70, maxHealth: 100, isDead: false);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("30") && msg.Contains("70") && msg.Contains("100"));
    }

    [Fact]
    public void OnDamageTaken_Should_Append_Died_Message_When_IsDead()
    {
        var vm = MakeVm();
        vm.OnDamageTaken(damageAmount: 50, currentHealth: 0, maxHealth: 100, isDead: true);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("died") || msg.Contains("Dead") || msg.Contains("0"));
    }

    // SeedInitialStats
    [Fact]
    public void SeedInitialStats_Should_Set_All_Properties()
    {
        var vm = MakeVm();
        vm.SeedInitialStats(new SeedInitialStatsArgs(
            Level: 5, Experience: 200L,
            CurrentHealth: 80, MaxHealth: 100,
            CurrentMana: 40, MaxMana: 50,
            Gold: 300, UnspentAttributePoints: 3));

        vm.Level.Should().Be(5);
        vm.Experience.Should().Be(200L);
        vm.CurrentHealth.Should().Be(80);
        vm.MaxHealth.Should().Be(100);
        vm.CurrentMana.Should().Be(40);
        vm.MaxMana.Should().Be(50);
        vm.Gold.Should().Be(300);
        vm.UnspentAttributePoints.Should().Be(3);
    }

    [Fact]
    public void SeedInitialStats_With_Zero_Values_Should_Not_Throw()
    {
        var vm = MakeVm();
        var act = () => vm.SeedInitialStats(new SeedInitialStatsArgs(0, 0L, 0, 0, 0, 0, 0, 0));
        act.Should().NotThrow();
    }

    // GainExperienceCommand
    [Fact]
    public async Task GainExperienceCommand_Should_Send_GainExperience_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.GainExperienceCommand.Execute((100, "Quest"));

        conn.SentCommands.Should().Contain(c => c.Method == "GainExperience");
    }

    // OnExperienceGained
    [Fact]
    public void OnExperienceGained_Should_Update_Level_Property()
    {
        var vm = MakeVm();
        vm.OnExperienceGained(newLevel: 3, newExperience: 50L, leveledUp: true, leveledUpTo: 3);

        vm.Level.Should().Be(3);
    }

    [Fact]
    public void OnExperienceGained_Should_Update_Experience_Property()
    {
        var vm = MakeVm();
        vm.OnExperienceGained(newLevel: 2, newExperience: 120L, leveledUp: false, leveledUpTo: null);

        vm.Experience.Should().Be(120L);
    }

    [Fact]
    public void OnExperienceGained_Should_Append_LevelUp_Message_When_LeveledUp()
    {
        var vm = MakeVm();
        vm.OnExperienceGained(newLevel: 4, newExperience: 10L, leveledUp: true, leveledUpTo: 4);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("4") && (msg.Contains("Level") || msg.Contains("level") || msg.Contains("Leveled")));
    }

    [Fact]
    public void OnExperienceGained_Should_Append_Xp_Message_When_Not_LeveledUp()
    {
        var vm = MakeVm();
        vm.OnExperienceGained(newLevel: 2, newExperience: 50L, leveledUp: false, leveledUpTo: null);

        vm.ActionLog.Should().ContainSingle(msg =>
            msg.Contains("2") && msg.Contains("50"));
    }

    // OnItemCrafted
    [Fact]
    public void OnItemCrafted_Should_Update_Gold_To_RemainingGold()
    {
        var vm = MakeVm();
        vm.OnItemCrafted("iron-sword", goldSpent: 50, remainingGold: 150);
        vm.Gold.Should().Be(150);
    }

    [Fact]
    public void OnItemCrafted_Should_Append_Log_With_Recipe_Name()
    {
        var vm = MakeVm();
        vm.OnItemCrafted("magic-staff", goldSpent: 50, remainingGold: 200);
        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("magic-staff"));
    }

    // OnDungeonEntered
    [Fact]
    public void OnDungeonEntered_Should_Append_Log_With_Dungeon_Id()
    {
        var vm = MakeVm();
        vm.OnDungeonEntered("dungeon-grotto", "dungeon-grotto");
        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("dungeon-grotto"));
    }

    [Fact]
    public void OnDungeonEntered_Should_Send_EnterZone_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);
        vm.OnDungeonEntered("dungeon-grotto", "dungeon-grotto");

        conn.SentCommands.Should().Contain(c => c.Method == "EnterZone");
    }

    [Fact]
    public async Task OnDungeonEntered_Should_Call_InitializeAsync()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("dungeon-grotto", "Grotto of Echoes",
                "A dark cave.", "Dungeon", 1, 10, false, 0)
        };
        var vm = MakeVm(zones: zones);
        vm.OnDungeonEntered("dungeon-grotto", "dungeon-grotto");
        await Task.Yield(); // allow fire-and-forget InitializeAsync to complete

        vm.ZoneName.Should().Be("Grotto of Echoes");
    }

    // CraftItemCommand
    [Fact]
    public async Task CraftItemCommand_Should_Send_CraftItem_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.CraftItemCommand.Execute("iron-sword");

        conn.SentCommands.Should().Contain(c => c.Method == "CraftItem");
    }

    // EnterDungeonCommand
    [Fact]
    public async Task EnterDungeonCommand_Should_Send_EnterDungeon_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.EnterDungeonCommand.Execute("dungeon-grotto");

        conn.SentCommands.Should().Contain(c => c.Method == "EnterDungeon");
    }

    // VisitShopCommand
    [Fact]
    public async Task VisitShopCommand_Should_Send_VisitShop_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.VisitShopCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "VisitShop");
    }

    [Fact]
    public void OnShopVisited_Should_Append_Welcome_Message_To_ActionLog()
    {
        var vm = MakeVm();

        vm.OnShopVisited("fenwick-crossing", "Fenwick's Crossing");

        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("Fenwick's Crossing"));
    }

    // IsLeftPanelOpen + ToggleLeftPanelCommand
    [Fact]
    public void IsLeftPanelOpen_Should_Default_To_True()
    {
        var vm = MakeVm();
        vm.IsLeftPanelOpen.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleLeftPanelCommand_Should_Collapse_Panel_When_Open()
    {
        var vm = MakeVm();
        await vm.ToggleLeftPanelCommand.Execute();
        vm.IsLeftPanelOpen.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleLeftPanelCommand_Should_Expand_Panel_When_Collapsed()
    {
        var vm = MakeVm();
        await vm.ToggleLeftPanelCommand.Execute(); // collapse
        await vm.ToggleLeftPanelCommand.Execute(); // expand
        vm.IsLeftPanelOpen.Should().BeTrue();
    }

    [Fact]
    public void LeftPanelToggleIcon_Should_Be_Left_Arrow_When_Open()
    {
        var vm = MakeVm();
        vm.LeftPanelToggleIcon.Should().Be("◀");
    }

    [Fact]
    public async Task LeftPanelToggleIcon_Should_Be_Right_Arrow_When_Collapsed()
    {
        var vm = MakeVm();
        await vm.ToggleLeftPanelCommand.Execute();
        vm.LeftPanelToggleIcon.Should().Be("▶");
    }

    [Fact]
    public async Task ToggleLeftPanelCommand_Should_RaisePropertyChanged_For_IsLeftPanelOpen()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        await vm.ToggleLeftPanelCommand.Execute();

        changes.Should().Contain(nameof(GameViewModel.IsLeftPanelOpen));
        changes.Should().Contain(nameof(GameViewModel.LeftPanelToggleIcon));
    }

    // Zone context flags (HasInn, HasMerchant, ZoneType)
    [Fact]
    public void HasInn_Should_Default_To_False()
    {
        var vm = MakeVm();
        vm.HasInn.Should().BeFalse();
    }

    [Fact]
    public void HasMerchant_Should_Default_To_False()
    {
        var vm = MakeVm();
        vm.HasMerchant.Should().BeFalse();
    }

    [Fact]
    public void ZoneType_Should_Default_To_Empty()
    {
        var vm = MakeVm();
        vm.ZoneType.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_ZoneType_From_Service()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick's Crossing",
                "A cozy starting town.", "Town", 0, 50, true, 0,
                RegionId: "thornveil", HasInn: true, HasMerchant: true)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "fenwick-crossing");

        vm.ZoneType.Should().Be("Town");
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_HasInn_True_For_Inn_Zone()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick's Crossing",
                "A cozy starting town.", "Town", 0, 50, true, 0,
                RegionId: "thornveil", HasInn: true, HasMerchant: false)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "fenwick-crossing");

        vm.HasInn.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_HasInn_False_For_Wilderness_Zone()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("greenveil-paths", "Greenveil Paths",
                "A forest path.", "Wilderness", 1, 50, false, 0)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "greenveil-paths");

        vm.HasInn.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_Should_Set_HasMerchant_True_For_Merchant_Zone()
    {
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick's Crossing",
                "A cozy starting town.", "Town", 0, 50, true, 0,
                RegionId: "thornveil", HasInn: false, HasMerchant: true)
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "fenwick-crossing");

        vm.HasMerchant.Should().BeTrue();
    }

    // HasUnspentPoints
    [Fact]
    public void HasUnspentPoints_Should_Be_False_When_Zero()
    {
        var vm = MakeVm();
        vm.UnspentAttributePoints = 0;
        vm.HasUnspentPoints.Should().BeFalse();
    }

    [Fact]
    public void HasUnspentPoints_Should_Be_True_When_Points_Available()
    {
        var vm = MakeVm();
        vm.UnspentAttributePoints = 3;
        vm.HasUnspentPoints.Should().BeTrue();
    }

    [Fact]
    public void HasUnspentPoints_Should_RaisePropertyChanged_When_UnspentPoints_Changes()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.UnspentAttributePoints = 2;

        changes.Should().Contain(nameof(GameViewModel.HasUnspentPoints));
    }

    // ExperienceToNextLevel
    [Fact]
    public void ExperienceToNextLevel_Should_Scale_With_Level()
    {
        var vm = MakeVm();
        vm.SeedInitialStats(new SeedInitialStatsArgs(Level: 5, Experience: 0, CurrentHealth: 50, MaxHealth: 50,
                            CurrentMana: 25, MaxMana: 25, Gold: 0, UnspentAttributePoints: 0));

        vm.ExperienceToNextLevel.Should().Be(500L); // 5 * 100
    }

    [Fact]
    public void ExperienceToNextLevel_Should_Be_At_Least_One_At_Level_Zero()
    {
        var vm = MakeVm();
        vm.SeedInitialStats(new SeedInitialStatsArgs(Level: 0, Experience: 0, CurrentHealth: 10, MaxHealth: 10,
                            CurrentMana: 5, MaxMana: 5, Gold: 0, UnspentAttributePoints: 0));

        vm.ExperienceToNextLevel.Should().BeGreaterThanOrEqualTo(1L);
    }

    [Fact]
    public void ExperienceToNextLevel_Should_RaisePropertyChanged_When_Level_Changes()
    {
        var vm      = MakeVm();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.Level = 10;

        changes.Should().Contain(nameof(GameViewModel.ExperienceToNextLevel));
    }

    // Dev commands
    [Fact]
    public async Task DevGainXpCommand_Should_Send_GainExperience_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.DevGainXpCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "GainExperience");
    }

    [Fact]
    public async Task DevAddGoldCommand_Should_Send_AddGold_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.DevAddGoldCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "AddGold");
    }

    [Fact]
    public async Task DevTakeDamageCommand_Should_Send_TakeDamage_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.DevTakeDamageCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "TakeDamage");
    }

    // Zone view mode
    [Fact]
    public void ZoneViewMode_Should_Default_To_Zone()
    {
        var vm = MakeVm();
        vm.ZoneViewMode.Should().Be("Zone");
        vm.IsZoneViewActive.Should().BeTrue();
        vm.IsRegionViewActive.Should().BeFalse();
        vm.IsWorldViewActive.Should().BeFalse();
    }

    [Fact]
    public async Task ShowRegionViewCommand_Should_Set_ZoneViewMode_To_Region()
    {
        var vm = MakeVm();
        await vm.ShowRegionViewCommand.Execute();
        vm.ZoneViewMode.Should().Be("Region");
        vm.IsRegionViewActive.Should().BeTrue();
        vm.IsZoneViewActive.Should().BeFalse();
    }

    [Fact]
    public async Task ShowWorldViewCommand_Should_Set_ZoneViewMode_To_World()
    {
        var vm = MakeVm();
        await vm.ShowWorldViewCommand.Execute();
        vm.ZoneViewMode.Should().Be("World");
        vm.IsWorldViewActive.Should().BeTrue();
        vm.IsZoneViewActive.Should().BeFalse();
    }

    [Fact]
    public async Task ShowZoneViewCommand_Should_Return_To_Zone_View()
    {
        var vm = MakeVm();
        await vm.ShowRegionViewCommand.Execute();
        await vm.ShowZoneViewCommand.Execute();
        vm.ZoneViewMode.Should().Be("Zone");
        vm.IsZoneViewActive.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_Reset_ZoneViewMode_To_Zone()
    {
        var vm = MakeVm();
        await vm.ShowRegionViewCommand.Execute();

        await vm.InitializeAsync("Hero", "any-zone");

        vm.ZoneViewMode.Should().Be("Zone");
    }

    // World context loading
    [Fact]
    public async Task InitializeAsync_Should_Populate_RegionZones_And_Mark_Current()
    {
        var thornveil = new RegionDto("thornveil", "Thornveil", "A dark forest.", "Forest", 0, 6, true, "draveth");
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                "Town", 1, 50, true, 0, RegionId: "thornveil", HasInn: true, HasMerchant: true),
            Regions = [thornveil],
            Zones =
            [
                new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                    "Town", 1, 50, true, 0, "thornveil"),
                new ZoneDto("greenveil-paths", "The Greenveil Paths", "Winding paths.",
                    "Wilderness", 1, 30, false, 0, "thornveil"),
            ],
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "fenwick-crossing");

        vm.RegionZones.Should().HaveCount(2);
        vm.RegionZones.Should().Contain(z => z.Id == "fenwick-crossing" && z.IsCurrentZone);
        vm.RegionZones.Should().Contain(z => z.Id == "greenveil-paths" && !z.IsCurrentZone);
        vm.RegionName.Should().Be("Thornveil");
    }

    [Fact]
    public async Task InitializeAsync_Should_Populate_WorldRegions_And_Mark_Current_Region()
    {
        var thornveil = new RegionDto("thornveil", "Thornveil", "A dark forest.", "Forest", 0, 6, true, "draveth");
        var greymoor  = new RegionDto("greymoor",  "Greymoor",  "A bleak moor.", "Highland", 5, 14, false, "draveth");
        var world     = new WorldDto("draveth", "Draveth", "A world of embers.", "The Age of Embers");
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                "Town", 1, 50, true, 0, RegionId: "thornveil"),
            Regions = [thornveil, greymoor],
            Worlds  = [world],
        };
        var vm = MakeVm(zones: zones);

        await vm.InitializeAsync("Hero", "fenwick-crossing");

        vm.WorldRegions.Should().HaveCount(2);
        vm.WorldRegions.Should().Contain(r => r.Id == "thornveil" && r.IsCurrentRegion);
        vm.WorldRegions.Should().Contain(r => r.Id == "greymoor"  && !r.IsCurrentRegion);
        vm.WorldName.Should().Be("Draveth");
        vm.WorldEra.Should().Be("The Age of Embers");
    }

    // Zone travel
    [Fact]
    public async Task TravelToZoneCommand_Should_Send_EnterZone_Hub_Command()
    {
        var conn  = new FakeServerConnectionService();
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("aldenmere", "Aldenmere", "A highland town.",
                "Town", 5, 50, false, 0),
        };
        var vm = MakeVm(conn: conn, zones: zones);

        await vm.TravelToZoneCommand.Execute("aldenmere");

        conn.SentCommands.Should().Contain(c => c.Method == "EnterZone");
    }

    [Fact]
    public async Task TravelToZoneCommand_Should_Skip_Hub_Call_When_Already_In_Zone()
    {
        var conn  = new FakeServerConnectionService();
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                "Town", 1, 50, true, 0),
        };
        var vm = MakeVm(conn: conn, zones: zones);
        await vm.InitializeAsync("Hero", "fenwick-crossing");
        conn.SentCommands.Clear();

        await vm.TravelToZoneCommand.Execute("fenwick-crossing");

        conn.SentCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task RegionZones_TravelCommand_Should_Be_Null_For_Current_Zone_Only()
    {
        var thornveil = new RegionDto("thornveil", "Thornveil", "A dark forest.", "Forest", 0, 6, true, "draveth");
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                "Town", 1, 50, true, 0, RegionId: "thornveil"),
            Regions = [thornveil],
            Zones =
            [
                new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                    "Town", 1, 50, true, 0, "thornveil"),
                new ZoneDto("greenveil-paths", "The Greenveil Paths", "Winding paths.",
                    "Wilderness", 1, 30, false, 0, "thornveil"),
            ],
        };
        var vm = MakeVm(zones: zones);
        await vm.InitializeAsync("Hero", "fenwick-crossing");

        var current = vm.RegionZones.Single(z => z.IsCurrentZone);
        var other   = vm.RegionZones.Single(z => !z.IsCurrentZone);

        current.TravelCommand.Should().BeNull();
        current.CanTravel.Should().BeFalse();
        other.TravelCommand.Should().NotBeNull();
        other.CanTravel.Should().BeTrue();
    }

    [Fact]
    public async Task ViewRegionCommand_Should_Load_Region_Data_And_Switch_To_Region_View()
    {
        var greymoor = new RegionDto("greymoor", "Greymoor", "A bleak moor.", "Highland", 5, 14, false, "draveth");
        var zones = new FakeZoneService
        {
            Regions = [greymoor],
            Zones =
            [
                new ZoneDto("pale-moor", "The Pale Moor", "A desolate plain.",
                    "Wilderness", 7, 30, false, 0, "greymoor"),
            ],
        };
        var vm = MakeVm(zones: zones);

        await vm.ViewRegionCommand.Execute("greymoor");

        vm.ZoneViewMode.Should().Be("Region");
        vm.RegionName.Should().Be("Greymoor");
        vm.RegionZones.Should().Contain(z => z.Id == "pale-moor");
    }

    [Fact]
    public async Task ShowRegionViewCommand_Should_Restore_Current_Region_After_Drill_Into_Other()
    {
        var thornveil = new RegionDto("thornveil", "Thornveil", "A dark forest.",     "Forest",   0, 6,  true,  "draveth");
        var greymoor  = new RegionDto("greymoor",  "Greymoor",  "A bleak moor.",      "Highland", 5, 14, false, "draveth");
        var zones = new FakeZoneService
        {
            ZoneToReturn = new ZoneDto("fenwick-crossing", "Fenwick Crossing", "The starting town.",
                "Town", 1, 50, true, 0, RegionId: "thornveil"),
            Regions = [thornveil, greymoor],
        };
        var vm = MakeVm(zones: zones);
        await vm.InitializeAsync("Hero", "fenwick-crossing");

        // Drill into a different region from the world view
        await vm.ViewRegionCommand.Execute("greymoor");
        vm.RegionName.Should().Be("Greymoor");

        // Clicking the Region tab should restore the player's actual region
        await vm.ShowRegionViewCommand.Execute();
        vm.RegionName.Should().Be("Thornveil");
    }

    // ToggleInventoryCommand
    [Fact]
    public async Task ToggleInventoryCommand_When_Closed_Should_Send_GetInventory_To_Hub()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);

        await vm.ToggleInventoryCommand.Execute();

        conn.SentCommands.Should().Contain(c => c.Method == "GetInventory");
    }

    [Fact]
    public async Task ToggleInventoryCommand_When_Open_Should_Close_Panel_Without_Server_Call()
    {
        var conn = new FakeServerConnectionService();
        var vm   = MakeVm(conn: conn);
        vm.OnInventoryLoaded([]); // sets IsInventoryOpen = true

        await vm.ToggleInventoryCommand.Execute();

        vm.IsInventoryOpen.Should().BeFalse();
        conn.SentCommands.Should().NotContain(c => c.Method == "GetInventory");
    }

    // OnInventoryLoaded
    [Fact]
    public void OnInventoryLoaded_Should_Set_IsInventoryOpen_True()
    {
        var vm = MakeVm();

        vm.OnInventoryLoaded([]);

        vm.IsInventoryOpen.Should().BeTrue();
    }

    [Fact]
    public void OnInventoryLoaded_Should_Populate_InventoryItems()
    {
        var vm = MakeVm();

        vm.OnInventoryLoaded([
            new InventoryItemEntry("iron_sword", 1, 80),
            new InventoryItemEntry("health_potion", 3, null)
        ]);

        vm.InventoryItems.Should().HaveCount(2);
        vm.InventoryItems[0].ItemRef.Should().Be("iron_sword");
        vm.InventoryItems[1].ItemRef.Should().Be("health_potion");
    }

    [Fact]
    public void OnInventoryLoaded_Should_Clear_Previous_Items_Before_Populating()
    {
        var vm = MakeVm();
        vm.OnInventoryLoaded([new InventoryItemEntry("old_item", 1, null)]);

        vm.OnInventoryLoaded([new InventoryItemEntry("new_item", 1, null)]);

        vm.InventoryItems.Should().ContainSingle(i => i.ItemRef == "new_item");
    }

    // OnShopVisited (new state)
    [Fact]
    public void OnShopVisited_Should_Set_IsShopOpen_True()
    {
        var vm = MakeVm();

        vm.OnShopVisited("fenwick-crossing", "Fenwick's Crossing");

        vm.IsShopOpen.Should().BeTrue();
    }

    [Fact]
    public void OnShopVisited_Should_Set_ShopZoneName()
    {
        var vm = MakeVm();

        vm.OnShopVisited("fenwick-crossing", "Fenwick's Crossing");

        vm.ShopZoneName.Should().Be("Fenwick's Crossing");
    }

    // CloseShopCommand
    [Fact]
    public async Task CloseShopCommand_Should_Set_IsShopOpen_False()
    {
        var vm = MakeVm();
        vm.OnShopVisited("fenwick-crossing", "Fenwick's Crossing"); // open it

        await vm.CloseShopCommand.Execute();

        vm.IsShopOpen.Should().BeFalse();
    }
}