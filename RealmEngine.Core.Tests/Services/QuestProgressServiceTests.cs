using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using Moq;
using FluentAssertions;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class QuestProgressServiceTests
{
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly QuestProgressService _service;

    public QuestProgressServiceTests()
    {
        _mockSaveGameService = new Mock<SaveGameService>();
        _service = new QuestProgressService(_mockSaveGameService.Object, NullLogger<QuestProgressService>.Instance);
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Fail_When_No_Active_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.ObjectiveCompleted.Should().BeFalse();
        result.QuestCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Fail_When_Quest_Not_Active()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest>()
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 1);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Fail_When_Objective_Not_Found()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 }
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-999", 1);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Increase_Objective_Progress()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 },
            ObjectiveProgress = new Dictionary<string, int> { ["objective-1"] = 5 }
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 3);

        // Assert
        result.Success.Should().BeTrue();
        quest.ObjectiveProgress["objective-1"].Should().Be(8);
        result.ObjectiveCompleted.Should().BeFalse("objective not fully complete yet");
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Complete_Objective()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 },
            ObjectiveProgress = new Dictionary<string, int> { ["objective-1"] = 8 }
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 5);

        // Assert
        result.Success.Should().BeTrue();
        quest.ObjectiveProgress["objective-1"].Should().Be(10, "should not exceed required");
        result.ObjectiveCompleted.Should().BeTrue();
        result.QuestCompleted.Should().BeTrue("quest has only one objective");
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Not_Exceed_Required_Amount()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 },
            ObjectiveProgress = new Dictionary<string, int> { ["objective-1"] = 9 }
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 100);

        // Assert
        result.Success.Should().BeTrue();
        quest.ObjectiveProgress["objective-1"].Should().Be(10, "should cap at required amount");
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Initialize_Progress_If_Missing()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int> { ["objective-1"] = 10 },
            ObjectiveProgress = new Dictionary<string, int>() // Empty
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-1", 3);

        // Assert
        result.Success.Should().BeTrue();
        quest.ObjectiveProgress.Should().ContainKey("objective-1");
        quest.ObjectiveProgress["objective-1"].Should().Be(3);
    }

    [Fact]
    public async Task UpdateProgressAsync_Should_Detect_Quest_Completion()
    {
        // Arrange
        var quest = new Quest
        {
            Id = "quest-1",
            Objectives = new Dictionary<string, int>
            {
                ["objective-1"] = 10,
                ["objective-2"] = 5
            },
            ObjectiveProgress = new Dictionary<string, int>
            {
                ["objective-1"] = 10,  // Already complete
                ["objective-2"] = 4    // Almost complete
            }
        };
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            ActiveQuests = new List<Quest> { quest }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UpdateProgressAsync("quest-1", "objective-2", 1);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeTrue("objective-2 should be complete");
        result.QuestCompleted.Should().BeTrue("all objectives are now complete");
    }
}
