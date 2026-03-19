using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
public class GenerateEnemyForLocationHandlerTests
{
    private static EnemyGenerator MakeEnemyGenerator(Mock<IEnemyRepository>? repo = null) =>
        new((repo ?? new Mock<IEnemyRepository>()).Object, NullLogger<EnemyGenerator>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoLocationSetAndNoRequestId()
    {
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.CurrentLocation).Returns(string.Empty);

        var explorationSvc = new Mock<ExplorationService>();
        var locationGen = new Mock<LocationGenerator>();
        var handler = new GenerateEnemyForLocationHandler(
            gameState.Object,
            explorationSvc.Object,
            locationGen.Object,
            MakeEnemyGenerator(),
            NullLogger<GenerateEnemyForLocationHandler>.Instance);

        var result = await handler.Handle(new GenerateEnemyForLocationCommand(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("location");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationIdNotInKnownLocations()
    {
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.CurrentLocation).Returns("forest");

        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([]);

        var locationGen = new Mock<LocationGenerator>();
        var enemyGen = MakeEnemyGenerator(); // repo returns null → fallback fails too

        var handler = new GenerateEnemyForLocationHandler(
            gameState.Object,
            explorationSvc.Object,
            locationGen.Object,
            enemyGen,
            NullLogger<GenerateEnemyForLocationHandler>.Instance);

        var result = await handler.Handle(new GenerateEnemyForLocationCommand(), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UsesRequestLocationId_OverCurrentLocation()
    {
        var gameState = new Mock<IGameStateService>();
        gameState.SetupGet(g => g.CurrentLocation).Returns("forest");
        gameState.SetupGet(g => g.Player).Returns(new Character { Name = "Hero", Level = 5 });

        var knownLocation = new Location { Id = "dungeon-1", Name = "dungeon-1", Description = "A dark dungeon.", Type = "dungeons" };
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([knownLocation]);

        var locationGen = new Mock<LocationGenerator>();
        locationGen.Setup(g => g.GenerateLocationAppropriateEnemyAsync(knownLocation, It.IsAny<EnemyGenerator>()))
            .ReturnsAsync((Enemy?)null);

        var handler = new GenerateEnemyForLocationHandler(
            gameState.Object,
            explorationSvc.Object,
            locationGen.Object,
            MakeEnemyGenerator(),
            NullLogger<GenerateEnemyForLocationHandler>.Instance);

        // Even if the generation fails, the request's LocationId is used for lookup
        var result = await handler.Handle(
            new GenerateEnemyForLocationCommand { LocationId = "dungeon-1" }, default);

        explorationSvc.Verify(s => s.GetKnownLocationsAsync(), Times.Once);
    }
}
