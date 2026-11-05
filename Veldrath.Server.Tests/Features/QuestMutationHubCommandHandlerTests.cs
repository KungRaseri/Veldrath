using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Quest;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for the quest mutation hub handlers in
/// <see cref="Veldrath.Server.Features.Quest.QuestMutationHubCommands"/>: accept, abandon, turn in and advance.
/// Every case checks the returned result, the journal that was persisted back to the character, and the projection
/// the client receives (<c>Quests</c>), because a mutation that returns success but writes nothing would leave the
/// journal and the HUD describing different worlds.
/// </summary>
public class QuestMutationHubCommandHandlerTests
{
    private const string Active = CharacterQuestEntry.StatusActive;
    private const string Completed = CharacterQuestEntry.StatusCompleted;

    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static Character MakeCharacter(string blob = "[]") => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        Name = "QuestHero",
        ClassName = "Warrior",
        SlotIndex = 1,
        Attributes = "{}",
        InventoryBlob = "[]",
        QuestBlob = blob,
    };

    private static string Journal(params CharacterQuestEntry[] entries) =>
        QuestJournal.Serialize(entries);

    private static CharacterQuestEntry Entry(
        string slug,
        string status = Active,
        int gold = 25,
        int xp = 60,
        Dictionary<string, int>? objectives = null,
        Dictionary<string, int>? progress = null) =>
        new(slug,
            $"Title of {slug}",
            $"Description of {slug}",
            status,
            gold,
            xp,
            objectives ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            progress ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow);

    private static (Mock<ICharacterRepository> Repo, Character Character) CharacterRepo(Character character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (repo, character);
    }

    private static Mock<ICharacterRepository> MissingCharacterRepo()
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Character?)null);
        return repo;
    }

    private static Mock<IQuestRepository> QuestRepo(params Quest[] quests)
    {
        var repo = new Mock<IQuestRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(quests.ToList());
        foreach (var quest in quests)
            repo.Setup(r => r.GetBySlugAsync(quest.Slug)).ReturnsAsync(quest);
        repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync((string slug) => quests.FirstOrDefault(q => q.Slug == slug));
        return repo;
    }

    private static AcceptQuestHubCommandHandler AcceptHandler(
        ICharacterRepository chars, IQuestRepository quests) =>
        new(chars, quests, NullLogger<AcceptQuestHubCommandHandler>.Instance);

    private static AbandonQuestHubCommandHandler AbandonHandler(ICharacterRepository chars) =>
        new(chars, NullLogger<AbandonQuestHubCommandHandler>.Instance);

    private static TurnInQuestHubCommandHandler TurnInHandler(ICharacterRepository chars) =>
        new(chars, NullLogger<TurnInQuestHubCommandHandler>.Instance);

    private static AdvanceQuestObjectivesHubCommandHandler AdvanceHandler(ICharacterRepository chars) =>
        new(chars, NullLogger<AdvanceQuestObjectivesHubCommandHandler>.Instance);

    // ── AcceptQuestHubCommandHandler ──────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Accept_WithoutSlug_FailsWithoutTouchingTheCharacter(string slug)
    {
        var (repo, character) = CharacterRepo(MakeCharacter());

        var result = await AcceptHandler(repo.Object, QuestRepo().Object)
            .Handle(new AcceptQuestHubCommand(character.Id, slug), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("slug is required");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_UnknownCharacter_ReportsTheMissingCharacter()
    {
        var id = Guid.NewGuid();

        var result = await AcceptHandler(MissingCharacterRepo().Object, QuestRepo().Object)
            .Handle(new AcceptQuestHubCommand(id, "missing-quest"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain(id.ToString()).And.Contain("not found");
    }

    [Fact]
    public async Task Accept_QuestAlreadyInJournal_FailsAndKeepsTheJournal()
    {
        var character = MakeCharacter(Journal(Entry("wolves")));
        var (repo, _) = CharacterRepo(character);

        var result = await AcceptHandler(repo.Object, QuestRepo(new Quest { Slug = "wolves" }).Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already in your journal");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_JournalSlugMatchIsCaseInsensitive()
    {
        var character = MakeCharacter(Journal(Entry("Wolves")));
        var (repo, _) = CharacterRepo(character);

        var result = await AcceptHandler(repo.Object, QuestRepo(new Quest { Slug = "wolves" }).Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already in your journal");
    }

    [Fact]
    public async Task Accept_UnknownCatalogQuest_Fails()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);

        var result = await AcceptHandler(repo.Object, QuestRepo().Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "no-such-quest"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no-such-quest").And.Contain("does not exist");
    }

    [Fact]
    public async Task Accept_CatalogLookupThrows_FailsInsteadOfPropagating()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);
        var quests = new Mock<IQuestRepository>();
        quests.Setup(r => r.GetBySlugAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("catalog is down"));

        var result = await AcceptHandler(repo.Object, quests.Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_ValidQuest_AppendsAnActiveEntryWithEmptyProgress()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);
        var quest = new Quest
        {
            Slug = "wolves",
            Title = "Thin the Pack",
            Description = "Cull the wolves.",
            GoldReward = 40,
            XpReward = 120,
            Objectives = new Dictionary<string, int> { ["kill:Barrow Wolf"] = 3 },
        };

        var result = await AcceptHandler(repo.Object, QuestRepo(quest).Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeTrue();
        var persisted = QuestJournal.Parse(character.QuestBlob).Should().ContainSingle().Subject;
        persisted.Slug.Should().Be("wolves");
        persisted.Title.Should().Be("Thin the Pack");
        persisted.Description.Should().Be("Cull the wolves.");
        persisted.Status.Should().Be(Active);
        persisted.GoldReward.Should().Be(40);
        persisted.XpReward.Should().Be(120);
        persisted.Objectives.Should().ContainKey("kill:Barrow Wolf").WhoseValue.Should().Be(3);
        persisted.Progress.Should().BeEmpty("accepting starts every objective at zero");

        var dto = result.Quests.Should().ContainSingle().Subject;
        dto.Slug.Should().Be("wolves");
        dto.CanTurnIn.Should().BeFalse();
        dto.Objectives.Should().ContainSingle();
        dto.Objectives[0].Current.Should().Be(0);
        dto.Objectives[0].Required.Should().Be(3);
        dto.Objectives[0].Complete.Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_BlankTitleAndZeroRewards_FallsBackToSlugAndBaseRewards()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);
        var quest = new Quest
        {
            Slug = "untitled",
            Title = "  ",
            BaseGoldReward = 7,
            BaseXpReward = 11,
        };

        var result = await AcceptHandler(repo.Object, QuestRepo(quest).Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "untitled"), CancellationToken.None);

        result.Success.Should().BeTrue();
        var persisted = QuestJournal.Parse(character.QuestBlob).Single();
        persisted.Title.Should().Be("untitled", "a blank catalog title falls back to the slug");
        persisted.GoldReward.Should().Be(7);
        persisted.XpReward.Should().Be(11);
    }

    [Fact]
    public async Task Accept_KeepsExistingEntriesAndProjectsAllOfThem()
    {
        var character = MakeCharacter(Journal(Entry("wolves")));
        var (repo, _) = CharacterRepo(character);
        var quest = new Quest { Slug = "bandits", Title = "Bandit Trouble", GoldReward = 5, XpReward = 10 };

        var result = await AcceptHandler(repo.Object, QuestRepo(quest).Object)
            .Handle(new AcceptQuestHubCommand(character.Id, "bandits"), CancellationToken.None);

        result.Quests.Should().HaveCount(2);
        result.Quests.Select(q => q.Slug).Should().BeEquivalentTo(["wolves", "bandits"]);
        QuestJournal.Parse(character.QuestBlob).Should().HaveCount(2);
    }

    // ── AbandonQuestHubCommandHandler ─────────────────────────────────────────

    [Fact]
    public async Task Abandon_UnknownCharacter_ReportsTheMissingCharacter()
    {
        var id = Guid.NewGuid();

        var result = await AbandonHandler(MissingCharacterRepo().Object)
            .Handle(new AbandonQuestHubCommand(id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Abandon_QuestNotInJournal_Fails()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);

        var result = await AbandonHandler(repo.Object)
            .Handle(new AbandonQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("That quest is not active in your journal.");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Abandon_AlreadyCompletedQuest_Fails()
    {
        var character = MakeCharacter(Journal(Entry("wolves", Completed)));
        var (repo, _) = CharacterRepo(character);

        var result = await AbandonHandler(repo.Object)
            .Handle(new AbandonQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not active");
        QuestJournal.Parse(character.QuestBlob).Should().ContainSingle("a completed quest is history, not deletable");
    }

    [Fact]
    public async Task Abandon_ActiveQuest_RemovesItAndPersistsTheJournal()
    {
        var character = MakeCharacter(Journal(Entry("wolves"), Entry("bandits")));
        var (repo, _) = CharacterRepo(character);

        var result = await AbandonHandler(repo.Object)
            .Handle(new AbandonQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().ContainSingle().Which.Slug.Should().Be("bandits");
        QuestJournal.Parse(character.QuestBlob).Should().ContainSingle().Which.Slug.Should().Be("bandits");
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Abandon_SlugMatchIsCaseInsensitive()
    {
        var character = MakeCharacter(Journal(Entry("Wolves")));
        var (repo, _) = CharacterRepo(character);

        var result = await AbandonHandler(repo.Object)
            .Handle(new AbandonQuestHubCommand(character.Id, "wOlVeS"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().BeEmpty();
    }

    // ── TurnInQuestHubCommandHandler ──────────────────────────────────────────

    [Fact]
    public async Task TurnIn_UnknownCharacter_ReportsTheMissingCharacter()
    {
        var id = Guid.NewGuid();

        var result = await TurnInHandler(MissingCharacterRepo().Object)
            .Handle(new TurnInQuestHubCommand(id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task TurnIn_QuestNotInJournal_Fails()
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("That quest is not in your journal.");
    }

    [Fact]
    public async Task TurnIn_AlreadyCompleted_Fails()
    {
        var character = MakeCharacter(Journal(Entry("wolves", Completed)));
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already been turned in");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TurnIn_IncompleteObjectives_Fails()
    {
        var character = MakeCharacter(Journal(Entry(
            "wolves",
            objectives: new() { ["kill:Barrow Wolf"] = 3 },
            progress: new() { ["kill:Barrow Wolf"] = 1 })));
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Not every objective is complete yet.");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TurnIn_CompleteQuest_MarksItCompletedAndReturnsTheRewards()
    {
        var character = MakeCharacter(Journal(Entry(
            "wolves",
            gold: 55,
            xp: 210,
            objectives: new() { ["kill:Barrow Wolf"] = 3 },
            progress: new() { ["kill:Barrow Wolf"] = 3 })));
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Slug.Should().Be("wolves");
        result.GoldReward.Should().Be(55);
        result.XpReward.Should().Be(210);

        var persisted = QuestJournal.Parse(character.QuestBlob).Should().ContainSingle().Subject;
        persisted.Status.Should().Be(Completed);

        var dto = result.Quests.Should().ContainSingle().Subject;
        dto.Status.Should().Be(Completed);
        dto.CanTurnIn.Should().BeFalse("a turned-in quest is no longer claimable");
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TurnIn_QuestWithNoObjectives_IsCompleteByDefinition()
    {
        var character = MakeCharacter(Journal(Entry("errand", gold: 3, xp: 4)));
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "errand"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Single().Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task TurnIn_ProjectionClampsProgressToTheRequiredCount()
    {
        // A journal written by an older build may hold progress beyond the requirement.
        var character = MakeCharacter(Journal(Entry(
            "wolves",
            objectives: new() { ["kill:Barrow Wolf"] = 2 },
            progress: new() { ["kill:Barrow Wolf"] = 9 })));
        var (repo, _) = CharacterRepo(character);

        var result = await TurnInHandler(repo.Object)
            .Handle(new TurnInQuestHubCommand(character.Id, "wolves"), CancellationToken.None);

        var objective = result.Quests.Single().Objectives.Single();
        objective.Current.Should().Be(2, "the projected current count never exceeds the requirement");
        objective.Required.Should().Be(2);
        objective.Complete.Should().BeTrue();
    }

    // ── AdvanceQuestObjectivesHubCommandHandler ───────────────────────────────

    [Theory]
    [InlineData("", "wolf")]
    [InlineData("kill", "")]
    [InlineData(" ", " ")]
    public async Task Advance_WithoutKindOrKey_Fails(string kind, string key)
    {
        var character = MakeCharacter();
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, kind, key), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kind and key are required");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Advance_UnknownCharacter_ReportsTheMissingCharacter()
    {
        var result = await AdvanceHandler(MissingCharacterRepo().Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(Guid.NewGuid(), "kill", "wolf"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Advance_NoMatchingObjective_SucceedsWithoutWriting()
    {
        var character = MakeCharacter(Journal(Entry("wolves", objectives: new() { ["kill:Barrow Wolf"] = 3 })));
        var before = character.QuestBlob;
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, "collect", "wolf-pelt"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.QuestsChanged.Should().Be(0);
        character.QuestBlob.Should().Be(before, "nothing changed, so nothing is rewritten");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Advance_MatchingObjective_IncrementsAndPersists()
    {
        var character = MakeCharacter(Journal(Entry("wolves", objectives: new() { ["kill:Barrow Wolf"] = 3 })));
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, "kill", "Barrow Wolf"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.QuestsChanged.Should().Be(1);
        QuestJournal.Parse(character.QuestBlob).Single()
            .Progress["kill:Barrow Wolf"].Should().Be(1);

        var objective = result.Quests.Single().Objectives.Single();
        objective.Current.Should().Be(1);
        objective.Complete.Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Advance_RespectsTheAmountAndCapsAtTheRequirement()
    {
        var character = MakeCharacter(Journal(Entry("wolves", objectives: new() { ["kill:Barrow Wolf"] = 2 })));
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, "kill", "Barrow Wolf", 5), CancellationToken.None);

        result.QuestsChanged.Should().Be(1);
        result.Quests.Single().Objectives.Single().Current.Should().Be(2);
        result.Quests.Single().CanTurnIn.Should().BeTrue();
        QuestJournal.Parse(character.QuestBlob).Single().IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Advance_SkipsCompletedQuests()
    {
        var character = MakeCharacter(Journal(Entry(
            "wolves",
            Completed,
            objectives: new() { ["kill:Barrow Wolf"] = 3 })));
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, "kill", "Barrow Wolf"), CancellationToken.None);

        result.QuestsChanged.Should().Be(0);
        result.Quests.Single().CanTurnIn.Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Advance_UpdatesEveryMatchingQuestInOneCall()
    {
        var character = MakeCharacter(Journal(
            Entry("wolves", objectives: new() { ["kill:Barrow Wolf"] = 2 }),
            Entry("wolf-pelts", objectives: new() { ["kill:Barrow Wolf"] = 1 })));
        var (repo, _) = CharacterRepo(character);

        var result = await AdvanceHandler(repo.Object)
            .Handle(new AdvanceQuestObjectivesHubCommand(character.Id, "kill", "Barrow Wolf"), CancellationToken.None);

        result.QuestsChanged.Should().Be(2);
        result.Quests.Should().OnlyContain(q => q.Objectives[0].Current == 1);
    }
}
