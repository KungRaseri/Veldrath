using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Reputation.Queries;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Reputation.Queries;

/// <summary>
/// Tests for <see cref="GetReputationHandler"/>: the no-save failure, the single-faction lookup
/// (which creates a neutral standing on demand) and the all-factions projection with its derived flags.
/// </summary>
[Trait("Category", "Feature")]
public class GetReputationHandlerTests
{
    private static GetReputationHandler CreateHandler(SaveGame? save)
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new GetReputationHandler(saveSvc.Object, new ReputationService(NullLogger<ReputationService>.Instance));
    }

    private static SaveGame CreateSave(params (string FactionId, int Points)[] standings)
    {
        var save = new SaveGame { PlayerName = "Hero" };
        foreach (var (factionId, points) in standings)
        {
            save.FactionReputations[factionId] = new ReputationStanding
            {
                FactionId = factionId,
                ReputationPoints = points
            };
        }
        return save;
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenThereIsNoActiveGame()
    {
        var result = await CreateHandler(null).Handle(new GetReputationQuery(), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("No active game found.");
        result.Reputations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllReputations_ProjectedWithDerivedFlags()
    {
        var save = CreateSave(("guild", 0), ("cult", -4000), ("order", 500));

        var result = await CreateHandler(save).Handle(new GetReputationQuery(), default);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Retrieved 3 faction reputation(s).");
        result.Reputations.Select(r => r.FactionId).Should().BeEquivalentTo(["guild", "cult", "order"]);

        var cult = result.Reputations.Single(r => r.FactionId == "cult");
        cult.Points.Should().Be(-4000);
        cult.Level.Should().Be(save.FactionReputations["cult"].Level);
        cult.IsHostile.Should().BeTrue("deeply negative reputation is hostile");

        var order = result.Reputations.Single(r => r.FactionId == "order");
        order.CanTrade.Should().Be(save.FactionReputations["order"].CanTrade);
        order.CanAcceptQuests.Should().Be(save.FactionReputations["order"].CanAcceptQuests);
        order.PriceDiscount.Should().Be(save.FactionReputations["order"].GetPriceDiscount());
        order.PriceDiscount.Should().BeGreaterThan(0, "friendly factions discount prices");
    }

    [Fact]
    public async Task Handle_WithNoStandings_ReportsThatNothingWasFound()
    {
        var result = await CreateHandler(CreateSave()).Handle(new GetReputationQuery(), default);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("No faction reputations found.");
        result.Reputations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithFactionId_ReturnsOnlyThatFaction()
    {
        var save = CreateSave(("guild", 120), ("cult", -50));

        var result = await CreateHandler(save).Handle(new GetReputationQuery { FactionId = "guild" }, default);

        result.Success.Should().BeTrue();
        result.Reputations.Should().ContainSingle().Which.FactionId.Should().Be("guild");
        result.Reputations[0].Points.Should().Be(120);
        result.Message.Should().Be("Retrieved 1 faction reputation(s).");
    }

    [Fact]
    public async Task Handle_WithUnknownFactionId_CreatesANeutralStandingOnTheSave()
    {
        var save = CreateSave();

        var result = await CreateHandler(save).Handle(new GetReputationQuery { FactionId = "newcomers" }, default);

        result.Reputations.Should().ContainSingle().Which.FactionId.Should().Be("newcomers");
        result.Reputations[0].Points.Should().Be(0);
        result.Reputations[0].CanAcceptQuests.Should().BeTrue("neutral reputation may accept quests");
        result.Reputations[0].IsHostile.Should().BeFalse();
        save.FactionReputations.Should().ContainKey("newcomers", "the handler persists the created standing");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankFactionId_ReturnsEveryFaction(string factionId)
    {
        var save = CreateSave(("guild", 10), ("cult", -10));

        var result = await CreateHandler(save).Handle(new GetReputationQuery { FactionId = factionId }, default);

        result.Reputations.Should().HaveCount(2);
    }
}
