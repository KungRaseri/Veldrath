using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Quest;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetQuestLogHubCommandHandler"/>.</summary>
public class GetQuestLogHubCommandHandlerTests
{
    private static GetQuestLogHubCommandHandler MakeHandler(
        ICharacterRepository charRepo,
        ISaveGameRepository saveRepo) =>
        new(charRepo, saveRepo, NullLogger<GetQuestLogHubCommandHandler>.Instance);

    private static Character MakeCharacter(Guid id, string name = "TestHero") =>
        new()
        {
            Id        = id,
            AccountId = Guid.NewGuid(),
            Name      = name,
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
            InventoryBlob = "[]",
        };

    private static SaveGame MakeSave(string playerName,
        IEnumerable<Quest>? active = null,
        IEnumerable<Quest>? completed = null,
        IEnumerable<Quest>? failed = null) =>
        new()
        {
            PlayerName     = playerName,
            ActiveQuests    = (active    ?? []).ToList(),
            CompletedQuests = (completed ?? []).ToList(),
            FailedQuests    = (failed    ?? []).ToList(),
        };

    private static Quest MakeQuest(string slug, string title = "") =>
        new() { Slug = slug, Title = string.IsNullOrEmpty(title) ? slug : title };

    // ── Guard / error tests ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterNotFound()
    {
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Character?)null);

        var result = await MakeHandler(charRepo.Object, Mock.Of<ISaveGameRepository>())
            .Handle(new GetQuestLogHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // ── No save game ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsSuccess_WithEmptyQuests_WhenNoSaveGame()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId));

        var saveRepo = new Mock<ISaveGameRepository>();
        saveRepo.Setup(r => r.GetByPlayerName(It.IsAny<string>()))
                .Returns([]);

        var result = await MakeHandler(charRepo.Object, saveRepo.Object)
            .Handle(new GetQuestLogHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().BeEmpty();
    }

    // ── Successful quest log retrieval ─────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsActiveQuests_WithActiveStatus()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, "Hero"));

        var saveRepo = new Mock<ISaveGameRepository>();
        saveRepo.Setup(r => r.GetByPlayerName("Hero"))
                .Returns([MakeSave("Hero", active: [MakeQuest("find-key", "Find the Key")])]);

        var result = await MakeHandler(charRepo.Object, saveRepo.Object)
            .Handle(new GetQuestLogHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().ContainSingle(q => q.Slug == "find-key" && q.Status == "Active");
    }

    [Fact]
    public async Task Handle_ReturnsCompletedQuests_WithCompletedStatus()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, "Hero"));

        var saveRepo = new Mock<ISaveGameRepository>();
        saveRepo.Setup(r => r.GetByPlayerName("Hero"))
                .Returns([MakeSave("Hero", completed: [MakeQuest("slain-dragon", "Slay the Dragon")])]);

        var result = await MakeHandler(charRepo.Object, saveRepo.Object)
            .Handle(new GetQuestLogHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().ContainSingle(q => q.Slug == "slain-dragon" && q.Status == "Completed");
    }

    [Fact]
    public async Task Handle_ReturnsAllStatuses_WhenMixedQuests()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, "Hero"));

        var saveRepo = new Mock<ISaveGameRepository>();
        saveRepo.Setup(r => r.GetByPlayerName("Hero"))
                .Returns(
                [
                    MakeSave("Hero",
                        active:    [MakeQuest("q-active")],
                        completed: [MakeQuest("q-done")],
                        failed:    [MakeQuest("q-fail")]),
                ]);

        var result = await MakeHandler(charRepo.Object, saveRepo.Object)
            .Handle(new GetQuestLogHubCommand(charId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Quests.Should().HaveCount(3);
        result.Quests.Should().Contain(q => q.Status == "Active");
        result.Quests.Should().Contain(q => q.Status == "Completed");
        result.Quests.Should().Contain(q => q.Status == "Failed");
    }
}
