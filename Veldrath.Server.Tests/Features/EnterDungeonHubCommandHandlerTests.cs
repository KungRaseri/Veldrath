using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the dungeon command as an alias of the location-entry command: it resolves the dungeon's
/// entrance and hands the move to <see cref="EnterZoneLocationHubCommand"/>, so the only thing worth
/// asserting here is which entrance it picks and that a refusal is passed through unchanged.
/// </summary>
public class EnterDungeonHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.NewGuid();

    private static Zone LocationZone(ZoneType type = ZoneType.Dungeon) => new()
    {
        Id = "the-halrow",
        Name = "The Halrow",
        Description = "A buried estate.",
        Type = type,
        RegionId = "varenmark",
    };

    private static ZoneLocationEntry Location(
        string slug,
        string typeKey = "dungeons",
        bool isSafeRespawn = false) =>
        new(
            slug,
            slug,
            typeKey,
            "the-halrow",
            100,
            MinLevel: 4,
            MaxLevel: 6,
            IsSafeRespawn: isSafeRespawn);

    private static EnterDungeonHubCommandHandler Handler(
        Zone? zone,
        IReadOnlyList<ZoneLocationEntry> locations,
        IMediator mediator)
    {
        var zoneRepo = new Mock<IZoneRepository>();
        zoneRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(zone);

        var locationRepo = new Mock<IZoneLocationRepository>();
        locationRepo
            .Setup(repo => repo.GetByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync(locations.ToList());

        return new EnterDungeonHubCommandHandler(
            zoneRepo.Object,
            locationRepo.Object,
            mediator,
            NullLogger<EnterDungeonHubCommandHandler>.Instance);
    }

    private static IMediator Arriving(TravelToNodeHubResult arrival)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<EnterZoneLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterZoneLocationHubResult { Success = true, Arrival = arrival });
        return mediator.Object;
    }

    private static IMediator Refusing(string message)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<EnterZoneLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterZoneLocationHubResult { Success = false, ErrorMessage = message });
        return mediator.Object;
    }

    [Fact]
    public async Task Handle_Fails_When_The_Slug_Is_Empty()
    {
        var handler = Handler(LocationZone(), [], Arriving(new TravelToNodeHubResult()));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "  "), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Zone_Does_Not_Exist()
    {
        var handler = Handler(null, [], Arriving(new TravelToNodeHubResult()));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "nowhere"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Zone_Is_Not_A_Dungeon()
    {
        var handler = Handler(LocationZone(ZoneType.Town), [], Arriving(new TravelToNodeHubResult()));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "the-halrow"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a dungeon");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Dungeon_Has_No_Entrance()
    {
        var handler = Handler(LocationZone(), [], Arriving(new TravelToNodeHubResult()));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "the-halrow"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no entrance");
    }

    [Fact]
    public async Task Handle_Enters_Through_The_Safe_Respawn_Room()
    {
        var arrival = new TravelToNodeHubResult
        {
            Success = true,
            LocationSlug = "halrow-threshold",
            RecommendedLevel = 11,
            YourLevel = 3,
            LevelWarning = "This place expects level 11; you are 3.",
        };

        var handler = Handler(
            LocationZone(),
            [Location("rootbound-cellars"), Location("halrow-threshold", "locations", isSafeRespawn: true)],
            Arriving(arrival));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "the-halrow"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DungeonId.Should().Be("the-halrow");
        result.LocationSlug.Should().Be("halrow-threshold");
        result.RecommendedLevel.Should().Be(11);
        result.YourLevel.Should().Be(3);
        result.LevelWarning.Should().Contain("level 11");
    }

    [Fact]
    public async Task Handle_Falls_Back_To_A_Non_Dungeon_Room_When_No_Lobby_Is_Flagged()
    {
        var handler = Handler(
            LocationZone(),
            [Location("rootbound-cellars"), Location("halrow-threshold", "locations")],
            Arriving(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "the-halrow"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be("halrow-threshold");
    }

    [Fact]
    public async Task Handle_Passes_A_Refusal_Through_Unchanged()
    {
        var handler = Handler(
            LocationZone(),
            [Location("halrow-threshold", "locations", isSafeRespawn: true)],
            Refusing("Travel to The Halrow first; The Halrow Threshold is the way in."));

        var result = await handler.Handle(new EnterDungeonHubCommand(CharacterId, "the-halrow"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Travel to The Halrow first");
    }
}
