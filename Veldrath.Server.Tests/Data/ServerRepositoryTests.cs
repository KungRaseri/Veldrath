using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;
using Serilog;

namespace Veldrath.Server.Tests.Data;

public class ServerHallOfFameRepositoryTests : IDisposable
{
    private readonly TestGameDbContextFactory _factory = new();

    public ServerHallOfFameRepositoryTests()
    {
        // Silence Serilog during tests
        Log.Logger = new LoggerConfiguration().CreateLogger();
    }

    public void Dispose() => _factory.Dispose();

    private static HallOfFameEntry MakeEntry(string name = "Hero", int level = 10) =>
        new()
        {
            CharacterName       = name,
            ClassName           = "Warrior",
            Level               = level,
            PlayTimeMinutes     = 120,
            TotalEnemiesDefeated = 50,
        };

    // AddEntry
    [Fact]
    public void AddEntry_Should_Persist_Entry()
    {
        using var db   = _factory.CreateContext();
        var repo       = new ServerHallOfFameRepository(db);
        repo.AddEntry(MakeEntry("Thorin", level: 20));

        var entries = db.HallOfFameEntries.ToList();
        entries.Should().ContainSingle(e => e.CharacterName == "Thorin");
    }

    [Fact]
    public void AddEntry_Should_Calculate_Fame_Score()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        var entry    = MakeEntry("Legolas", level: 30);
        repo.AddEntry(entry);

        var saved = db.HallOfFameEntries.First(e => e.CharacterName == "Legolas");
        saved.FameScore.Should().BeGreaterThan(0);
    }

    // GetAllEntries
    [Fact]
    public void GetAllEntries_Should_Return_Entries_Ordered_By_Fame_Score_Descending()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        repo.AddEntry(MakeEntry("Low",  level: 1));
        repo.AddEntry(MakeEntry("High", level: 50));

        var entries = repo.GetAllEntries();

        entries[0].FameScore.Should().BeGreaterThanOrEqualTo(entries[1].FameScore);
    }

    [Fact]
    public void GetAllEntries_Should_Respect_Limit()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        for (int i = 0; i < 5; i++)
            repo.AddEntry(MakeEntry($"Hero{i}", level: i + 1));

        var entries = repo.GetAllEntries(limit: 3);
        entries.Should().HaveCount(3);
    }

    [Fact]
    public void GetAllEntries_Should_Return_Empty_When_No_Entries()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        repo.GetAllEntries().Should().BeEmpty();
    }

    // GetTopHeroes
    [Fact]
    public void GetTopHeroes_Should_Return_Top_N_By_Fame_Score()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        for (int i = 0; i < 15; i++)
            repo.AddEntry(MakeEntry($"Hero{i}", level: i + 1));

        var top = repo.GetTopHeroes(count: 5);
        top.Should().HaveCount(5);
    }

    [Fact]
    public void GetTopHeroes_Should_Return_First_Entry_With_Highest_Score()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        repo.AddEntry(MakeEntry("Weak",  level: 1));
        repo.AddEntry(MakeEntry("Elite", level: 99));

        var top = repo.GetTopHeroes(count: 1);
        top.Should().ContainSingle(e => e.CharacterName == "Elite");
    }

    // Error handling
    [Fact]
    public void AddEntry_Should_Swallow_Exception_When_Db_Fails()
    {
        var db   = _factory.CreateContext();
        var repo = new ServerHallOfFameRepository(db);
        // Dispose the EF context so subsequent DB calls raise ObjectDisposedException
        db.Dispose();

        // Should not throw — catch block swallows the exception
        var act = () => repo.AddEntry(MakeEntry());
        act.Should().NotThrow();
    }

    // Dispose
    [Fact]
    public void Dispose_Should_Not_Throw()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerHallOfFameRepository(db);
        var act      = () => repo.Dispose();
        act.Should().NotThrow();
    }
}

public class ServerSaveGameRepositoryTests : IDisposable
{
    private readonly TestGameDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static SaveGame MakeSaveGame(string playerName = "Player", int slot = 1) =>
        new()
        {
            Id         = Guid.NewGuid().ToString(),
            PlayerName = playerName,
        };

    private static SaveGameRecord SlotHack(SaveGame sg, int slot)
    {
        // The repo stores SlotIndex in SaveGameRecord but SaveGame doesn't expose it.
        // Work around by reading from the db after Insert to set the slot.
        // Actually SaveGame might have a Slot property - check the model.
        // We'll insert via the repo and update the record directly for slot tests.
        return new SaveGameRecord
        {
            Id         = sg.Id,
            PlayerName = sg.PlayerName,
            SlotIndex  = slot,
            DataJson   = Newtonsoft.Json.JsonConvert.SerializeObject(sg),
        };
    }

    // Save (new)
    [Fact]
    public void Save_Should_Persist_New_SaveGame()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame("Frodo");

        repo.Save(sg);

        db.SaveGames.Should().ContainSingle(r => r.PlayerName == "Frodo");
    }

    [Fact]
    public void Save_Should_Update_Existing_SaveGame()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame("Sam");
        repo.Save(sg);

        // Update and save again
        sg.PlayerName = "Samwise";
        repo.Save(sg);

        db.SaveGames.Should().ContainSingle();
        db.SaveGames.Single().PlayerName.Should().Be("Samwise");
    }

    // GetById
    [Fact]
    public void GetById_Should_Return_SaveGame_When_Found()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame("Bilbo");
        repo.Save(sg);

        var result = repo.GetById(sg.Id);
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetById_Should_Return_Null_When_Not_Found()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.GetById("nonexistent").Should().BeNull();
    }

    // GetMostRecent
    [Fact]
    public void GetMostRecent_Should_Return_Latest_Save()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var older    = MakeSaveGame("Old");
        var newer    = MakeSaveGame("New");

        // Insert records manually with different SaveDate to control ordering
        db.SaveGames.Add(new SaveGameRecord
        {
            Id = older.Id, PlayerName = "Old",
            SaveDate = DateTime.UtcNow.AddDays(-1),
            DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(older)
        });
        db.SaveGames.Add(new SaveGameRecord
        {
            Id = newer.Id, PlayerName = "New",
            SaveDate = DateTime.UtcNow,
            DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(newer)
        });
        db.SaveChanges();

        var result = repo.GetMostRecent();
        result.Should().NotBeNull();
        result!.Id.Should().Be(newer.Id);
    }

    [Fact]
    public void GetMostRecent_Should_Return_Null_When_No_Saves()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.GetMostRecent().Should().BeNull();
    }

    // GetAll / GetAllSaves
    [Fact]
    public void GetAll_Should_Return_All_Saves()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.Save(MakeSaveGame("A"));
        repo.Save(MakeSaveGame("B"));
        repo.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetAllSaves_Should_Alias_GetAll()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.Save(MakeSaveGame("X"));
        repo.GetAllSaves().Should().HaveCount(1);
    }

    // GetByPlayerName
    [Fact]
    public void GetByPlayerName_Should_Return_Saves_For_Player()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.Save(MakeSaveGame("Alice"));
        repo.Save(MakeSaveGame("Bob"));

        var result = repo.GetByPlayerName("Alice");
        result.Should().ContainSingle();
    }

    [Fact]
    public void GetByPlayerName_Should_Return_Empty_When_No_Match()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.GetByPlayerName("Ghost").Should().BeEmpty();
    }

    // Delete
    [Fact]
    public void Delete_Should_Return_True_And_Remove_Record()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame("Del");
        repo.Save(sg);

        var result = repo.Delete(sg.Id);

        result.Should().BeTrue();
        db.SaveGames.Should().BeEmpty();
    }

    [Fact]
    public void Delete_Should_Return_False_When_Not_Found()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.Delete("nonexistent").Should().BeFalse();
    }

    // LoadGame / DeleteSave / SaveExists
    [Fact]
    public void LoadGame_Should_Return_Save_For_Slot()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame("Slot1Player");

        db.SaveGames.Add(new SaveGameRecord
        {
            Id        = sg.Id,
            PlayerName = sg.PlayerName,
            SlotIndex = 2,
            DataJson  = Newtonsoft.Json.JsonConvert.SerializeObject(sg),
        });
        db.SaveChanges();

        var result = repo.LoadGame(2);
        result.Should().NotBeNull();
    }

    [Fact]
    public void LoadGame_Should_Return_Null_When_Slot_Empty()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.LoadGame(99).Should().BeNull();
    }

    [Fact]
    public void SaveGameMethod_Should_Alias_Save()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.SaveGame(MakeSaveGame("GameAlias"));
        db.SaveGames.Should().ContainSingle();
    }

    [Fact]
    public void DeleteSave_Should_Return_True_And_Remove_Slot()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame();

        db.SaveGames.Add(new SaveGameRecord
        {
            Id = sg.Id, PlayerName = sg.PlayerName, SlotIndex = 3,
            DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(sg),
        });
        db.SaveChanges();

        repo.DeleteSave(3).Should().BeTrue();
        db.SaveGames.Should().BeEmpty();
    }

    [Fact]
    public void DeleteSave_Should_Return_False_When_Slot_Not_Found()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.DeleteSave(99).Should().BeFalse();
    }

    [Fact]
    public void SaveExists_Should_Return_True_When_Slot_Has_Save()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var sg       = MakeSaveGame();

        db.SaveGames.Add(new SaveGameRecord
        {
            Id = sg.Id, PlayerName = sg.PlayerName, SlotIndex = 1,
            DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(sg),
        });
        db.SaveChanges();

        repo.SaveExists(1).Should().BeTrue();
    }

    [Fact]
    public void SaveExists_Should_Return_False_When_Slot_Empty()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        repo.SaveExists(99).Should().BeFalse();
    }

    // Dispose
    [Fact]
    public void Dispose_Should_Not_Throw()
    {
        using var db = _factory.CreateContext();
        var repo     = new ServerSaveGameRepository(db);
        var act      = () => repo.Dispose();
        act.Should().NotThrow();
    }
}
