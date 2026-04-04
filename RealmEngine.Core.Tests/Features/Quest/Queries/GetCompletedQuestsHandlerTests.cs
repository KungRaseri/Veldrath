using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.Quests.Queries;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using QuestModel = RealmEngine.Shared.Models.Quest;

namespace RealmEngine.Core.Tests.Features.Quest.Queries;

/// <summary>
/// Unit tests for <see cref="GetCompletedQuestsHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetCompletedQuestsHandlerTests
{
    private static GetCompletedQuestsHandler CreateHandler(SaveGame? save = null)
    {
        var svc = new Mock<ISaveGameService>();
        svc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new GetCompletedQuestsHandler(svc.Object);
    }

    private static QuestModel MakeQuest(string id, string title = "A Quest") =>
        new() { Id = id, Title = title, DisplayName = title };

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoActiveSave()
    {
        var result = await CreateHandler(save: null)
            .Handle(new GetCompletedQuestsQuery(), default);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenCompletedQuestsIsEmpty()
    {
        var save = new SaveGame { CompletedQuests = [] };

        var result = await CreateHandler(save)
            .Handle(new GetCompletedQuestsQuery(), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllCompletedQuests()
    {
        var quests = new List<QuestModel>
        {
            MakeQuest("quest-01", "The Awakening"),
            MakeQuest("quest-02", "Into the Dark"),
            MakeQuest("quest-03", "The Reckoning"),
        };
        var save = new SaveGame { CompletedQuests = quests };

        var result = await CreateHandler(save)
            .Handle(new GetCompletedQuestsQuery(), default);

        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(quests);
    }

    [Fact]
    public async Task Handle_ReturnsSameReference_AsCompletedQuestsList()
    {
        var quests = new List<QuestModel> { MakeQuest("quest-01") };
        var save = new SaveGame { CompletedQuests = quests };

        var result = await CreateHandler(save)
            .Handle(new GetCompletedQuestsQuery(), default);

        result.Should().BeSameAs(quests);
    }
}

/// <summary>
/// Unit tests for <see cref="GetAvailableQuestsHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetAvailableQuestsHandlerTests
{
    private static GetAvailableQuestsHandler CreateHandler(SaveGame? save = null)
    {
        var svc = new Mock<ISaveGameService>();
        svc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new GetAvailableQuestsHandler(svc.Object);
    }

    private static QuestModel MakeQuest(string id, string title = "A Quest") =>
        new() { Id = id, Title = title, DisplayName = title };

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoActiveSave()
    {
        var result = await CreateHandler(save: null)
            .Handle(new GetAvailableQuestsQuery(), default);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenAvailableQuestsIsEmpty()
    {
        var save = new SaveGame { AvailableQuests = [] };

        var result = await CreateHandler(save)
            .Handle(new GetAvailableQuestsQuery(), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllAvailableQuests()
    {
        var quests = new List<QuestModel>
        {
            MakeQuest("quest-01", "The Awakening"),
            MakeQuest("quest-04", "A New Lead"),
        };
        var save = new SaveGame { AvailableQuests = quests };

        var result = await CreateHandler(save)
            .Handle(new GetAvailableQuestsQuery(), default);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(quests);
    }

    [Fact]
    public async Task Handle_ReturnsSameReference_AsAvailableQuestsList()
    {
        var quests = new List<QuestModel> { MakeQuest("quest-01") };
        var save = new SaveGame { AvailableQuests = quests };

        var result = await CreateHandler(save)
            .Handle(new GetAvailableQuestsQuery(), default);

        result.Should().BeSameAs(quests);
    }

    [Fact]
    public async Task Handle_DoesNotReturnCompletedOrActiveQuests()
    {
        var available = new List<QuestModel> { MakeQuest("q-avail") };
        var save = new SaveGame
        {
            AvailableQuests = available,
            ActiveQuests    = [MakeQuest("q-active")],
            CompletedQuests = [MakeQuest("q-done")],
        };

        var result = await CreateHandler(save)
            .Handle(new GetAvailableQuestsQuery(), default);

        result.Should().HaveCount(1);
        result.Should().Contain(q => q.Id == "q-avail");
    }
}

/// <summary>
/// Unit tests for <see cref="InitializeStartingQuestsHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class InitializeStartingQuestsHandlerTests
{
    private static InitializeStartingQuestsHandler CreateHandler(
        Mock<QuestInitializationService>? initSvc = null)
    {
        var svc = initSvc ?? new Mock<QuestInitializationService>(
            new Mock<MainQuestService>(true).Object,
            NullLogger<QuestInitializationService>.Instance);
        return new InitializeStartingQuestsHandler(svc.Object);
    }

    private static SaveGame SaveWithAvailableQuests(int count)
    {
        var save = new SaveGame();
        for (var i = 0; i < count; i++)
            save.AvailableQuests.Add(new QuestModel { Id = $"q-{i}", Title = $"Quest {i}", DisplayName = $"Quest {i}" });
        return save;
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenServiceCompletes()
    {
        var save = SaveWithAvailableQuests(1);
        var initSvc = new Mock<QuestInitializationService>(
            new Mock<MainQuestService>(true).Object,
            NullLogger<QuestInitializationService>.Instance);
        initSvc.Setup(s => s.InitializeStartingQuests(save))
               .Returns(Task.CompletedTask);

        var result = await CreateHandler(initSvc)
            .Handle(new InitializeStartingQuestsCommand(save), default);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsQuestsInitialized_EqualToSaveAvailableCount()
    {
        var save = SaveWithAvailableQuests(3);
        var initSvc = new Mock<QuestInitializationService>(
            new Mock<MainQuestService>(true).Object,
            NullLogger<QuestInitializationService>.Instance);
        initSvc.Setup(s => s.InitializeStartingQuests(save))
               .Returns(Task.CompletedTask);

        var result = await CreateHandler(initSvc)
            .Handle(new InitializeStartingQuestsCommand(save), default);

        result.QuestsInitialized.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CallsInitService_WithCorrectSaveGame()
    {
        var save = new SaveGame();
        var initSvc = new Mock<QuestInitializationService>(
            new Mock<MainQuestService>(true).Object,
            NullLogger<QuestInitializationService>.Instance);
        initSvc.Setup(s => s.InitializeStartingQuests(save))
               .Returns(Task.CompletedTask);

        await CreateHandler(initSvc)
            .Handle(new InitializeStartingQuestsCommand(save), default);

        initSvc.Verify(s => s.InitializeStartingQuests(save), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsZeroQuestsInitialized_WhenSaveHasNoAvailableQuests()
    {
        var save = new SaveGame(); // AvailableQuests = empty
        var initSvc = new Mock<QuestInitializationService>(
            new Mock<MainQuestService>(true).Object,
            NullLogger<QuestInitializationService>.Instance);
        initSvc.Setup(s => s.InitializeStartingQuests(save))
               .Returns(Task.CompletedTask);

        var result = await CreateHandler(initSvc)
            .Handle(new InitializeStartingQuestsCommand(save), default);

        result.QuestsInitialized.Should().Be(0);
    }
}
