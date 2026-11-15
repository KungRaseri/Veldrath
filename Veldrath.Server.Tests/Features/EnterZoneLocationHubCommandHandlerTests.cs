using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Zones;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the one location-entry path: it resolves the location's node, refuses to teleport a character
/// who is elsewhere, and delegates the move itself to <see cref="TravelToNodeHubCommand"/>.
/// </summary>
public class EnterZoneLocationHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.NewGuid();

    private static Zone Zone() => new()
    {
        Id = "the-halrow",
        Name = "The Halrow",
        Description = "A buried estate.",
        Type = ZoneType.Dungeon,
        RegionId = "varenmark",
    };

    private static MapGraph Map() => new(
        "zone:the-halrow",
        MapType.Zone,
        "the-halrow",
        "The Halrow",
        "A buried estate.",
        "zone:the-halrow:entry",
        [
            new MapNodeEntry(
                "zone:the-halrow:entry",
                "zone:the-halrow",
                MapNodeType.EntryPoint,
                null,
                "Arrival",
                "Arrival point.",
                "login",
                1,
                1,
                true,
                false,
                null,
                null,
                false,
                null,
                null,
                null,
                0),
            new MapNodeEntry(
                "zone:the-halrow:location:halrow-threshold",
                "zone:the-halrow",
                MapNodeType.Location,
                "halrow-threshold",
                "The Halrow Threshold",
                "The way in.",
                "place",
                2,
                2,
                false,
                false,
                null,
                null,
                false,
                null,
                null,
                null,
                10),
        ],
        []);

    private static ZoneLocationEntry Location(string slug, bool isSafeRespawn = false) =>
        new(slug, $"The {slug}", "locations", "the-halrow", 100, 11, 14, IsSafeRespawn: isSafeRespawn);

    private static ServerCharacter Character(string currentZoneId, string currentMapId) => new()
    {
        Id = CharacterId,
        Name = "Tester",
        Level = 3,
        CurrentZoneId = currentZoneId,
        CurrentMapId = currentMapId,
    };

    private static EnterZoneLocationHubCommandHandler Handler(
        ServerCharacter? character,
        MapGraph? map,
        IReadOnlyList<ZoneLocationEntry> zoneLocations,
        ZoneLocationEntry? location,
        IMediator mediator)
    {
        var zoneRepo = new Mock<IZoneRepository>();
        zoneRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(Zone());

        var locationRepo = new Mock<IZoneLocationRepository>();
        locationRepo
            .Setup(repo => repo.GetBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync(location);
        locationRepo
            .Setup(repo => repo.GetByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync(zoneLocations.ToList());

        var mapRepo = new Mock<IMapGraphRepository>();
        mapRepo
            .Setup(repo => repo.GetMapForZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(map);

        var characterRepo = new Mock<ICharacterRepository>();
        characterRepo
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);

        return new EnterZoneLocationHubCommandHandler(
            zoneRepo.Object,
            locationRepo.Object,
            mapRepo.Object,
            characterRepo.Object,
            mediator,
            NullLogger<EnterZoneLocationHubCommandHandler>.Instance);
    }

    private static IMediator Mover(TravelToNodeHubResult arrival)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TravelToNodeHubCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(arrival);
        return mediator.Object;
    }

    [Fact]
    public async Task Handle_Moves_The_Character_Through_The_Shared_Travel_Command()
    {
        TravelToNodeHubCommand? dispatched = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TravelToNodeHubCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TravelToNodeHubResult>, CancellationToken>((command, _) =>
                dispatched = (TravelToNodeHubCommand)command)
            .ReturnsAsync(new TravelToNodeHubResult
            {
                Success = true,
                LocationSlug = "halrow-threshold",
                RecommendedLevel = 11,
                YourLevel = 3,
                LevelWarning = "This place expects level 11; you are 3.",
            });

        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            Map(),
            [Location("halrow-threshold", isSafeRespawn: true)],
            Location("halrow-threshold", isSafeRespawn: true),
            mediator.Object);

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "halrow-threshold", "zone:the-halrow"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Arrival!.LevelWarning.Should().Contain("level 11");
        dispatched.Should().NotBeNull();
        dispatched!.MapId.Should().Be("zone:the-halrow");
        dispatched.NodeId.Should().Be("zone:the-halrow:location:halrow-threshold");
        dispatched.ZoneGroup.Should().Be("zone:the-halrow");
    }

    [Fact]
    public async Task Handle_Refuses_To_Teleport_A_Character_Who_Is_Elsewhere()
    {
        var handler = Handler(
            Character("crestfall", "zone:crestfall"),
            Map(),
            [Location("halrow-threshold", isSafeRespawn: true)],
            Location("halrow-threshold", isSafeRespawn: true),
            Mover(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "halrow-threshold"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Travel to The Halrow first");
        result.ErrorMessage.Should().Contain("The halrow-threshold");
    }

    [Fact]
    public async Task Handle_Fails_For_An_Unknown_Location()
    {
        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            Map(),
            [],
            location: null,
            Mover(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "nowhere"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("was not found");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Location_Belongs_To_Another_Zone()
    {
        var other = new ZoneLocationEntry("mire-crossing", "The Mire Crossing", "locations", "grevenmire", 100, 1, 3);

        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            Map(),
            [],
            other,
            Mover(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "mire-crossing"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is not in The Halrow");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Zone_Has_No_Map()
    {
        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            map: null,
            [Location("halrow-threshold")],
            Location("halrow-threshold"),
            Mover(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "halrow-threshold"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("has no map");
    }

    [Fact]
    public async Task Handle_Fails_When_The_Location_Has_No_Map_Node()
    {
        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            Map(),
            [Location("rootbound-cellars")],
            Location("rootbound-cellars"),
            Mover(new TravelToNodeHubResult { Success = true }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "rootbound-cellars"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("has no node");
    }

    [Fact]
    public async Task Handle_Passes_A_Travel_Refusal_Through()
    {
        var handler = Handler(
            Character("the-halrow", "zone:the-halrow"),
            Map(),
            [Location("halrow-threshold")],
            Location("halrow-threshold"),
            Mover(new TravelToNodeHubResult { Success = false, ErrorMessage = "There is no direct path" }));

        var result = await handler.Handle(
            new EnterZoneLocationHubCommand(CharacterId, "the-halrow", "halrow-threshold"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no direct path");
    }
}
