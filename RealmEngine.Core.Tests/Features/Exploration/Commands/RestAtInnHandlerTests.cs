using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
public class RestAtInnHandlerTests
{
    private static RestAtInnHandler CreateHandler(
        Mock<ExplorationService>? explorationSvc = null,
        Mock<IGameStateService>? gameState = null,
        Mock<ISaveGameService>? saveSvc = null) =>
        new(
            (explorationSvc ?? new Mock<ExplorationService>()).Object,
            (gameState ?? new Mock<IGameStateService>()).Object,
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            NullLogger<RestAtInnHandler>.Instance);

    private static Location MakeInnLocation(string id = "town-1") =>
        new() { Id = id, Name = "Riverside Inn", Description = "A cozy inn.", HasInn = true, Type = "towns" };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationNotFound()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([]);
        var handler = CreateHandler(explorationSvc);

        var result = await handler.Handle(new RestAtInnCommand("unknown", ""), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unknown");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationHasNoInn()
    {
        var location = new Location { Id = "shop-only", Name = "Trading Post", Description = "A trading post.", HasInn = false, Type = "towns" };
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([location]);

        var result = await CreateHandler(explorationSvc).Handle(
            new RestAtInnCommand("shop-only", ""), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("inn");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActivePlayer()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([MakeInnLocation()]);

        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.Player).Returns((Character?)null);

        var result = await CreateHandler(explorationSvc, gameState).Handle(
            new RestAtInnCommand("town-1", "", 10), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenPlayerCannotAffordInn()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([MakeInnLocation()]);

        var player = new Character { Name = "Pauper", Gold = 5 };
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.Player).Returns(player);

        var result = await CreateHandler(explorationSvc, gameState).Handle(
            new RestAtInnCommand("town-1", "", 20), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gold");
    }

    [Fact]
    public async Task Handle_RestoredHealthAndMana_AndDeductsGold_OnSuccess()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([MakeInnLocation()]);

        var player = new Character
        {
            Name = "Traveller",
            Health = 40,
            MaxHealth = 100,
            Mana = 10,
            MaxMana = 60,
            Gold = 50
        };
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.Player).Returns(player);

        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null); // no save = skip save

        var result = await CreateHandler(explorationSvc, gameState, saveSvc).Handle(
            new RestAtInnCommand("town-1", "", 10), default);

        result.Success.Should().BeTrue();
        player.Gold.Should().Be(40); // 50 - 10
        player.Health.Should().BeGreaterThan(40);  // restored
        player.Mana.Should().BeGreaterThan(10);    // restored
    }

    [Fact]
    public async Task Handle_SavesGame_WhenActiveSaveExists()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([MakeInnLocation()]);

        var player = new Character { Name = "Hero", Health = 50, MaxHealth = 100, Mana = 20, MaxMana = 60, Gold = 100 };
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.Player).Returns(player);

        var save = new SaveGame { PlayerName = "Hero" };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        await CreateHandler(explorationSvc, gameState, saveSvc).Handle(
            new RestAtInnCommand("town-1", "", 10), default);

        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }
}
