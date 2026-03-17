using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.Quests.Services;

namespace RealmEngine.Core.Tests.Features.Quest.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UpdateQuestProgressHandler.
/// </summary>
public class UpdateQuestProgressHandlerTests
{
    private readonly Mock<QuestProgressService> _mockProgressService;
    private readonly UpdateQuestProgressHandler _handler;

    public UpdateQuestProgressHandlerTests()
    {
        _mockProgressService = new Mock<QuestProgressService>(MockBehavior.Strict, null!, NullLogger<QuestProgressService>.Instance);
        _handler = new UpdateQuestProgressHandler(_mockProgressService.Object);
    }

    [Fact]
    public async Task Handle_Should_Update_Progress_Successfully()
    {
        // Arrange
        var questId = "quest-001";
        var objectiveId = "kill-enemies";
        var amount = 5;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: true, ObjectiveCompleted: false, QuestCompleted: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeFalse();
        result.QuestCompleted.Should().BeFalse();
        _mockProgressService.Verify(s => s.UpdateProgressAsync(questId, objectiveId, amount), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Mark_Objective_Complete()
    {
        // Arrange
        var questId = "quest-001";
        var objectiveId = "kill-enemies";
        var amount = 10;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: true, ObjectiveCompleted: true, QuestCompleted: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeTrue();
        result.QuestCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Mark_Quest_Complete_When_All_Objectives_Done()
    {
        // Arrange
        var questId = "quest-001";
        var objectiveId = "final-objective";
        var amount = 1;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: true, ObjectiveCompleted: true, QuestCompleted: true));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeTrue();
        result.QuestCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Update_Fails()
    {
        // Arrange
        var questId = "quest-999";
        var objectiveId = "invalid-objective";
        var amount = 5;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: false, ObjectiveCompleted: false, QuestCompleted: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ObjectiveCompleted.Should().BeFalse();
        result.QuestCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Update_Progress_With_Large_Amount()
    {
        // Arrange
        var questId = "quest-001";
        var objectiveId = "collect-items";
        var amount = 100;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: true, ObjectiveCompleted: true, QuestCompleted: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Update_Progress_With_Negative_Amount()
    {
        // Arrange (in case of progress reduction/rollback)
        var questId = "quest-001";
        var objectiveId = "objective-001";
        var amount = -5;
        var command = new UpdateQuestProgressCommand(questId, objectiveId, amount);

        _mockProgressService
            .Setup(s => s.UpdateProgressAsync(questId, objectiveId, amount))
            .ReturnsAsync((Success: true, ObjectiveCompleted: false, QuestCompleted: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ObjectiveCompleted.Should().BeFalse();
        result.QuestCompleted.Should().BeFalse();
    }
}
