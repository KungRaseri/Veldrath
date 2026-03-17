using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Quests.Queries;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using QuestModel = RealmEngine.Shared.Models.Quest;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Quest.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetActiveQuestsHandler.
/// </summary>
public class GetActiveQuestsHandlerTests
{
    private readonly Mock<QuestService> _mockQuestService;
    private readonly GetActiveQuestsHandler _handler;

    public GetActiveQuestsHandlerTests()
    {
        var mockSaveGameService = new Mock<ISaveGameService>();
        var mockMainQuestService = new Mock<MainQuestService>(true); // Use protected constructor for mocking
        var mockInitService = new Mock<QuestInitializationService>(mockMainQuestService.Object, NullLogger<QuestInitializationService>.Instance);
        
        _mockQuestService = new Mock<QuestService>(MockBehavior.Strict, 
            mockSaveGameService.Object, 
            mockMainQuestService.Object, 
            mockInitService.Object,
            NullLogger<QuestService>.Instance);
        _handler = new GetActiveQuestsHandler(_mockQuestService.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Active_Quests()
    {
        // Arrange
        var activeQuests = new List<QuestModel>
        {
            new QuestModel { Id = "quest-001", Title = "Quest 1", IsActive = true },
            new QuestModel { Id = "quest-002", Title = "Quest 2", IsActive = true },
            new QuestModel { Id = "quest-003", Title = "Quest 3", IsActive = true }
        };
        var query = new GetActiveQuestsQuery();

        _mockQuestService
            .Setup(s => s.GetActiveQuestsAsync())
            .ReturnsAsync(activeQuests);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(activeQuests);
        _mockQuestService.Verify(s => s.GetActiveQuestsAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Active_Quests()
    {
        // Arrange
        var emptyList = new List<QuestModel>();
        var query = new GetActiveQuestsQuery();

        _mockQuestService
            .Setup(s => s.GetActiveQuestsAsync())
            .ReturnsAsync(emptyList);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Return_Only_Active_Quests()
    {
        // Arrange
        var activeQuests = new List<QuestModel>
        {
            new QuestModel { Id = "quest-001", Title = "Active Quest 1", IsActive = true, IsCompleted = false },
            new QuestModel { Id = "quest-002", Title = "Active Quest 2", IsActive = true, IsCompleted = false }
        };
        var query = new GetActiveQuestsQuery();

        _mockQuestService
            .Setup(s => s.GetActiveQuestsAsync())
            .ReturnsAsync(activeQuests);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(q => q.IsActive);
        result.Should().OnlyContain(q => !q.IsCompleted);
    }

    [Fact]
    public async Task Handle_Should_Return_Quests_With_Progress()
    {
        // Arrange
        var activeQuests = new List<QuestModel>
        {
            new QuestModel
            {
                Id = "quest-001",
                Title = "Kill Enemies",
                IsActive = true,
                Objectives = new Dictionary<string, int> { ["kill-goblins"] = 10 },
                ObjectiveProgress = new Dictionary<string, int> { ["kill-goblins"] = 5 }
            }
        };
        var query = new GetActiveQuestsQuery();

        _mockQuestService
            .Setup(s => s.GetActiveQuestsAsync())
            .ReturnsAsync(activeQuests);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var quest = result.First();
        quest.Objectives.Should().ContainKey("kill-goblins").WhoseValue.Should().Be(10);
        quest.ObjectiveProgress.Should().ContainKey("kill-goblins").WhoseValue.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_Preserve_Quest_Order()
    {
        // Arrange
        var activeQuests = new List<QuestModel>
        {
            new QuestModel { Id = "quest-001", Title = "First Quest", IsActive = true },
            new QuestModel { Id = "quest-002", Title = "Second Quest", IsActive = true },
            new QuestModel { Id = "quest-003", Title = "Third Quest", IsActive = true }
        };
        var query = new GetActiveQuestsQuery();

        _mockQuestService
            .Setup(s => s.GetActiveQuestsAsync())
            .ReturnsAsync(activeQuests);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainInOrder(activeQuests);
    }
}
