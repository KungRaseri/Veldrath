using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class QuestServiceTests
{
    private readonly Mock<ISaveGameService> _mockSaveGameService;
    private readonly Mock<MainQuestService> _mockMainQuestService;
    private readonly QuestService _service;

    public QuestServiceTests()
    {
        _mockSaveGameService = new Mock<ISaveGameService>();
        _mockMainQuestService = new Mock<MainQuestService>();
        var mockInitService = new Mock<QuestInitializationService>(_mockMainQuestService.Object, NullLogger<QuestInitializationService>.Instance);
        _service = new QuestService(_mockSaveGameService.Object, _mockMainQuestService.Object, mockInitService.Object, NullLogger<QuestService>.Instance);
    }

    [Fact]
    public async Task StartQuestAsync_Should_Fail_When_No_Active_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.StartQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No active save game");
    }

    [Fact]
    public async Task StartQuestAsync_Should_Fail_When_Quest_Already_Active()
    {
        // Arrange
        var existingQuest = new Quest { Id = "quest-1", Title = "Test Quest" };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { existingQuest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.StartQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Quest is already active");
    }

    [Fact]
    public async Task StartQuestAsync_Should_Fail_When_Quest_Already_Completed()
    {
        // Arrange
        var completedQuest = new Quest { Id = "quest-1", Title = "Test Quest" };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>(),
            CompletedQuests = new List<Quest> { completedQuest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.StartQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Quest is already completed");
    }

    [Fact]
    public async Task StartQuestAsync_Should_Fail_When_Quest_Not_Found()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>(),
            CompletedQuests = new List<Quest>()
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        _mockMainQuestService.Setup(m => m.GetQuestByIdAsync("quest-1")).ReturnsAsync((Quest?)null);

        // Act
        var result = await _service.StartQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Quest not found");
    }

    [Fact]
    public async Task StartQuestAsync_Should_Add_Quest_To_Active_Quests()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>(),
            CompletedQuests = new List<Quest>()
        };
        var quest = new Quest
        {
            Id = "quest-1",
            Title = "Test Quest",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        _mockMainQuestService.Setup(m => m.GetQuestByIdAsync("quest-1")).ReturnsAsync(quest);

        // Act
        var result = await _service.StartQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeTrue();
        result.Quest.Should().NotBeNull();
        result.Quest!.IsActive.Should().BeTrue();
        saveGame.ActiveQuests.Should().Contain(q => q.Id == "quest-1");
    }

    [Fact]
    public async Task CompleteQuestAsync_Should_Fail_When_No_Active_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.CompleteQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No active save game");
    }

    [Fact]
    public async Task CompleteQuestAsync_Should_Fail_When_Quest_Not_Active()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>()
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.CompleteQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Quest is not active");
    }

    [Fact]
    public async Task CompleteQuestAsync_Should_Fail_When_Objectives_Not_Complete()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Title = "Test Quest",
            Objectives = new Dictionary<string, int>
            {
                ["objective-1"] = 10,
                ["objective-2"] = 5
            },
            ObjectiveProgress = new Dictionary<string, int>
            {
                ["objective-1"] = 10,
                ["objective-2"] = 3  // Not complete
            }
        };

        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.CompleteQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Not all objectives complete");
    }

    [Fact]
    public async Task CompleteQuestAsync_Should_Move_Quest_To_Completed()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Title = "Test Quest",
            Objectives = new Dictionary<string, int>
            {
                ["objective-1"] = 10,
                ["objective-2"] = 5
            },
            ObjectiveProgress = new Dictionary<string, int>
            {
                ["objective-1"] = 10,
                ["objective-2"] = 5
            }
        };

        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest },
            CompletedQuests = new List<Quest>(),
            QuestsCompleted = 0
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.CompleteQuestAsync("quest-1");

        // Assert
        result.Success.Should().BeTrue();
        result.Quest!.IsCompleted.Should().BeTrue();
        result.Quest.IsActive.Should().BeFalse();
        saveGame.ActiveQuests.Should().NotContain(q => q.Id == "quest-1");
        saveGame.CompletedQuests.Should().Contain(q => q.Id == "quest-1");
        saveGame.QuestsCompleted.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveQuestsAsync_Should_Return_Empty_When_No_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.GetActiveQuestsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveQuestsAsync_Should_Return_Active_Quests()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>
            {
                new Quest { Id = "quest-1", Title = "Quest 1" },
                new Quest { Id = "quest-2", Title = "Quest 2" }
            }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.GetActiveQuestsAsync();

        // Assert
        result.Should().HaveCount(2);
    }
}
