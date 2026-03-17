using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class InMemorySaveGameRepositoryTests
{
    private readonly InMemorySaveGameRepository _repository = new();

    private static SaveGame MakeSave(string id, string playerName = "Hero", DateTime? saveDate = null) =>
        new() { Id = id, PlayerName = playerName, SaveDate = saveDate ?? DateTime.Now };

    // ── SaveGame / GetById ─────────────────────────────────────────────────

    [Fact]
    public void SaveGame_And_GetById_ShouldRoundTrip()
    {
        var save = MakeSave("save-1");
        _repository.SaveGame(save);
        _repository.GetById("save-1").Should().BeSameAs(save);
    }

    [Fact]
    public void SaveGame_Overwrites_ExistingEntry()
    {
        var original = MakeSave("save-1", "Alice");
        var updated = MakeSave("save-1", "Bob");
        _repository.SaveGame(original);
        _repository.SaveGame(updated);
        _repository.GetById("save-1")!.PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        _repository.GetById("missing").Should().BeNull();
    }

    // ── LoadGame ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadGame_BySlot_FindsSaveWhoseIdMatchesSlotString()
    {
        var save = MakeSave("3");
        _repository.SaveGame(save);
        _repository.LoadGame(3).Should().BeSameAs(save);
    }

    [Fact]
    public void LoadGame_ReturnsNull_WhenSlotDoesNotExist()
    {
        _repository.LoadGame(99).Should().BeNull();
    }

    // ── GetMostRecent ────────────────────────────────────────────────────

    [Fact]
    public void GetMostRecent_ReturnsLatestBySaveDate()
    {
        var old = MakeSave("a", saveDate: new DateTime(2025, 1, 1));
        var newest = MakeSave("b", saveDate: new DateTime(2026, 6, 1));
        var middle = MakeSave("c", saveDate: new DateTime(2025, 12, 1));
        _repository.SaveGame(old);
        _repository.SaveGame(newest);
        _repository.SaveGame(middle);
        _repository.GetMostRecent()!.Id.Should().Be("b");
    }

    [Fact]
    public void GetMostRecent_ReturnsNull_WhenEmpty()
    {
        _repository.GetMostRecent().Should().BeNull();
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllSavedGames()
    {
        _repository.SaveGame(MakeSave("x1"));
        _repository.SaveGame(MakeSave("x2"));
        _repository.SaveGame(MakeSave("x3"));
        _repository.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void GetAll_ReturnsEmptyList_WhenEmpty()
    {
        _repository.GetAll().Should().BeEmpty();
    }

    // ── GetByPlayerName ──────────────────────────────────────────────────

    [Fact]
    public void GetByPlayerName_ReturnsOnlyMatchingSaves()
    {
        _repository.SaveGame(MakeSave("1", "Alice"));
        _repository.SaveGame(MakeSave("2", "Bob"));
        _repository.SaveGame(MakeSave("3", "Alice"));

        var results = _repository.GetByPlayerName("Alice");
        results.Should().HaveCount(2).And.OnlyContain(s => s.PlayerName == "Alice");
    }

    [Fact]
    public void GetByPlayerName_ReturnsEmpty_WhenNoMatch()
    {
        _repository.SaveGame(MakeSave("1", "Bob"));
        _repository.GetByPlayerName("Alice").Should().BeEmpty();
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesEntry_AndReturnsTrue()
    {
        _repository.SaveGame(MakeSave("del-1"));
        _repository.Delete("del-1").Should().BeTrue();
        _repository.GetById("del-1").Should().BeNull();
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdNotFound()
    {
        _repository.Delete("no-such-id").Should().BeFalse();
    }

    // ── DeleteSave ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteSave_RemovesBySlotString_AndReturnsTrue()
    {
        _repository.SaveGame(MakeSave("5"));
        _repository.DeleteSave(5).Should().BeTrue();
        _repository.GetById("5").Should().BeNull();
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
        _repository.SaveGame(MakeSave("7"));
        _repository.SaveExists(7).Should().BeTrue();
    }

    [Fact]
    public void SaveExists_ReturnsFalse_WhenSlotDoesNotExist()
    {
        _repository.SaveExists(99).Should().BeFalse();
    }
}
