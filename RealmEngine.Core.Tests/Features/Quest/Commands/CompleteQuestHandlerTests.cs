using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using QuestModel = RealmEngine.Shared.Models.Quest;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Quest.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for CompleteQuestHandler.
/// </summary>
public class CompleteQuestHandlerTests
{
    private readonly Mock<QuestService> _mockQuestService;
    private readonly Mock<QuestRewardService> _mockRewardService;
    private readonly Mock<ISaveGameService> _mockSaveGameService;
    private readonly CompleteQuestHandler _handler;

    public CompleteQuestHandlerTests()
    {
        _mockQuestService = new Mock<QuestService>(MockBehavior.Strict, null!, null!, null!, NullLogger<QuestService>.Instance);
        _mockRewardService = new Mock<QuestRewardService>(MockBehavior.Strict, (ISaveGameService)null!, NullLogger<QuestRewardService>.Instance);
        _mockSaveGameService = new Mock<ISaveGameService>();
        _handler = new CompleteQuestHandler(_mockQuestService.Object, _mockRewardService.Object, _mockSaveGameService.Object);
    }

    [Fact]
    public async Task Handle_Should_Complete_Quest_Successfully()
    {
        // Arrange
        var questId = "quest-001";
        var quest = new QuestModel
        {
            Id = questId,
            Title = "Test Quest",
            Description = "A test quest",
            XpReward = 100,
            GoldReward = 50,
            ApocalypseBonusMinutes = 10,
            ItemRewardIds = new List<string> { "item-001", "item-002" }
        };
        var command = new CompleteQuestCommand(questId);
        var saveGame = new SaveGame { Character = new Character() };

        _mockQuestService
            .Setup(s => s.CompleteQuestAsync(questId))
            .ReturnsAsync((true, quest, string.Empty));
        
        _mockSaveGameService
            .Setup(s => s.GetCurrentSave())
            .Returns(saveGame);

        _mockRewardService
            .Setup(s => s.DistributeRewards(quest, saveGame.Character, saveGame))
            .Verifiable();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Quest completed");
        result.Message.Should().Contain(quest.Title);
        result.Rewards.Should().NotBeNull();
        result.Rewards!.Xp.Should().Be(100);
        result.Rewards.Gold.Should().Be(50);
        result.Rewards.ApocalypseBonus.Should().Be(10);
        result.Rewards.Items.Should().BeEquivalentTo(["item-001", "item-002"]);
        _mockQuestService.Verify(s => s.CompleteQuestAsync(questId), Times.Once);
        _mockRewardService.Verify();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Quest_Not_Active()
    {
        // Arrange
        var questId = "quest-999";
        var command = new CompleteQuestCommand(questId);
        var errorMessage = "Quest is not active";

        _mockQuestService
            .Setup(s => s.CompleteQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Rewards.Should().BeNull();
        _mockQuestService.Verify(s => s.CompleteQuestAsync(questId), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Objectives_Not_Complete()
    {
        // Arrange
        var questId = "quest-001";
        var command = new CompleteQuestCommand(questId);
        var errorMessage = "Not all objectives complete";

        _mockQuestService
            .Setup(s => s.CompleteQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Rewards.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_No_Active_Save()
    {
        // Arrange
        var questId = "quest-001";
        var command = new CompleteQuestCommand(questId);
        var errorMessage = "No active save game";

        _mockQuestService
            .Setup(s => s.CompleteQuestAsync(questId))
            .ReturnsAsync((false, null, errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(errorMessage);
        result.Rewards.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Handle_Quest_With_No_Rewards()
    {
        // Arrange
        var questId = "quest-no-rewards";
        var quest = new QuestModel
        {
            Id = questId,
            Title = "No Reward Quest",
            Description = "A quest with no rewards",
            XpReward = 0,
            GoldReward = 0,
            ApocalypseBonusMinutes = 0,
            ItemRewardIds = new List<string>()
        };
        var command = new CompleteQuestCommand(questId);

        _mockQuestService
            .Setup(s => s.CompleteQuestAsync(questId))
            .ReturnsAsync((true, quest, string.Empty));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Rewards.Should().NotBeNull();
        result.Rewards!.Xp.Should().Be(0);
        result.Rewards.Gold.Should().Be(0);
        result.Rewards.ApocalypseBonus.Should().Be(0);
        result.Rewards.Items.Should().BeEmpty();
    }
}
