using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreSaveGameRepositoryTests : IDisposable
{
    private readonly GameDbContext _db;
    private readonly EfCoreSaveGameRepository _repository;

    public EfCoreSaveGameRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GameDbContext(options);
        _repository = new EfCoreSaveGameRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static SaveGame MakeSave(string id, string playerName = "Hero", int slot = 1, DateTime? saveDate = null) =>
        new()
        {
            Id = id,
            PlayerName = playerName,
            SaveDate = saveDate ?? DateTime.UtcNow,
            Character = new Character { Name = playerName }
        };

    // ── SaveGame / GetById ────────────────────────────────────────────────

    [Fact]
    public void SaveGame_PersistsRecord_RetrievableById()
    {
        var save = MakeSave("sg-1", "Alice");
        _repository.SaveGame(save);
        var loaded = _repository.GetById("sg-1");
        loaded.Should().NotBeNull();
        loaded!.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public void SaveGame_Update_OverwritesExistingRecord()
    {
        var original = MakeSave("sg-upd", "Alice");
        _repository.SaveGame(original);

        var updated = MakeSave("sg-upd", "Bob");
        _repository.SaveGame(updated);

        _repository.GetById("sg-upd")!.PlayerName.Should().Be("Bob");
        _db.SaveGames.Count().Should().Be(1); // only one record
    }

    [Fact]
    public void GetById_ReturnsNull_WhenMissing()
    {
        _repository.GetById("no-such").Should().BeNull();
    }

    // ── LoadGame ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadGame_FindsBySlotIndex()
    {
        // SaveGame doesn't set SlotIndex on the record automatically — the caller
        // must store the record with the correct SlotIndex. We insert directly.
        _db.SaveGames.Add(new SaveGameRecord
        {
            Id = "sg-slot",
            PlayerName = "SlotHero",
            SlotIndex = 3,
            SaveDate = DateTime.UtcNow,
            DataJson = """{"Id":"sg-slot","PlayerName":"SlotHero","SaveDate":"2026-01-01T00:00:00Z","CreationDate":"2026-01-01T00:00:00Z","PlayTimeMinutes":0,"GameVersion":"1.0.0","Character":{"Name":"SlotHero"}}"""
        });
        _db.SaveChanges();

        var result = _repository.LoadGame(3);
        result.Should().NotBeNull();
        result!.PlayerName.Should().Be("SlotHero");
    }

    [Fact]
    public void LoadGame_ReturnsNull_WhenSlotNotFound()
    {
        _repository.LoadGame(99).Should().BeNull();
    }

    // ── GetMostRecent ─────────────────────────────────────────────────────

    [Fact]
    public void GetMostRecent_ReturnsLatestBySaveDate()
    {
        _repository.SaveGame(MakeSave("old", saveDate: new DateTime(2024, 1, 1)));
        _repository.SaveGame(MakeSave("new", saveDate: new DateTime(2026, 6, 1)));
        _repository.SaveGame(MakeSave("mid", saveDate: new DateTime(2025, 6, 1)));

        _repository.GetMostRecent()!.Id.Should().Be("new");
    }

    [Fact]
    public void GetMostRecent_ReturnsNull_WhenEmpty()
    {
        _repository.GetMostRecent().Should().BeNull();
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllSaves()
    {
        _repository.SaveGame(MakeSave("a1"));
        _repository.SaveGame(MakeSave("a2"));
        _repository.SaveGame(MakeSave("a3"));
        _repository.GetAll().Should().HaveCount(3);
    }

    // ── GetByPlayerName ───────────────────────────────────────────────────

    [Fact]
    public void GetByPlayerName_FiltersCorrectly()
    {
        _repository.SaveGame(MakeSave("p1", "Alice"));
        _repository.SaveGame(MakeSave("p2", "Bob"));
        _repository.SaveGame(MakeSave("p3", "Alice"));

        var results = _repository.GetByPlayerName("Alice");
        results.Should().HaveCount(2).And.OnlyContain(s => s.PlayerName == "Alice");
    }

    [Fact]
    public void GetByPlayerName_ReturnsEmpty_WhenNoMatch()
    {
        _repository.SaveGame(MakeSave("p4", "Bob"));
        _repository.GetByPlayerName("NoOne").Should().BeEmpty();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesRecord_ReturnsTrue()
    {
        _repository.SaveGame(MakeSave("del-1"));
        _repository.Delete("del-1").Should().BeTrue();
        _repository.GetById("del-1").Should().BeNull();
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenNotFound()
    {
        _repository.Delete("ghost").Should().BeFalse();
    }

    // ── DeleteSave ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteSave_RemovesBySlot_ReturnsTrue()
    {
        _db.SaveGames.Add(new SaveGameRecord
        {
            Id = "sg-dslot",
            PlayerName = "Hero",
            SlotIndex = 5,
            SaveDate = DateTime.UtcNow,
            DataJson = "{}"
        });
        _db.SaveChanges();

        _repository.DeleteSave(5).Should().BeTrue();
        _db.SaveGames.Should().BeEmpty();
    }

    [Fact]
    public void DeleteSave_ReturnsFalse_WhenSlotNotFound()
    {
        _repository.DeleteSave(42).Should().BeFalse();
    }

    // ── SaveExists ────────────────────────────────────────────────────────

    [Fact]
    public void SaveExists_ReturnsTrue_WhenSlotExists()
    {
        _db.SaveGames.Add(new SaveGameRecord { Id = "ex-1", PlayerName = "H", SlotIndex = 2 });
        _db.SaveChanges();
        _repository.SaveExists(2).Should().BeTrue();
    }

    [Fact]
    public void SaveExists_ReturnsFalse_WhenSlotMissing()
    {
        _repository.SaveExists(99).Should().BeFalse();
    }

    // ── JSON round-trip ───────────────────────────────────────────────────

    [Fact]
    public void SaveGame_SerializesCharacterName_RoundTrip()
    {
        var save = MakeSave("rt-1", "Gandalf");
        save.Character.Name = "Gandalf";
        save.Character.Level = 20;
        _repository.SaveGame(save);

        var loaded = _repository.GetById("rt-1")!;
        loaded.Character.Name.Should().Be("Gandalf");
        loaded.Character.Level.Should().Be(20);
    }
}
