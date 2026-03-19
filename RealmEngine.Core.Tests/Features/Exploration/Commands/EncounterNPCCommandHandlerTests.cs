using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

/// <summary>
/// Unit tests for EncounterNPCCommandHandler.
/// Complements NPCEncounterIntegrationTests — covers edge cases not exercised there.
/// </summary>
[Trait("Category", "Unit")]
public class EncounterNPCCommandHandlerTests
{
    private static NPC MakeNpc(string id, Dictionary<string, TraitValue>? traits = null) =>
        new() { Id = id, Name = $"NPC {id}", Traits = traits ?? [] };

    private static (EncounterNPCCommandHandler handler, Mock<ISaveGameService> mockSvc)
        CreateHandler(SaveGame? activeSave)
    {
        var mock = new Mock<ISaveGameService>();
        mock.Setup(s => s.GetCurrentSave()).Returns(activeSave);
        var handler = new EncounterNPCCommandHandler(
            mock.Object,
            NullLogger<EncounterNPCCommandHandler>.Instance);
        return (handler, mock);
    }

    // ── No active save ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var (handler, _) = CreateHandler(activeSave: null);

        var result = await handler.Handle(new EncounterNPCCommand("npc-1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── MeetNPC called ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CallsMeetNPC_WhenNpcFound()
    {
        var npc = MakeNpc("npc-1");
        var save = new SaveGame { KnownNPCs = [npc] };
        var (handler, mockSvc) = CreateHandler(save);

        await handler.Handle(new EncounterNPCCommand("npc-1"), CancellationToken.None);

        mockSvc.Verify(s => s.MeetNPC(npc), Times.Once);
    }

    // ── Quest giver ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IncludesQuestAction_WhenIsQuestGiverTraitIsTrue()
    {
        var npc = MakeNpc("quest-giver", new()
        {
            ["isQuestGiver"] = new TraitValue(true, TraitType.Boolean)
        });
        var save = new SaveGame { KnownNPCs = [npc] };
        var (handler, _) = CreateHandler(save);

        var result = await handler.Handle(new EncounterNPCCommand("quest-giver"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AvailableActions.Should().Contain("Quest");
    }

    // ── Trainer ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IncludesTrainAction_WhenIsTrainerTraitIsTrue()
    {
        var npc = MakeNpc("trainer", new()
        {
            ["isTrainer"] = new TraitValue(true, TraitType.Boolean)
        });
        var save = new SaveGame { KnownNPCs = [npc] };
        var (handler, _) = CreateHandler(save);

        var result = await handler.Handle(new EncounterNPCCommand("trainer"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AvailableActions.Should().Contain("Train");
    }

    // ── Always-present actions ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlwaysIncludesTalkAndLeave_ForAnyNpc()
    {
        var npc = MakeNpc("plain-npc");
        var save = new SaveGame { KnownNPCs = [npc] };
        var (handler, _) = CreateHandler(save);

        var result = await handler.Handle(new EncounterNPCCommand("plain-npc"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AvailableActions.Should().Contain("Talk");
        result.AvailableActions.Should().Contain("Leave");
    }
}
