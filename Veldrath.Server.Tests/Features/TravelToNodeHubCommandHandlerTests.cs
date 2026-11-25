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
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Tests.Infrastructure;
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
    private const string GrevenmireMapId = "zone:grevenmire";
    private const string MireExitNodeId = "zone:crestfall:exit:zone:grevenmire";
    private const string MireCrossingNodeId = "zone:grevenmire:location:mire-crossing";

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
    public async Task Handle_Keys_A_Cross_Zone_Arrival_To_The_Destination_Zones_Scope()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, mapRepo, _, _, _, zoneRepo, locationRepo, _) = MakeHandler(enemyStore: store);

        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, SquareNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(SquareNodeId, MireExitNodeId)]);
        mapRepo.Setup(r => r.GetMapAsync(GrevenmireMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GrevenmireGraph());
        zoneRepo.Setup(r => r.GetByIdAsync("grevenmire"))
            .ReturnsAsync(new Zone
            {
                Id = "grevenmire",
                Name = "Grevenmire",
                RegionId = "varenmark",
                Type = ZoneType.Wilderness,
            });
        locationRepo.Setup(r => r.GetBySlugAsync("mire-crossing"))
            .ReturnsAsync(new ZoneLocationEntry(
                "mire-crossing",
                "The Mire Crossing",
                "locations",
                "grevenmire",
                100,
                null,
                null,
                ActorPool: [new ActorPoolEntry("bog-lurker", 1)]));

        // The caller walked out of Crestfall, so its connection is still scoped to the town's group. That group used
        // to key the destination's roster, leaving the same enemies stored under both zones' groups at once.
        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, MireExitNodeId, "zone:crestfall"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ZoneId.Should().Be("grevenmire");
        result.ZoneGroup.Should().Be("zone:grevenmire_normal", "a wilderness scope carries the character's difficulty");
        store.RosterRequests.Should().Equal(("zone:grevenmire_normal", "mire-crossing"));
    }

    [Fact]
    public async Task Handle_Keeps_The_Zones_Own_Scope_For_A_Walk_Inside_It()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, mapRepo, _, _, _, _, locationRepo, _) = MakeHandler(character => character.CurrentNodeId = EntryNodeId, store);
        mapRepo.Setup(r => r.GetOutgoingEdgesAsync(
                ZoneMapId, EntryNodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Edge(EntryNodeId, SquareNodeId)]);

        // Crestfall is a town, so its shared scope has no difficulty suffix even though the character arrived with a
        // group that carries one: the destination zone decides the shape.
        locationRepo.Setup(r => r.GetBySlugAsync("crestfall-square"))
            .ReturnsAsync(new ZoneLocationEntry(
                "crestfall-square",
                "The Crestfall Square",
                "locations",
                "crestfall",
                100,
                null,
                null,
                ActorPool: [new ActorPoolEntry("town-drunk", 1)]));

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SquareNodeId, "zone:crestfall_normal"),
            CancellationToken.None);

        result.ZoneGroup.Should().Be("zone:crestfall");
        store.RosterRequests.Should().Equal(("zone:crestfall", "crestfall-square"));
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
    public async Task Handle_Reports_The_Soft_Gate_Without_Refusing_Entry()
    {
        var (handler, _, _, _, _, _, locationRepo, _) = MakeHandler(character => character.Level = 3);
        locationRepo.Setup(r => r.GetBySlugAsync("crestfall-square"))
            .ReturnsAsync(new ZoneLocationEntry(
                "crestfall-square",
                "The Crestfall Square",
                "dungeons",
                "crestfall",
                100,
                12,
                20,
                IsInstanced: true));

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SquareNodeId),
            CancellationToken.None);

        // Standing in the square already: the gate still has to be reported, because the party action
        // enters the location you are standing in.
        result.Success.Should().BeTrue("a level gate is advice, never a refusal");
        result.RecommendedLevel.Should().Be(12);
        result.YourLevel.Should().Be(3);
        result.LevelWarning.Should().Contain("expects level 12");
        result.LocationTypeKey.Should().Be("dungeons");
        result.IsDungeonLocation.Should().BeTrue();
        result.IsInstanced.Should().BeTrue("the location is flagged instanced");
        result.ZoneInstanceId.Should().NotBeNull("an instanced arrival belongs to a run");
        result.ZoneGroup.Should().StartWith("zone:crestfall#i1:");
    }

    [Fact]
    public async Task Handle_Reports_No_Gate_When_Neither_Location_Nor_Zone_Sets_A_Level()
    {
        var (handler, _, _, _, _, _, locationRepo, _) = MakeHandler();

        locationRepo.Setup(r => r.GetBySlugAsync("crestfall-square"))
            .ReturnsAsync(new ZoneLocationEntry(
                "crestfall-square",
                "The Crestfall Square",
                "locations",
                "crestfall",
                100,
                null,
                null));

        var result = await handler.Handle(
            new TravelToNodeHubCommand(CharacterId, ZoneMapId, SquareNodeId),
            CancellationToken.None);

        result.RecommendedLevel.Should().BeNull();
        result.LevelWarning.Should().BeNull();
        result.IsDungeonLocation.Should().BeFalse();
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
        Action<CharacterEntity>? configure = null,
        FakeEnemyInstanceStore? enemyStore = null)
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
                Type = ZoneType.Town,
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

        // The one roster read a walk and a resume share; the test's store is the same object either way, so a
        // second arrival sees the cohort the first one spawned.
        var rosters = new LocationRosterReader(
            enemyStore ?? new Veldrath.Server.Tests.Infrastructure.FakeEnemyInstanceStore(),
            new RealmEngine.Data.Repositories.InMemoryActorInstanceRepository(),
            actorPoolResolver,
            NullLogger<LocationRosterReader>.Instance);

        var instanceService = new Mock<Veldrath.Server.Features.Zones.IZoneInstanceService>();
        instanceService
            .Setup(service => service.FindOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneInstance
            {
                ZoneId = "crestfall",
                ZoneGroup = $"zone:crestfall#i1:{Guid.NewGuid():D}",
            });

        var handler = new TravelToNodeHubCommandHandler(
            mapRepo.Object,
            characterRepo.Object,
            unlockedRepo.Object,
            sessionRepo.Object,
            zoneRepo.Object,
            locationRepo.Object,
            rosters,
            instanceService.Object,
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
                Node(
                    MireExitNodeId,
                    MapNodeType.ZoneExit,
                    "grevenmire",
                    "Road to Grevenmire",
                    isExitPoint: true,
                    targetMapId: GrevenmireMapId,
                    targetNodeId: MireCrossingNodeId,
                    layoutX: 14,
                    layoutY: 2),
            ],
            [
                Edge(EntryNodeId, SquareNodeId),
                Edge(SquareNodeId, EntryNodeId),
                Edge(SquareNodeId, SecretNodeId),
                Edge(SquareNodeId, ExitNodeId),
                Edge(SquareNodeId, MireExitNodeId),
            ]);

    /// <summary>
    /// The far side of a border that is not at an entry node, so the walk ends on a location whose roster has to be
    /// keyed by the destination zone rather than by the group the caller arrived with.
    /// </summary>
    private static MapGraph GrevenmireGraph() =>
        new(
            GrevenmireMapId,
            MapType.Zone,
            "grevenmire",
            "Grevenmire",
            "A drowned lowland.",
            MireCrossingNodeId,
            [
                new MapNodeEntry(
                    MireCrossingNodeId,
                    GrevenmireMapId,
                    MapNodeType.Location,
                    "mire-crossing",
                    "The Mire Crossing",
                    "A causeway over black water.",
                    "place",
                    4,
                    4,
                    IsEntryPoint: true,
                    IsExitPoint: false,
                    TargetMapId: null,
                    TargetNodeId: null,
                    IsHidden: false,
                    UnlockType: null,
                    UnlockKey: null,
                    DiscoverThreshold: null,
                    SortOrder: 0),
            ],
            []);

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
