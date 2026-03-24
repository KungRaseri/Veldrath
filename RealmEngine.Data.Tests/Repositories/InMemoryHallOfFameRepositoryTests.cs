using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class InMemoryHallOfFameRepositoryTests
{
    private readonly InMemoryHallOfFameRepository _repository = new();

    private static HallOfFameEntry MakeEntry(string name, int level, int enemies, int quests, int playtime, int deaths = 0) =>
        new()
        {
            CharacterName = name,
            ClassName = "Warrior",
            Level = level,
            TotalEnemiesDefeated = enemies,
            QuestsCompleted = quests,
            PlayTimeMinutes = playtime,
            DeathCount = deaths
        };

    // AddEntry
    [Fact]
    public void AddEntry_CalculatesFameScore_OnInsert()
    {
        var entry = MakeEntry("Hero", 20, 100, 10, 60);
        _repository.AddEntry(entry);
        entry.FameScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddEntry_MultipleTimes_AccumulatesAll()
    {
        _repository.AddEntry(MakeEntry("A", 10, 50, 5, 30));
        _repository.AddEntry(MakeEntry("B", 15, 80, 8, 45));
        _repository.AddEntry(MakeEntry("C", 5, 20, 2, 15));
        _repository.GetAllEntries().Should().HaveCount(3);
    }

    // GetAllEntries
    [Fact]
    public void GetAllEntries_ReturnsSortedByFameScoreDescending()
    {
        var low = MakeEntry("Low", 1, 0, 0, 1);
        var high = MakeEntry("High", 50, 5000, 100, 3600);
        _repository.AddEntry(low);
        _repository.AddEntry(high);

        var entries = _repository.GetAllEntries();
        entries[0].FameScore.Should().BeGreaterThanOrEqualTo(entries[1].FameScore);
    }

    [Fact]
    public void GetAllEntries_DefaultLimit_Returns100Max()
    {
        for (var i = 0; i < 120; i++)
            _repository.AddEntry(MakeEntry($"Hero{i}", i % 50, i * 10, i, i * 2));

        _repository.GetAllEntries().Should().HaveCount(100);
    }

    [Fact]
    public void GetAllEntries_CustomLimit_RespectsLimit()
    {
        _repository.AddEntry(MakeEntry("A", 10, 50, 5, 30));
        _repository.AddEntry(MakeEntry("B", 20, 100, 10, 60));
        _repository.AddEntry(MakeEntry("C", 5, 20, 2, 15));

        _repository.GetAllEntries(limit: 2).Should().HaveCount(2);
    }

    [Fact]
    public void GetAllEntries_ReturnsEmpty_WhenNoEntries()
    {
        _repository.GetAllEntries().Should().BeEmpty();
    }

    // GetTopHeroes
    [Fact]
    public void GetTopHeroes_ReturnsTopNByFameScore()
    {
        for (var i = 0; i < 15; i++)
            _repository.AddEntry(MakeEntry($"Hero{i}", i + 1, i * 20, i, i * 3));

        var top = _repository.GetTopHeroes(5);
        top.Should().HaveCount(5);
        top.Should().BeInDescendingOrder(e => e.FameScore);
    }

    [Fact]
    public void GetTopHeroes_DefaultCount_Returns10Max()
    {
        for (var i = 0; i < 20; i++)
            _repository.AddEntry(MakeEntry($"Hero{i}", i + 1, i * 10, i, i * 2));

        _repository.GetTopHeroes().Should().HaveCount(10);
    }

    [Fact]
    public void GetTopHeroes_ReturnsAll_WhenFewerThanCount()
    {
        _repository.AddEntry(MakeEntry("Solo", 10, 50, 5, 30));
        _repository.GetTopHeroes(10).Should().HaveCount(1);
    }
}
