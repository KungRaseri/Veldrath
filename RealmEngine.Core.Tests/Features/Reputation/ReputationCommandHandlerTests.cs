using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Reputation.Commands;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Reputation;

[Trait("Category", "Feature")]
public class GainReputationHandlerTests
{
    private static ReputationService CreateReputationService() =>
        new(NullLogger<ReputationService>.Instance);

    private static GainReputationHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ReputationService? reputationSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            reputationSvc ?? CreateReputationService(),
            NullLogger<GainReputationHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new GainReputationCommand { FactionId = "guild", Amount = 100 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenFactionIdIsEmpty()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GainReputationCommand { FactionId = "", Amount = 100 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAmountIsZero()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GainReputationCommand { FactionId = "guild", Amount = 0 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithUpdatedReputation()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GainReputationCommand { FactionId = "guild", Amount = 200 }, default);

        result.Success.Should().BeTrue();
        result.CurrentPoints.Should().Be(200);
        result.NewLevel.Should().Be(ReputationLevel.Neutral); // 200 < 500 threshold
        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsLevelChanged_WhenReputationCrossesThreshold()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new GainReputationCommand { FactionId = "guild", Amount = 600 }, default);

        result.Success.Should().BeTrue();
        result.LevelChanged.Should().BeTrue();
        result.OldLevel.Should().Be(ReputationLevel.Neutral);
        result.NewLevel.Should().Be(ReputationLevel.Friendly);
    }
}

[Trait("Category", "Feature")]
public class LoseReputationHandlerTests
{
    private static ReputationService CreateReputationService() =>
        new(NullLogger<ReputationService>.Instance);

    private static LoseReputationHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        ReputationService? reputationSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            reputationSvc ?? CreateReputationService(),
            NullLogger<LoseReputationHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new LoseReputationCommand { FactionId = "guild", Amount = 100 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenFactionIdIsEmpty()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new LoseReputationCommand { FactionId = " ", Amount = 50 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAmountIsNegative()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new LoseReputationCommand { FactionId = "guild", Amount = -10 }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithReducedReputation()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new LoseReputationCommand { FactionId = "guild", Amount = 200 }, default);

        result.Success.Should().BeTrue();
        result.CurrentPoints.Should().Be(-200);
        result.NewLevel.Should().Be(ReputationLevel.Neutral); // -200 > -500 threshold
        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsLevelChanged_WhenReputationCrossesThreshold()
    {
        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new LoseReputationCommand { FactionId = "guild", Amount = 600 }, default);

        result.Success.Should().BeTrue();
        result.LevelChanged.Should().BeTrue();
        result.OldLevel.Should().Be(ReputationLevel.Neutral);
        result.NewLevel.Should().Be(ReputationLevel.Unfriendly);
    }
}
