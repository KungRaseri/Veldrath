using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using QuestModel = RealmEngine.Shared.Models.Quest;

namespace RealmEngine.Core.Tests.Features.SaveLoad;

/// <summary>
/// World-state tests for <see cref="SaveGameService"/>: persistence, quest bookkeeping, NPC and
/// location tracking, statistics, death handling and every guard branch of the service.
/// </summary>
[Trait("Category", "Service")]
public class SaveGameServiceWorldStateTests
{
    private sealed class FakeSaveGameRepository : ISaveGameRepository
    {
        public List<SaveGame> Saves { get; } = [];

        public Exception? ThrowOnSave { get; set; }
        public Exception? ThrowOnGetById { get; set; }
        public Exception? ThrowOnGetAll { get; set; }
        public Exception? ThrowOnGetMostRecent { get; set; }
        public Exception? ThrowOnDelete { get; set; }
        public bool DeleteResult { get; set; } = true;

        public List<string> SavedIds { get; } = [];
        public List<string> DeletedIds { get; } = [];

        public void SaveGame(SaveGame saveGame)
        {
            if (ThrowOnSave is not null) throw ThrowOnSave;
            SavedIds.Add(saveGame.Id);
            Saves.RemoveAll(s => s.Id == saveGame.Id);
            Saves.Add(saveGame);
        }

        public SaveGame? LoadGame(int slot) => null;

        public SaveGame? GetById(string id)
        {
            if (ThrowOnGetById is not null) throw ThrowOnGetById;
            return Saves.FirstOrDefault(s => s.Id == id);
        }

        public SaveGame? GetMostRecent()
        {
            if (ThrowOnGetMostRecent is not null) throw ThrowOnGetMostRecent;
            return Saves.OrderByDescending(s => s.SaveDate).FirstOrDefault();
        }

        public List<SaveGame> GetAll()
        {
            if (ThrowOnGetAll is not null) throw ThrowOnGetAll;
            return Saves.ToList();
        }

        public List<SaveGame> GetByPlayerName(string playerName) =>
            Saves.Where(s => s.PlayerName == playerName).ToList();

        public bool Delete(string id)
        {
            if (ThrowOnDelete is not null) throw ThrowOnDelete;
            DeletedIds.Add(id);
            Saves.RemoveAll(s => s.Id == id);
            return DeleteResult;
        }

        public bool DeleteSave(int slot) => false;

        public bool SaveExists(int slot) => false;
    }

    private sealed class FakeApocalypseTimer : IApocalypseTimer
    {
        public int BonusMinutes { get; private set; }
        public List<(int Minutes, string Reason)> Bonuses { get; } = [];

        public void Start() { }
        public void StartFromSave(DateTime startTime, int bonusMinutes) => BonusMinutes = bonusMinutes;
        public void Pause() { }
        public void Resume() { }
        public int GetRemainingMinutes() => 0;
        public bool IsExpired() => false;

        public BonusTimeResult AddBonusTime(int minutes, string reason = "Quest completed")
        {
            Bonuses.Add((minutes, reason));
            BonusMinutes += minutes;
            return new BonusTimeResult { MinutesAdded = minutes, Reason = reason, TotalRemainingMinutes = BonusMinutes };
        }

        public string GetFormattedTimeRemaining() => "0:00";
        public string GetColoredTimeDisplay() => "0:00";
        public TimeWarningResult? CheckTimeWarnings() => null;
        public int GetTotalTimeLimit() => 0;
        public int GetElapsedMinutes() => 0;
        public int GetBonusMinutes() => BonusMinutes;
    }

    private readonly FakeSaveGameRepository _repository = new();
    private readonly FakeApocalypseTimer _timer = new();

    private SaveGameService CreateService() =>
        new(_repository, _timer, NullLogger<SaveGameService>.Instance);

    private static SaveGame CreateSave(string id = "save-1", string player = "Hero", string difficulty = "Normal") =>
        new()
        {
            Id = id,
            PlayerName = player,
            DifficultyLevel = difficulty,
            Character = new Character { Name = player, Level = 3 },
            SaveDate = DateTime.Now
        };

    // ── CreateNewGame / difficulty ─────────────────────────────────────────────

    [Fact]
    public void CreateNewGame_Should_Copy_Difficulty_Flags_Onto_The_Save()
    {
        // Arrange
        var service = CreateService();
        var character = new Character { Name = "Aria" };
        var difficulty = new DifficultySettings
        {
            Name = "Apocalypse",
            AutoSaveOnly = true,
            IsPermadeath = true,
            IsApocalypse = true
        };

        // Act
        var save = service.CreateNewGame(character, difficulty);

        // Assert
        save.PlayerName.Should().Be("Aria");
        save.Character.Should().BeSameAs(character);
        save.DifficultyLevel.Should().Be("Apocalypse");
        save.IronmanMode.Should().BeTrue();
        save.PermadeathMode.Should().BeTrue();
        save.ApocalypseMode.Should().BeTrue();
        save.ApocalypseStartTime.Should().NotBeNull();
        save.ApocalypseBonusMinutes.Should().Be(0);
        save.PlayTimeMinutes.Should().Be(0);
        service.GetCurrentSave().Should().BeSameAs(save);
    }

    [Fact]
    public void CreateNewGame_WithoutApocalypse_Should_Leave_The_Start_Time_Null()
    {
        // Arrange
        var service = CreateService();

        // Act
        var save = service.CreateNewGame(new Character { Name = "Aria" }, new DifficultySettings { Name = "Easy" });

        // Assert
        save.ApocalypseMode.Should().BeFalse();
        save.ApocalypseStartTime.Should().BeNull();
    }

    [Fact]
    public void GetDifficultySettings_Without_A_Current_Save_Returns_Normal()
    {
        // Arrange
        var service = CreateService();

        // Act
        var difficulty = service.GetDifficultySettings();

        // Assert
        difficulty.Name.Should().Be("Normal");
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Hard")]
    [InlineData("Expert")]
    [InlineData("Ironman")]
    [InlineData("Permadeath")]
    [InlineData("Apocalypse")]
    [InlineData("Homebrew")]
    public void GetDifficultySettings_Looks_The_Name_Up_From_The_Current_Save(string name)
    {
        // Arrange
        var service = CreateService();
        service.SetCurrentSave(CreateSave(difficulty: name));

        // Act
        var difficulty = service.GetDifficultySettings();

        // Assert — an unknown name falls back to Normal.
        difficulty.Name.Should().Be(name == "Homebrew" ? "Normal" : name);
    }

    // ── SaveGame ───────────────────────────────────────────────────────────────

    [Fact]
    public void SaveGame_Should_Round_Trip_Through_The_Repository_And_Become_Current()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        save.Character.Inventory.Add(new Item { Name = "Sword" });
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Quest" });

        // Act
        service.SaveGame(save);

        // Assert
        _repository.SavedIds.Should().ContainSingle().Which.Should().Be("save-1");
        service.GetCurrentSave().Should().BeSameAs(save);
        save.Character.Inventory.Should().ContainSingle();
        save.ActiveQuests.Should().ContainSingle();
    }

    [Fact]
    public void SaveGame_Should_Copy_Every_Collection_Before_Persisting()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        save.KnownNPCs.Add(new NPC { Id = "npc-1", Name = "Innkeeper" });
        save.VisitedLocations.Add("village");
        save.DiscoveredLocations.Add("cave");
        save.LegendaryEnemiesDefeated.Add(new Enemy { Name = "Wyrm" });
        save.UnlockedAchievements.Add("first-blood");
        save.NPCRelationships["npc-1"] = 5;
        save.EnemiesDefeatedByType["orc"] = 2;
        save.GameFlags["met-king"] = true;
        save.DroppedItemsAtLocations["village"] = [new Item { Name = "Old Boot" }];

        var npcList = save.KnownNPCs;
        var flagMap = save.GameFlags;
        var dropsMap = save.DroppedItemsAtLocations;

        // Act
        service.SaveGame(save);

        // Assert — the service replaces the collections with copies.
        save.KnownNPCs.Should().NotBeSameAs(npcList);
        save.GameFlags.Should().NotBeSameAs(flagMap);
        save.DroppedItemsAtLocations.Should().NotBeSameAs(dropsMap);
        save.NPCRelationships.Should().ContainKey("npc-1");
        save.EnemiesDefeatedByType["orc"].Should().Be(2);
        save.GameFlags["met-king"].Should().BeTrue();
        save.DroppedItemsAtLocations["village"].Should().ContainSingle();
    }

    [Fact]
    public void SaveGame_Should_Refresh_The_Apocalypse_Bonus_From_The_Timer()
    {
        // Arrange
        var service = CreateService();
        _timer.AddBonusTime(25, "test");
        var save = CreateSave();
        save.ApocalypseMode = true;

        // Act
        service.SaveGame(save);

        // Assert
        save.ApocalypseBonusMinutes.Should().Be(25);
    }

    [Fact]
    public void SaveGame_Should_Rethrow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnSave = new InvalidOperationException("disk full");
        var save = CreateSave();

        // Act
        var act = () => service.SaveGame(save);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("disk full");
        service.GetCurrentSave().Should().BeNull();
    }

    [Fact]
    public void SaveGame_Legacy_Overload_Should_Create_A_New_Save_With_The_Given_Id()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Rook" };

        // Act
        service.SaveGame(player, [new Item { Name = "Dagger" }], saveId: "slot-a");

        // Assert
        _repository.SavedIds.Should().ContainSingle().Which.Should().Be("slot-a");
        service.GetCurrentSave()!.Id.Should().Be("slot-a");
        service.GetCurrentSave()!.PlayerName.Should().Be("Rook");
        service.GetCurrentSave()!.Character.Inventory.Should().ContainSingle();
    }

    [Fact]
    public void SaveGame_Legacy_Overload_Should_Reuse_The_Current_Save_When_Ids_Match()
    {
        // Arrange
        var service = CreateService();
        var existing = CreateSave("save-1");
        service.SetCurrentSave(existing);
        var player = new Character { Name = "Rook" };

        // Act
        service.SaveGame(player, [], saveId: "save-1");

        // Assert
        service.GetCurrentSave().Should().BeSameAs(existing);
        existing.Character.Should().BeSameAs(player);
    }

    [Fact]
    public void SaveGame_Legacy_Overload_Should_Keep_The_Existing_Inventory_When_Given_An_Empty_List()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Rook" };
        player.Inventory.Add(new Item { Name = "Torch" });

        // Act
        service.SaveGame(player, [], saveId: "slot-b");

        // Assert
        service.GetCurrentSave()!.Character.Inventory.Should().ContainSingle()
            .Which.Name.Should().Be("Torch");
    }

    // ── Load / list / delete ───────────────────────────────────────────────────

    [Fact]
    public void LoadGame_With_A_Known_Id_Should_Make_It_Current()
    {
        // Arrange
        var service = CreateService();
        _repository.Saves.Add(CreateSave("save-7", "Vale"));

        // Act
        var loaded = service.LoadGame("save-7");

        // Assert
        loaded.Should().NotBeNull();
        service.GetCurrentSave().Should().BeSameAs(loaded);
    }

    [Fact]
    public void LoadGame_With_An_Unknown_Id_Should_Leave_The_Current_Save_Untouched()
    {
        // Arrange
        var service = CreateService();
        var current = CreateSave("current");
        service.SetCurrentSave(current);

        // Act
        var loaded = service.LoadGame("missing");

        // Assert
        loaded.Should().BeNull();
        service.GetCurrentSave().Should().BeSameAs(current);
    }

    [Fact]
    public void LoadGame_Should_Rethrow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnGetById = new IOException("corrupt");

        // Act
        var act = () => service.LoadGame("save-1");

        // Assert
        act.Should().Throw<IOException>().WithMessage("corrupt");
    }

    [Fact]
    public void GetAllSaves_Should_Return_The_Most_Recent_First()
    {
        // Arrange
        var service = CreateService();
        var older = CreateSave("old");
        older.SaveDate = new DateTime(2020, 1, 1);
        var newer = CreateSave("new");
        newer.SaveDate = new DateTime(2026, 1, 1);
        _repository.Saves.AddRange([older, newer]);

        // Act
        var saves = service.GetAllSaves();

        // Assert
        saves.Select(s => s.Id).Should().Equal("new", "old");
    }

    [Fact]
    public void GetAllSaves_Should_Rethrow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnGetAll = new TimeoutException("slow");

        // Act
        var act = () => service.GetAllSaves();

        // Assert
        act.Should().Throw<TimeoutException>();
    }

    [Fact]
    public void DeleteSave_Should_Clear_The_Current_Save_When_It_Matches()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave("save-1");
        _repository.Saves.Add(save);
        service.SetCurrentSave(save);

        // Act
        var deleted = service.DeleteSave("save-1");

        // Assert
        deleted.Should().BeTrue();
        _repository.DeletedIds.Should().ContainSingle();
        service.GetCurrentSave().Should().BeNull();
    }

    [Fact]
    public void DeleteSave_Should_Keep_The_Current_Save_When_The_Repository_Refuses()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave("save-1");
        service.SetCurrentSave(save);
        _repository.DeleteResult = false;

        // Act
        var deleted = service.DeleteSave("save-1");

        // Assert
        deleted.Should().BeFalse();
        service.GetCurrentSave().Should().BeSameAs(save);
    }

    [Fact]
    public void DeleteSave_Should_Rethrow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnDelete = new UnauthorizedAccessException("locked");

        // Act
        var act = () => service.DeleteSave("save-1");

        // Assert
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetMostRecentSave_Should_Return_The_Latest()
    {
        // Arrange
        var service = CreateService();
        var older = CreateSave("old");
        older.SaveDate = new DateTime(2021, 5, 5);
        var newer = CreateSave("new");
        newer.SaveDate = new DateTime(2026, 5, 5);
        _repository.Saves.AddRange([older, newer]);

        // Act
        var mostRecent = service.GetMostRecentSave();

        // Assert
        mostRecent!.Id.Should().Be("new");
    }

    [Fact]
    public void GetMostRecentSave_Should_Rethrow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnGetMostRecent = new InvalidOperationException("boom");

        // Act
        var act = () => service.GetMostRecentSave();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HasAnySaves_Should_Reflect_The_Repository_Contents()
    {
        // Arrange
        var service = CreateService();

        // Act / Assert
        service.HasAnySaves().Should().BeFalse();
        _repository.Saves.Add(CreateSave());
        service.HasAnySaves().Should().BeTrue();
    }

    [Fact]
    public void HasAnySaves_Should_Return_False_When_The_Repository_Throws()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnGetAll = new InvalidOperationException("down");

        // Act
        var result = service.HasAnySaves();

        // Assert
        result.Should().BeFalse();
    }

    // ── AutoSave ───────────────────────────────────────────────────────────────

    [Fact]
    public void AutoSave_Should_Persist_The_Given_Save()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();

        // Act
        service.AutoSave(save);

        // Assert
        _repository.SavedIds.Should().ContainSingle();
        service.GetCurrentSave().Should().BeSameAs(save);
    }

    [Fact]
    public void AutoSave_Should_Swallow_Repository_Failures()
    {
        // Arrange
        var service = CreateService();
        _repository.ThrowOnSave = new IOException("disk gone");

        // Act
        var act = () => service.AutoSave(CreateSave());

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AutoSave_Legacy_Overload_Should_Reuse_The_Current_Save()
    {
        // Arrange
        var service = CreateService();
        var current = CreateSave("save-1");
        service.SetCurrentSave(current);
        var player = new Character { Name = "Hero" };

        // Act
        service.AutoSave(player, [new Item { Name = "Torch" }]);

        // Assert
        _repository.SavedIds.Should().ContainSingle().Which.Should().Be("save-1");
        current.Character.Should().BeSameAs(player);
    }

    [Fact]
    public void AutoSave_Legacy_Overload_Should_Reuse_An_Existing_Save_For_The_Player()
    {
        // Arrange
        var service = CreateService();
        _repository.Saves.Add(CreateSave("existing", "Rook"));

        // Act
        service.AutoSave(new Character { Name = "Rook" }, []);

        // Assert
        _repository.SavedIds.Should().ContainSingle().Which.Should().Be("existing");
    }

    [Fact]
    public void AutoSave_Legacy_Overload_Should_Create_A_Save_When_None_Exists_For_The_Player()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.AutoSave(new Character { Name = "Brand-New" }, []);

        // Assert
        service.GetCurrentSave().Should().NotBeNull();
        service.GetCurrentSave()!.PlayerName.Should().Be("Brand-New");
        service.GetCurrentSave()!.Id.Should().NotBeNullOrWhiteSpace();
    }

    // ── Death ──────────────────────────────────────────────────────────────────

    [Fact]
    public void RecordDeath_Without_A_Save_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.RecordDeath("forest", "wolf");

        // Assert
        _repository.SavedIds.Should().BeEmpty();
    }

    [Fact]
    public void RecordDeath_Should_Increment_The_Counter_And_Set_The_Death_Flag()
    {
        // Arrange
        var service = CreateService();
        service.SetCurrentSave(CreateSave());

        // Act
        service.RecordDeath("forest", "wolf");

        // Assert
        service.GetCurrentSave()!.DeathCount.Should().Be(1);
        service.GetCurrentSave()!.GameFlags.Should().ContainKey("deaths");
    }

    [Fact]
    public void RecordDeath_In_Ironman_Mode_Should_Auto_Save()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        save.IronmanMode = true;
        service.SetCurrentSave(save);

        // Act
        service.RecordDeath("forest", "wolf");

        // Assert
        _repository.SavedIds.Should().ContainSingle();
    }

    [Fact]
    public void RecordDeath_Legacy_Overload_Should_Delete_The_Save_In_Ironman_Mode()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave("save-1");
        save.IronmanMode = true;
        _repository.Saves.Add(save);
        service.SetCurrentSave(save);

        // Act
        service.RecordDeath();

        // Assert
        save.DeathCount.Should().Be(1);
        _repository.DeletedIds.Should().ContainSingle().Which.Should().Be("save-1");
        service.GetCurrentSave().Should().BeNull();
    }

    [Fact]
    public void RecordDeath_Legacy_Overload_Should_Keep_The_Save_Outside_Ironman_Mode()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.RecordDeath();

        // Assert
        save.DeathCount.Should().Be(1);
        _repository.DeletedIds.Should().BeEmpty();
    }

    // ── Quests ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddAvailableQuest_Should_Add_Once()
    {
        // Arrange
        var service = CreateService();
        service.SetCurrentSave(CreateSave());
        var quest = new QuestModel { Id = "q1", Title = "Find the Well" };

        // Act
        service.AddAvailableQuest(quest);
        service.AddAvailableQuest(quest);

        // Assert
        service.GetCurrentSave()!.AvailableQuests.Should().ContainSingle();
    }

    [Fact]
    public void AddAvailableQuest_Without_A_Save_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.AddAvailableQuest(new QuestModel { Id = "q1", Title = "Find the Well" });

        // Assert
        service.GetCurrentSave().Should().BeNull();
    }

    [Fact]
    public void AcceptQuest_Should_Move_The_Quest_Into_Active()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        service.AddAvailableQuest(new QuestModel { Id = "q1", Title = "Find the Well" });

        // Act
        service.AcceptQuest("q1");

        // Assert
        save.AvailableQuests.Should().BeEmpty();
        save.ActiveQuests.Should().ContainSingle()
            .Which.IsActive.Should().BeTrue();
        save.ActiveQuests[0].StartTime.Should().NotBeNull();
    }

    [Fact]
    public void AcceptQuest_With_An_Unknown_Id_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.AcceptQuest("nope");

        // Assert
        save.ActiveQuests.Should().BeEmpty();
    }

    [Fact]
    public void CompleteQuest_Should_Move_The_Quest_Into_Completed_And_Count_It()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Find the Well", IsActive = true });

        // Act
        service.CompleteQuest("q1");

        // Assert
        save.ActiveQuests.Should().BeEmpty();
        save.CompletedQuests.Should().ContainSingle()
            .Which.IsCompleted.Should().BeTrue();
        save.CompletedQuests[0].IsActive.Should().BeFalse();
        save.QuestsCompleted.Should().Be(1);
    }

    [Theory]
    [InlineData("easy", 10)]
    [InlineData("medium", 20)]
    [InlineData("hard", 30)]
    [InlineData("epic", 15)]
    public void CompleteQuest_In_Apocalypse_Mode_Should_Award_Bonus_Time(string difficulty, int expectedMinutes)
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        save.ApocalypseMode = true;
        service.SetCurrentSave(save);
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Find the Well", Difficulty = difficulty });

        // Act
        service.CompleteQuest("q1");

        // Assert
        _timer.Bonuses.Should().ContainSingle()
            .Which.Minutes.Should().Be(expectedMinutes);
        save.ApocalypseBonusMinutes.Should().Be(expectedMinutes);
        _repository.SavedIds.Should().ContainSingle();
    }

    [Fact]
    public void CompleteQuest_With_An_Unknown_Id_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.CompleteQuest("nope");

        // Assert
        save.CompletedQuests.Should().BeEmpty();
        save.QuestsCompleted.Should().Be(0);
    }

    [Fact]
    public void FailQuest_Should_Move_The_Quest_Into_Failed_And_Count_It()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Find the Well", IsActive = true });

        // Act
        service.FailQuest("q1", "timed out");

        // Assert
        save.ActiveQuests.Should().BeEmpty();
        save.FailedQuests.Should().ContainSingle()
            .Which.IsActive.Should().BeFalse();
        save.QuestsFailed.Should().Be(1);
    }

    [Fact]
    public void FailQuest_With_An_Unknown_Id_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.FailQuest("nope");

        // Assert
        save.FailedQuests.Should().BeEmpty();
    }

    [Fact]
    public void UpdateQuestProgress_Should_Record_Progress_Below_The_Quantity()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Find the Well", Quantity = 5 });

        // Act
        service.UpdateQuestProgress("q1", 3);

        // Assert
        save.ActiveQuests[0].Progress.Should().Be(3);
        save.CompletedQuests.Should().BeEmpty();
    }

    [Fact]
    public void UpdateQuestProgress_Should_Auto_Complete_At_The_Quantity()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        save.ActiveQuests.Add(new QuestModel { Id = "q1", Title = "Find the Well", Quantity = 5 });

        // Act
        service.UpdateQuestProgress("q1", 5);

        // Assert
        save.ActiveQuests.Should().BeEmpty();
        save.CompletedQuests.Should().ContainSingle();
    }

    [Fact]
    public void UpdateQuestProgress_With_An_Unknown_Id_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.UpdateQuestProgress("nope", 9);

        // Assert
        save.ActiveQuests.Should().BeEmpty();
    }

    // ── NPCs and locations ────────────────────────────────────────────────────

    [Fact]
    public void MeetNPC_Should_Register_The_Npc_And_A_Neutral_Relationship()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.MeetNPC(new NPC { Id = "npc-1", Name = "Innkeeper", Occupation = "Innkeeper" });
        service.MeetNPC(new NPC { Id = "npc-1", Name = "Innkeeper" });

        // Assert
        save.KnownNPCs.Should().ContainSingle();
        save.NPCRelationships.Should().ContainKey("npc-1").WhoseValue.Should().Be(0);
    }

    [Fact]
    public void ModifyNPCRelationship_Should_Clamp_Between_Minus_And_Plus_Hundred()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        service.MeetNPC(new NPC { Id = "npc-1", Name = "Innkeeper" });

        // Act
        service.ModifyNPCRelationship("npc-1", 500);
        var afterBoost = save.NPCRelationships["npc-1"];
        service.ModifyNPCRelationship("npc-1", -500);
        var afterDrop = save.NPCRelationships["npc-1"];

        // Assert
        afterBoost.Should().Be(100);
        afterDrop.Should().Be(-100);
    }

    [Fact]
    public void ModifyNPCRelationship_With_An_Unknown_Npc_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.ModifyNPCRelationship("ghost", 10);

        // Assert
        save.NPCRelationships.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverLocation_Should_Skip_Locations_Already_Known_Or_Visited()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        save.VisitedLocations.Add("village");

        // Act
        service.DiscoverLocation("cave");
        service.DiscoverLocation("cave");
        service.DiscoverLocation("village");

        // Assert
        save.DiscoveredLocations.Should().Equal("cave");
    }

    [Fact]
    public void VisitLocation_Should_Move_It_Out_Of_Discovered()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        service.DiscoverLocation("cave");

        // Act
        service.VisitLocation("cave");
        service.VisitLocation("cave");

        // Assert
        save.VisitedLocations.Should().Equal("cave");
        save.DiscoveredLocations.Should().BeEmpty();
    }

    // ── Statistics and flags ──────────────────────────────────────────────────

    [Fact]
    public void RecordEnemyDefeat_Should_Track_Totals_By_Type()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.RecordEnemyDefeat(new Enemy { Name = "Orc", Type = EnemyType.Humanoid });
        service.RecordEnemyDefeat(new Enemy { Name = "Orc", Type = EnemyType.Humanoid });
        service.RecordEnemyDefeat(new Enemy { Name = "Bat", Type = EnemyType.Beast });

        // Assert
        save.TotalEnemiesDefeated.Should().Be(3);
        save.EnemiesDefeatedByType["humanoid"].Should().Be(2);
        save.EnemiesDefeatedByType["beast"].Should().Be(1);
    }

    [Fact]
    public void RecordEnemyDefeat_Should_Collect_Legendary_Enemies()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);
        var dragon = new Enemy { Name = "Ash Wyrm" };
        dragon.Traits["legendary"] = new TraitValue(true, TraitType.Boolean);

        // Act
        service.RecordEnemyDefeat(dragon);
        service.RecordEnemyDefeat(new Enemy { Name = "Goblin" });

        // Assert
        save.LegendaryEnemiesDefeated.Should().ContainSingle()
            .Which.Name.Should().Be("Ash Wyrm");
    }

    [Fact]
    public void RecordEnemyDefeat_Without_A_Save_Should_Do_Nothing()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.RecordEnemyDefeat(new Enemy { Name = "Orc" });

        // Assert
        service.GetCurrentSave().Should().BeNull();
    }

    [Fact]
    public void RecordGold_Should_Accumulate_Earned_And_Spent()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.RecordGoldEarned(120);
        service.RecordGoldSpent(45);

        // Assert
        save.TotalGoldEarned.Should().Be(120);
        save.TotalGoldSpent.Should().Be(45);
    }

    [Fact]
    public void UnlockAchievement_Should_Add_Once()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.UnlockAchievement("first-blood");
        service.UnlockAchievement("first-blood");

        // Assert
        save.UnlockedAchievements.Should().Equal("first-blood");
    }

    [Fact]
    public void GameFlags_Should_Round_Trip()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();
        service.SetCurrentSave(save);

        // Act
        service.SetGameFlag("met-king", true);

        // Assert
        service.GetGameFlag("met-king").Should().BeTrue();
        service.GetGameFlag("never-set").Should().BeFalse();
    }

    [Fact]
    public void GetGameFlag_Should_Be_False_When_A_Flag_Is_Explicitly_False_Or_There_Is_No_Save()
    {
        // Arrange
        var service = CreateService();

        // Act
        var withoutSave = service.GetGameFlag("met-king");

        var save = CreateSave();
        service.SetCurrentSave(save);
        service.SetGameFlag("met-king", false);
        var explicitlyFalse = service.GetGameFlag("met-king");

        // Assert
        withoutSave.Should().BeFalse();
        explicitlyFalse.Should().BeFalse();
    }

    [Fact]
    public void SetCurrentSave_Should_Become_The_Current_Save()
    {
        // Arrange
        var service = CreateService();
        var save = CreateSave();

        // Act
        service.SetCurrentSave(save);

        // Assert
        service.GetCurrentSave().Should().BeSameAs(save);
    }

    [Fact]
    public void Dispose_Should_Not_Throw()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
