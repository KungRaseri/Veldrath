using FluentAssertions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Victory.Services;
using RealmEngine.Shared.Models;
using Moq;

namespace RealmEngine.Core.Tests.Features.Victory.Services;

[Trait("Category", "Feature")]
public class NewGamePlusServiceTests
{
    private readonly Mock<ISaveGameService> _mockSaveGameService;
    private readonly NewGamePlusService _ngPlusService;

    public NewGamePlusServiceTests()
    {
        _mockSaveGameService = new Mock<ISaveGameService>();
        _ngPlusService = new NewGamePlusService(_mockSaveGameService.Object, NullLogger<NewGamePlusService>.Instance);
    }

    #region StartNewGamePlusAsync Tests

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Fail_When_No_Save_Game()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.NewSave.Should().BeNull();
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Fail_When_Game_Not_Completed()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool>()
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.NewSave.Should().BeNull();
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Fail_When_GameCompleted_Flag_Is_False()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool>
            {
                { "GameCompleted", false }
            }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.NewSave.Should().BeNull();
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Create_NG_Plus_Save_Successfully()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            PlayerName = "HeroPlayer",
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Warrior",
                Level = 50,
                MaxHealth = 500,
                MaxMana = 300,
                Strength = 30,
                Intelligence = 20,
                Dexterity = 25
            },
            DifficultyLevel = "Hard",
            UnlockedAchievements = new List<string> { "ach1", "ach2", "ach3" },
            GameFlags = new Dictionary<string, bool>
            {
                { "GameCompleted", true }
            }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.NewSave.Should().NotBeNull();
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Reset_Character_To_Level_1()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Mage",
                Level = 99
            },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.Level.Should().Be(1);
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Grant_HP_Bonus()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Warrior",
                MaxHealth = 500
            },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.MaxHealth.Should().Be(550); // 500 + 50 bonus
        result.NewSave.Character.Health.Should().Be(550);
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Grant_Mana_Bonus()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Mage",
                MaxMana = 400
            },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.MaxMana.Should().Be(450); // 400 + 50 bonus
        result.NewSave.Character.Mana.Should().Be(450);
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Grant_Stat_Bonuses()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Assassin",
                Strength = 30,
                Intelligence = 25,
                Dexterity = 40
            },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.Strength.Should().Be(35); // 30 + 5
        result.NewSave.Character.Intelligence.Should().Be(30); // 25 + 5
        result.NewSave.Character.Dexterity.Should().Be(45); // 40 + 5
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Grant_Starting_Gold()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character { Name = "Hero", ClassName = "Rogue" },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.Gold.Should().Be(500);
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Carry_Over_Achievements()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            UnlockedAchievements = new List<string> { "ach1", "ach2", "ach3", "ach4", "ach5" },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.UnlockedAchievements.Should().HaveCount(5);
        result.NewSave.UnlockedAchievements.Should().Contain("ach1");
        result.NewSave.UnlockedAchievements.Should().Contain("ach5");
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Append_NG_Plus_To_Player_Name()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            PlayerName = "TestPlayer",
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.PlayerName.Should().Be("TestPlayer (NG+)");
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Append_NG_Plus_To_Difficulty()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            DifficultyLevel = "Apocalypse",
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.DifficultyLevel.Should().Be("Apocalypse NG+");
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Set_NG_Plus_Flags()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.GameFlags.Should().ContainKey("IsNewGamePlus");
        result.NewSave.GameFlags["IsNewGamePlus"].Should().BeTrue();
        result.NewSave.GameFlags.Should().ContainKey("NewGamePlusGeneration");
        result.NewSave.GameFlags["NewGamePlusGeneration"].Should().BeTrue();
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Save_The_New_Game()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        _mockSaveGameService.Verify(s => s.SaveGame(It.Is<SaveGame>(sg =>
            sg.GameFlags.ContainsKey("IsNewGamePlus") &&
            sg.GameFlags["IsNewGamePlus"] == true
        )), Times.Once);
    }

    [Fact]
    public async Task StartNewGamePlusAsync_Should_Preserve_Character_Name()
    {
        // Arrange
        var completedSave = new SaveGame
        {
            Character = new Character
            {
                Name = "OriginalHeroName",
                ClassName = "Paladin"
            },
            GameFlags = new Dictionary<string, bool> { { "GameCompleted", true } }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(completedSave);

        // Act
        var result = await _ngPlusService.StartNewGamePlusAsync();

        // Assert
        result.NewSave!.Character.Name.Should().Be("OriginalHeroName");
        result.NewSave.Character.ClassName.Should().Be("Paladin");
    }

    #endregion
}
