using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Quest;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="GetQuestLogHubCommandHandler"/> and the quest journal helpers.
/// </summary>
public class GetQuestLogHubCommandHandlerTests
{
    private static GetQuestLogHubCommandHandler MakeHandler(
        ICharacterRepository charRepo,
        IQuestRepository questRepo) =>
        new(charRepo, questRepo, NullLogger<GetQuestLogHubCommandHandler>.Instance);

    private static Character MakeCharacter(Guid id, string questBlob = "[]") =>
        new()
        {
            Id = id,
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
            InventoryBlob = "[]",
            QuestBlob = questBlob,
        };

    private static Mock<ICharacterRepository> CharacterRepo(Character character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<IQuestRepository> QuestRepo(params Quest[] quests)
    {
        var repo = new Mock<IQuestRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(quests.ToList());
        foreach (var quest in quests)
            repo.Setup(r => r.GetBySlugAsync(quest.Slug)).ReturnsAsync(quest);
        return repo;
    }

    private static string Journal(params CharacterQuestEntry[] entries) =>
        QuestJournal.Serialize(entries);

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterNotFound()
    {
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Character?)null);

        var result = await MakeHandler(charRepo.Object, QuestRepo().Object)
            .Handle(new GetQuestLogHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Journal_With_Objective_Progress()
    {
        var character = MakeCharacter(Guid.NewGuid(), Journal(new CharacterQuestEntry(
            "wolves",
            "Thin the pack",
            "Cull the wolves.",
            CharacterQuestEntry.StatusActive,
            50,
            120,
            new Dictionary<string, int> { ["kill:Barrow Wolf"] = 3 },
            new Dictionary<string, int> { ["kill:Barrow Wolf"] = 3 },
            DateTimeOffset.UtcNow)));

        var result = await MakeHandler(CharacterRepo(character).Object, QuestRepo().Object)
            .Handle(new GetQuestLogHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        var entry = result.Quests.Should().ContainSingle().Subject;
        entry.Slug.Should().Be("wolves");
        entry.CanTurnIn.Should().BeTrue();
        entry.Objectives.Should().ContainSingle();
        entry.Objectives[0].Current.Should().Be(3);
        entry.Objectives[0].Complete.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Offers_Untaken_Catalog_Quests_As_Available()
    {
        var character = MakeCharacter(Guid.NewGuid(), Journal(new CharacterQuestEntry(
            "taken",
            "Already mine",
            string.Empty,
            CharacterQuestEntry.StatusActive,
            0,
            0,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            DateTimeOffset.UtcNow)));

        var questRepo = QuestRepo(
            new Quest { Slug = "taken", Title = "Already mine" },
            new Quest
            {
                Slug = "fresh",
                Title = "A new lead",
                Description = "Look into it.",
                GoldReward = 25,
                XpReward = 60,
                Objectives = new Dictionary<string, int> { ["visit:grey-cup"] = 1 },
            });

        var result = await MakeHandler(CharacterRepo(character).Object, questRepo.Object)
            .Handle(new GetQuestLogHubCommand(character.Id), CancellationToken.None);

        result.Available.Should().ContainSingle();
        result.Available[0].Slug.Should().Be("fresh");
        result.Available[0].GoldReward.Should().Be(25);
        result.Available[0].Objectives.Should().Contain("visit:grey-cup");
    }

    [Fact]
    public void Journal_Advance_Should_Increment_Matching_Objectives_Only()
    {
        var journal = new List<CharacterQuestEntry>
        {
            new(
                "wolves",
                "Thin the pack",
                string.Empty,
                CharacterQuestEntry.StatusActive,
                0,
                0,
                new Dictionary<string, int> { ["kill:Barrow Wolf"] = 2 },
                new Dictionary<string, int>(),
                DateTimeOffset.UtcNow),
        };

        var changed = QuestJournal.Advance(journal, "kill", "barrow wolf");
        QuestJournal.Advance(journal, "collect", "health_potion");

        changed.Should().Be(1);
        journal[0].Progress["kill:Barrow Wolf"].Should().Be(1);
        journal[0].Progress.Should().HaveCount(1);
    }
}
