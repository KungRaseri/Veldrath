using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Map;
using CharacterEntity = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="TravelToNodeHubCommandHandler"/>.</summary>
public class TravelToNodeHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string ZoneMapId = "zone:crestfall";
    private const string TargetZoneMapId = "zone:the-droveway";
    private const string EntryNodeId = "zone:crestfall:entry";
    private const string SquareNodeId = "zone:crestfall:location:crestfall-square";
    private const string SecretNodeId = "zone:crestfall:location:secret-location";
    private const string ExitNodeId = "zone:crestfall:exit:zone:the-droveway";
    private const string TargetEntryNodeId = "zone:the-droveway:entry";

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterIdIsEmpty()
    {
        var (handler, _, _, _, _, _, _, _) = MakeHandler();

        var result = await handler.Handle(
            new TravelToNodeHubCommand(Guid.Empty, ZoneMapId, EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Character ID");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterIsMissing()
    {
        var (handler, _, characterRepo, _, _, _, _, _) = MakeHandler();
        characterRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CharacterEntity?)null);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("was not found");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenMapIsMissing()
    {
        var (handler, mapRepo, _, _, _, _, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync("zone:missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MapGraph?)null);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, "zone:missing", EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zone:missing");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterIsOnAnotherMap()
    {
        var (handler, _, _, _, _, _, _, _) = MakeHandler(character =>
        {
            character.CurrentMapId = "zone:other";
            character.CurrentNodeId = "zone:other:entry";
        });

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not on the requested map");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenThereIsNoDirectEdge()
    {
        var (handler, mapRepo, _, _, _, _, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no direct path");
    }

    [Fact]
    public async Task Handle_TravelsToAdjacentNode_AndPersistsPosition()
    {
        var (handler, mapRepo, characterRepo, _, sessionRepo, _, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(SquareNodeId, EntryNodeId)]);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, EntryNodeId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MapId.Should().Be(ZoneMapId);
        result.NodeId.Should().Be(EntryNodeId);
        result.NodeType.Should().Be("EntryPoint");
        result.MapChanged.Should().BeFalse();
        result.ZoneId.Should().Be("crestfall");
        result.RegionId.Should().Be("varenmark");

        characterRepo.Verify(r => r.UpdateMapPositionAsync(
            CharacterId,
            ZoneMapId,
            EntryNodeId,
            "crestfall",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        sessionRepo.Verify(r => r.UpdateMapPositionAsync(
            CharacterId,
            ZoneMapId,
            EntryNodeId,
            "crestfall",
            "varenmark"), Times.Once);
    }

    [Fact]
    public async Task Handle_TravelsToUnlockedLocation_AndRunsArrivalEffects()
    {
        var (handler, mapRepo, characterRepo, unlockedRepo, _, _, locationRepo, _) = MakeHandler();
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(SquareNodeId, SecretNodeId)]);
        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["secret-location"]);
        unlockedRepo.Setup(r => r.AddUnlockAsync(
                CharacterId, "passive-location", "skill_check_passive", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        locationRepo.Setup(r => r.GetHiddenByZoneIdAsync("crestfall"))
            .ReturnsAsync(
            [
                new ZoneLocationEntry(
                    "passive-location",
                    "The Passive Path",
                    "locations",
                    "crestfall",
                    50,
                    null,
                    null,
                    IsHidden: true,
                    UnlockType: "skill_check_passive",
                    DiscoverThreshold: 3),
            ]);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SecretNodeId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NodeId.Should().Be(SecretNodeId);
        result.NodeType.Should().Be("Location");
        result.LocationSlug.Should().Be("secret-location");
        result.PassiveDiscoveries.Should().ContainSingle(d => d.Slug == "passive-location");

        characterRepo.Verify(r => r.UpdateMapPositionAsync(
            CharacterId,
            ZoneMapId,
            SecretNodeId,
            "crestfall",
            "secret-location",
            It.IsAny<CancellationToken>()), Times.Once);

        characterRepo.Verify(r => r.UpdateTilePositionAsync(
            CharacterId, 14, 10, "crestfall", It.IsAny<CancellationToken>()), Times.Once);

        unlockedRepo.Verify(r => r.AddUnlockAsync(
            CharacterId, "passive-location", "skill_check_passive", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TransitionsThroughExitNode_ToTargetMap()
    {
        var (handler, mapRepo, characterRepo, _, sessionRepo, zoneRepo, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(SquareNodeId, ExitNodeId)]);
        mapRepo.Setup(r => r.GetMapAsync(TargetZoneMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TargetZoneGraph());
        zoneRepo.Setup(r => r.GetByIdAsync("the-droveway"))
            .ReturnsAsync(new Zone
            {
                Id = "the-droveway",
                Name = "The Droveway",
                RegionId = "varenmark",
            });

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, ExitNodeId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MapChanged.Should().BeTrue();
        result.MapId.Should().Be(TargetZoneMapId);
        result.NodeId.Should().Be(TargetEntryNodeId);
        result.Message.Should().Contain("The Droveway");

        characterRepo.Verify(r => r.UpdateMapPositionAsync(
            CharacterId,
            TargetZoneMapId,
            TargetEntryNodeId,
            "the-droveway",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        sessionRepo.Verify(r => r.UpdateMapPositionAsync(
            CharacterId,
            TargetZoneMapId,
            TargetEntryNodeId,
            "the-droveway",
            "varenmark"), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenHiddenDestinationIsNotDiscovered()
    {
        var (handler, mapRepo, characterRepo, _, _, _, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(SquareNodeId, SecretNodeId)]);

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SecretNodeId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not been discovered");
        characterRepo.Verify(r => r.UpdateMapPositionAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenAlreadyAtNode()
    {
        var (handler, mapRepo, characterRepo, _, _, _, _, _) = MakeHandler();

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SquareNodeId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NodeId.Should().Be(SquareNodeId);
        result.MapChanged.Should().BeFalse();

        mapRepo.Verify(r => r.GetOutgoingEdgesAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        characterRepo.Verify(r => r.UpdateMapPositionAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (TravelToNodeHubCommandHandler Handler,
        Mock<IMapGraphRepository> MapRepo,
        Mock<ICharacterRepository> CharacterRepo,
        Mock<ICharacterUnlockedLocationRepository> UnlockedRepo,
        Mock<IPlayerSessionRepository> SessionRepo,
        Mock<IZoneRepository> ZoneRepo,
        Mock<IZoneLocationRepository> LocationRepo,
        Mock<IEnemyRepository> EnemyRepo) MakeHandler(
        Action<CharacterEntity>? configure = null)
    {
        var character = new CharacterEntity
        {
            Id = CharacterId,
            Level = 5,
            CurrentMapId = ZoneMapId,
            CurrentNodeId = SquareNodeId,
            CurrentZoneId = "crestfall",
        };

        configure?.Invoke(character);

        var mapRepo = new Mock<IMapGraphRepository>();
        var characterRepo = new Mock<ICharacterRepository>();
        var unlockedRepo = new Mock<ICharacterUnlockedLocationRepository>();
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var zoneRepo = new Mock<IZoneRepository>();
        var locationRepo = new Mock<IZoneLocationRepository>();
        var enemyRepo = new Mock<IEnemyRepository>();

        mapRepo.Setup(r => r.GetMapAsync(ZoneMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZoneGraph());
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        characterRepo.Setup(r => r.GetByIdAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        characterRepo.Setup(r => r.UpdateMapPositionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        characterRepo.Setup(r => r.UpdateTilePositionAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        unlockedRepo.Setup(r => r.AddUnlockAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        sessionRepo.Setup(r => r.UpdateMapPositionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        zoneRepo.Setup(r => r.GetByIdAsync("crestfall"))
            .ReturnsAsync(new Zone
            {
                Id = "crestfall",
                Name = "Crestfall",
                RegionId = "varenmark",
            });

        locationRepo.Setup(r => r.GetHiddenByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        locationRepo.Setup(r => r.GetBySlugAsync("secret-location"))
            .ReturnsAsync(new ZoneLocationEntry(
                "secret-location",
                "The Secret Cellar",
                "locations",
                "crestfall",
                50,
                null,
                null));

        enemyRepo.Setup(r => r.GetByFamilyAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var actorPoolResolver = new ActorPoolResolver(
            new EnemyGenerator(enemyRepo.Object, NullLogger<EnemyGenerator>.Instance));

        var handler = new TravelToNodeHubCommandHandler(
            mapRepo.Object,
            characterRepo.Object,
            unlockedRepo.Object,
            sessionRepo.Object,
            zoneRepo.Object,
            locationRepo.Object,
            actorPoolResolver,
            NullLogger<TravelToNodeHubCommandHandler>.Instance);

        return (handler, mapRepo, characterRepo, unlockedRepo, sessionRepo, zoneRepo, locationRepo, enemyRepo);
    }

    private static MapGraph ZoneGraph() =>
        new(
            ZoneMapId,
            MapType.Zone,
            "crestfall",
            "Crestfall",
            "A prosperous crossroads market town.",
            EntryNodeId,
            [
                Node(EntryNodeId, MapNodeType.EntryPoint, null, "Arrival", isEntryPoint: true, layoutX: 10, layoutY: 8),
                Node(SquareNodeId, MapNodeType.Location, "crestfall-square", "The Crestfall Square", layoutX: 10, layoutY: 10),
                Node(SecretNodeId, MapNodeType.Location, "secret-location", "The Secret Cellar", isHidden: true, layoutX: 14, layoutY: 10),
                Node(
                    ExitNodeId,
                    MapNodeType.ZoneExit,
                    "the-droveway",
                    "Road to The Droveway",
                    isExitPoint: true,
                    targetMapId: TargetZoneMapId,
                    targetNodeId: TargetEntryNodeId,
                    layoutX: 10,
                    layoutY: 2),
            ],
            [
                Edge(EntryNodeId, SquareNodeId),
                Edge(SquareNodeId, EntryNodeId),
                Edge(SquareNodeId, SecretNodeId),
                Edge(SquareNodeId, ExitNodeId),
            ]);

    private static MapGraph TargetZoneGraph() =>
        new(
            TargetZoneMapId,
            MapType.Zone,
            "the-droveway",
            "The Droveway",
            "An old cattle track.",
            TargetEntryNodeId,
            [
                Node(TargetEntryNodeId, MapNodeType.EntryPoint, null, "Arrival", isEntryPoint: true, layoutX: 4, layoutY: 4),
            ],
            []);

    private static MapNodeEntry Node(
        string id,
        MapNodeType nodeType,
        string? refId,
        string displayName,
        bool isEntryPoint = false,
        bool isExitPoint = false,
        string? targetMapId = null,
        string? targetNodeId = null,
        bool isHidden = false,
        int? layoutX = null,
        int? layoutY = null) =>
        new(
            id,
            ZoneMapId,
            nodeType,
            refId,
            displayName,
            $"{displayName} description.",
            "place",
            layoutX,
            layoutY,
            isEntryPoint,
            isExitPoint,
            targetMapId,
            targetNodeId,
            isHidden,
            UnlockType: isHidden ? "skill_check_passive" : null,
            UnlockKey: null,
            DiscoverThreshold: isHidden ? 5 : null,
            SortOrder: 0);

    private static MapEdgeEntry Edge(string fromNodeId, string toNodeId) =>
        new(
            Guid.NewGuid(),
            ZoneMapId,
            fromNodeId,
            toNodeId,
            MapDirection.North,
            MapEdgeType.Walk,
            Label: null,
            IsTraversable: true,
            IsHidden: false);
}
