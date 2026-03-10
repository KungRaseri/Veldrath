using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Repositories;
using Moq;

namespace RealmEngine.Core.Tests.Features.Death.Services;

[Trait("Category", "Feature")]
/// <summary>
/// Comprehensive tests for HallOfFame persistence via <see cref="InMemoryHallOfFameRepository"/>.
/// </summary>
public class HallOfFameRepositoryTests : IDisposable
{
    private readonly Mock<IGameUI> _mockConsoleUI;
    private readonly InMemoryHallOfFameRepository _HallOfFameRepository;

    public HallOfFameRepositoryTests()
    {
        _mockConsoleUI = new Mock<IGameUI>();
        _HallOfFameRepository = new InMemoryHallOfFameRepository();
    }

    public void Dispose()
    {
        _HallOfFameRepository?.Dispose();
    }

    #region AddEntry Tests

    [Fact]
    public void AddEntry_Should_Add_Entry_To_Database()
    {
        // Arrange
        var entry = CreateTestEntry("Hero1", level: 10, isPermadeath: true);

        // Act
        _HallOfFameRepository.AddEntry(entry);

        // Assert
        var allEntries = _HallOfFameRepository.GetAllEntries();
        allEntries.Should().ContainSingle();
        allEntries[0].CharacterName.Should().Be("Hero1");
    }

    [Fact]
    public void AddEntry_Should_Add_Multiple_Entries()
    {
        // Arrange
        var entry1 = CreateTestEntry("Hero1", level: 10, isPermadeath: true);
        var entry2 = CreateTestEntry("Hero2", level: 20, isPermadeath: false);
        var entry3 = CreateTestEntry("Hero3", level: 5, isPermadeath: true);

        // Act
        _HallOfFameRepository.AddEntry(entry1);
        _HallOfFameRepository.AddEntry(entry2);
        _HallOfFameRepository.AddEntry(entry3);

        // Assert
        var allEntries = _HallOfFameRepository.GetAllEntries();
        allEntries.Should().HaveCount(3);
        allEntries.Should().Contain(e => e.CharacterName == "Hero1");
        allEntries.Should().Contain(e => e.CharacterName == "Hero2");
        allEntries.Should().Contain(e => e.CharacterName == "Hero3");
    }

    [Fact]
    public void AddEntry_Should_Persist_All_Properties()
    {
        // Arrange
        var entry = new HallOfFameEntry
        {
            CharacterName = "TestHero",
            ClassName = "Warrior",
            Level = 15,
            TotalEnemiesDefeated = 100,
            QuestsCompleted = 10,
            AchievementsUnlocked = 5,
            PlayTimeMinutes = 120,
            IsPermadeath = true,
            DeathDate = DateTime.Now,
            DifficultyLevel = "Apocalypse"
        };

        // Act
        _HallOfFameRepository.AddEntry(entry);

        // Assert
        var retrieved = _HallOfFameRepository.GetAllEntries().First();
        retrieved.CharacterName.Should().Be("TestHero");
        retrieved.ClassName.Should().Be("Warrior");
        retrieved.Level.Should().Be(15);
        retrieved.TotalEnemiesDefeated.Should().Be(100);
        retrieved.QuestsCompleted.Should().Be(10);
        retrieved.AchievementsUnlocked.Should().Be(5);
        retrieved.PlayTimeMinutes.Should().Be(120);
        retrieved.IsPermadeath.Should().BeTrue();
        retrieved.DifficultyLevel.Should().Be("Apocalypse");
    }

    #endregion

    #region GetAllEntries Tests

    [Fact]
    public void GetAllEntries_Should_Return_Empty_List_When_No_Entries()
    {
        // Act
        var entries = _HallOfFameRepository.GetAllEntries();

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public void GetAllEntries_Should_Return_All_Entries_Sorted_By_Fame_Score()
    {
        // Arrange
        var lowScore = CreateTestEntry("LowHero", level: 5, isPermadeath: false);
        var highScore = CreateTestEntry("HighHero", level: 20, isPermadeath: true);
        var midScore = CreateTestEntry("MidHero", level: 10, isPermadeath: false);

        _HallOfFameRepository.AddEntry(lowScore);
        _HallOfFameRepository.AddEntry(highScore);
        _HallOfFameRepository.AddEntry(midScore);

        // Act
        var entries = _HallOfFameRepository.GetAllEntries();

        // Assert
        entries.Should().HaveCount(3);
        entries[0].CharacterName.Should().Be("HighHero", "highest fame score should be first");
        entries[1].CharacterName.Should().Be("MidHero", "mid fame score should be second");
        entries[2].CharacterName.Should().Be("LowHero", "lowest fame score should be last");
    }

    [Fact]
    public void GetAllEntries_Should_Respect_Limit_Parameter()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            _HallOfFameRepository.AddEntry(CreateTestEntry($"Hero{i}", level: i + 1, isPermadeath: false));
        }

        // Act
        var entries = _HallOfFameRepository.GetAllEntries(limit: 5);

        // Assert
        entries.Should().HaveCount(5, "should only return requested limit");
    }

    [Fact]
    public void GetAllEntries_Should_Default_To_100_Limit()
    {
        // Arrange
        for (int i = 0; i < 150; i++)
        {
            _HallOfFameRepository.AddEntry(CreateTestEntry($"Hero{i}", level: i + 1, isPermadeath: false));
        }

        // Act
        var entries = _HallOfFameRepository.GetAllEntries();

        // Assert
        entries.Should().HaveCount(100, "default limit should be 100");
    }

    #endregion

    #region GetTopHeroes Tests

    [Fact]
    public void GetTopHeroes_Should_Return_Empty_List_When_No_Entries()
    {
        // Act
        var topHeroes = _HallOfFameRepository.GetTopHeroes();

        // Assert
        topHeroes.Should().BeEmpty();
    }

    [Fact]
    public void GetTopHeroes_Should_Return_Top_Entries_By_Fame_Score()
    {
        // Arrange
        for (int i = 1; i <= 20; i++)
        {
            _HallOfFameRepository.AddEntry(CreateTestEntry($"Hero{i}", level: i, isPermadeath: false));
        }

        // Act
        var topHeroes = _HallOfFameRepository.GetTopHeroes(count: 10);

        // Assert
        topHeroes.Should().HaveCount(10);
        topHeroes[0].Level.Should().Be(20, "highest level should be first");
        topHeroes[9].Level.Should().Be(11, "10th highest level should be last");
    }

    [Fact]
    public void GetTopHeroes_Should_Default_To_10_Count()
    {
        // Arrange
        for (int i = 1; i <= 20; i++)
        {
            _HallOfFameRepository.AddEntry(CreateTestEntry($"Hero{i}", level: i, isPermadeath: false));
        }

        // Act
        var topHeroes = _HallOfFameRepository.GetTopHeroes();

        // Assert
        topHeroes.Should().HaveCount(10, "default count should be 10");
    }

    [Fact]
    public void GetTopHeroes_Should_Handle_Request_For_More_Than_Available()
    {
        // Arrange
        _HallOfFameRepository.AddEntry(CreateTestEntry("Hero1", level: 10, isPermadeath: false));
        _HallOfFameRepository.AddEntry(CreateTestEntry("Hero2", level: 5, isPermadeath: false));

        // Act
        var topHeroes = _HallOfFameRepository.GetTopHeroes(count: 10);

        // Assert
        topHeroes.Should().HaveCount(2, "should return all available entries when requested count is higher");
    }

    #endregion

    #region Fame Score Tests

    [Fact]
    public void Entries_Should_Be_Ordered_By_Fame_Score_Calculation()
    {
        // Arrange - create entries with different stats that impact fame score
        var highQuestHero = CreateTestEntry("QuestHero", level: 10, isPermadeath: false, completedQuests: 50);
        var highEnemyHero = CreateTestEntry("SlayerHero", level: 15, isPermadeath: false, enemiesDefeated: 200);
        var lowStatsHero = CreateTestEntry("WeakHero", level: 5, isPermadeath: false, enemiesDefeated: 10);

        _HallOfFameRepository.AddEntry(highQuestHero);
        _HallOfFameRepository.AddEntry(highEnemyHero);
        _HallOfFameRepository.AddEntry(lowStatsHero);

        // Act
        var topHeroes = _HallOfFameRepository.GetTopHeroes();

        // Assert
        topHeroes.Should().HaveCount(3);
        // All should be ordered by their calculated fame scores
        for (int i = 0; i < topHeroes.Count - 1; i++)
        {
            topHeroes[i].FameScore.Should().BeGreaterThanOrEqualTo(topHeroes[i + 1].FameScore,
                $"entry at index {i} should have higher or equal fame score than entry at {i + 1}");
        }
    }

    [Fact]
    public void Permadeath_Entries_Should_Have_Higher_Fame_Score()
    {
        // Arrange - identical stats except permadeath
        var permadeathHero = CreateTestEntry("Permadeath", level: 10, isPermadeath: true);
        var normalHero = CreateTestEntry("Normal", level: 10, isPermadeath: false);

        // Calculate fame scores
        permadeathHero.CalculateFameScore();
        normalHero.CalculateFameScore();

        // Assert
        permadeathHero.FameScore.Should().BeGreaterThan(normalHero.FameScore,
            "permadeath should give bonus to fame score");
    }

    #endregion

    #region Helper Methods

    private HallOfFameEntry CreateTestEntry(
        string name,
        int level,
        bool isPermadeath,
        int enemiesDefeated = 10,
        int completedQuests = 5)
    {
        return new HallOfFameEntry
        {
            CharacterName = name,
            ClassName = "Warrior",
            Level = level,
            TotalEnemiesDefeated = enemiesDefeated,
            QuestsCompleted = completedQuests,
            AchievementsUnlocked = 1,
            PlayTimeMinutes = 60,
            IsPermadeath = isPermadeath,
            DeathDate = DateTime.Now,
            DifficultyLevel = "Normal"
        };
    }

    #endregion
}
