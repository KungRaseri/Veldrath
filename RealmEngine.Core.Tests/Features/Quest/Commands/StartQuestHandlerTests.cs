using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using QuestModel = RealmEngine.Shared.Models.Quest;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Quest.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for StartQuestHandler.
/// </summary>
public class StartQuestHandlerTests
{
    private readonly Mock<QuestService> _mockQuestService;
    private readonly StartQuestHandler _handler;

    public StartQuestHandlerTests()
    {
        var mockSaveGameService = new Mock<ISaveGameService>();
        var mockMainQuestService = new Mock<MainQuestService>(true); // Use protected constructor for mocking
        var mockInitService = new Mock<QuestInitializationService>(mockMainQuestService.Object, NullLogger<QuestInitializationService>.Instance);
        
        _mockQuestService = new Mock<QuestService>(MockBehavior.Strict,
            mockSaveGameService.Object,
            mockMainQuestService.Object,
            mockInitService.Object,
            NullLogger<QuestService>.Instance);
        _handler = new StartQuestHandler(_mockQuestService.Object);
    }

    [Fact]
    public async Task Handle_Should_Start_Quest_Successfully()
    {
        // Arrange
        var questId = "quest-001";
        var quest = new QuestModel
        {
            Id = questId,
            Title = "Test Quest",
            Description = "A test quest",
            IsActive = true
        };
        var command = new StartQuestCommand(questId);

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((true, quest, string.Empty));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Quest started");
        result.Message.Should().Contain(quest.Title);
        result.Quest.Should().Be(quest);
        _mockQuestService.Verify(s => s.StartQuestAsync(questId), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Quest_Not_Found()
    {
        // Arrange
        var questId = "quest-999";
        var command = new StartQuestCommand(questId);
        var errorMessage = "Quest not found";

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Quest.Should().BeNull();
        _mockQuestService.Verify(s => s.StartQuestAsync(questId), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Quest_Already_Active()
    {
        // Arrange
        var questId = "quest-001";
        var command = new StartQuestCommand(questId);
        var errorMessage = "Quest is already active";

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Quest.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Quest_Already_Completed()
    {
        // Arrange
        var questId = "quest-001";
        var command = new StartQuestCommand(questId);
        var errorMessage = "Quest is already completed";

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Quest.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Prerequisites_Not_Met()
    {
        // Arrange
        var questId = "quest-002";
        var command = new StartQuestCommand(questId);
        var errorMessage = "Prerequisites not met";

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Quest.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_No_Active_Save()
    {
        // Arrange
        var questId = "quest-001";
        var command = new StartQuestCommand(questId);
        var errorMessage = "No active save game";

        _mockQuestService
            .Setup(s => s.StartQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Quest.Should().BeNull();
    }
}
