using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Map;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Map;
using CharacterEntity = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetMapGraphHubCommandHandler"/>.</summary>
public class GetMapGraphHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string CrestfallMapId = "zone:crestfall";
    private const string EntryNodeId = "zone:crestfall:entry";
    private const string SquareNodeId = "zone:crestfall:location:crestfall-square";
    private const string SecretNodeId = "zone:crestfall:location:secret-location";
    private const string ExitNodeId = "zone:crestfall:exit:zone:the-droveway";

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterIdIsEmpty()
    {
        var (handler, mapRepo, _, _) = MakeHandler();

        var result = await handler.Handle(
            new GetMapGraphHubCommand(Guid.Empty),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Character ID");
        mapRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterIsMissing()
    {
        var (handler, _, characterRepo, _) = MakeHandler();
        characterRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CharacterEntity?)null);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("was not found");
    }

    [Fact]
    public async Task Handle_UsesCurrentMapAndCurrentNode()
    {
        var (handler, mapRepo, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Map.Should().NotBeNull();
        result.Map!.Id.Should().Be(CrestfallMapId);
        result.CurrentNodeId.Should().Be(SquareNodeId);
        result.Map.Nodes.Should().Contain(n => n.Id == SquareNodeId);
    }

    [Fact]
    public async Task Handle_FallsBackToZoneMap_WhenCharacterHasNoCurrentMap()
    {
        var (handler, mapRepo, _, _) = MakeHandler(character =>
        {
            character.CurrentMapId = null;
            character.CurrentNodeId = null;
            character.CurrentZoneId = "crestfall";
        });

        mapRepo.Setup(r => r.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Map!.Id.Should().Be(CrestfallMapId);
        result.CurrentNodeId.Should().Be(EntryNodeId);
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenMapDoesNotExist()
    {
        var (handler, mapRepo, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync("zone:missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MapGraph?)null);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId, "zone:missing"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zone:missing");
    }

    [Fact]
    public async Task Handle_FiltersHiddenNodesAndEdges_WhenNotUnlocked()
    {
        var (handler, mapRepo, _, unlockedRepo) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());
        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Map!.Nodes.Should().NotContain(n => n.Id == SecretNodeId);
        result.Map.Edges.Should().NotContain(e =>
            e.FromNodeId == SecretNodeId || e.ToNodeId == SecretNodeId);
        result.Map.Edges.Should().NotContain(e => e.IsHidden);
        result.Map.Edges.Should().ContainSingle(e =>
            e.FromNodeId == EntryNodeId && e.ToNodeId == SquareNodeId);
    }

    [Fact]
    public async Task Handle_IncludesHiddenCurrentNode()
    {
        var (handler, mapRepo, _, unlockedRepo) = MakeHandler(character =>
        {
            character.CurrentNodeId = SecretNodeId;
        });
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());
        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Map!.Nodes.Should().Contain(n => n.Id == SecretNodeId);
        result.CurrentNodeId.Should().Be(SecretNodeId);
    }

    [Fact]
    public async Task Handle_IncludesUnlockedHiddenLocation()
    {
        var (handler, mapRepo, _, unlockedRepo) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());
        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["secret-location"]);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Map!.Nodes.Should().Contain(n => n.Id == SecretNodeId);
    }

    [Fact]
    public async Task Handle_UsesEntryNode_WhenRequestingAnotherMap()
    {
        var (handler, mapRepo, _, _) = MakeHandler(character =>
        {
            character.CurrentMapId = "zone:other";
            character.CurrentNodeId = "zone:other:entry";
        });
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId, CrestfallMapId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentNodeId.Should().Be(EntryNodeId);
    }

    [Fact]
    public async Task Handle_ProjectsEnumsAsStringsAndReturnsSummaries()
    {
        var (handler, mapRepo, _, _) = MakeHandler();
        mapRepo.Setup(r => r.GetMapAsync(CrestfallMapId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGraph());
        mapRepo.Setup(r => r.GetMapsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MapSummary(
                    CrestfallMapId,
                    MapType.Zone,
                    "crestfall",
                    "Crestfall",
                    "A market town.",
                    EntryNodeId,
                    4,
                    3),
            ]);

        var result = await handler.Handle(
            new GetMapGraphHubCommand(CharacterId),
            CancellationToken.None);

        result.Map!.MapType.Should().Be("Zone");
        result.Map.Nodes.Single(n => n.Id == SquareNodeId).NodeType.Should().Be("Location");
        result.Map.Edges.Should().Contain(e => e.Direction == "North" && e.EdgeType == "Walk");
        result.AvailableMaps.Should().ContainSingle();
        result.AvailableMaps[0].MapType.Should().Be("Zone");
        result.AvailableMaps[0].NodeCount.Should().Be(4);
    }

    private static (GetMapGraphHubCommandHandler Handler,
        Mock<IMapGraphRepository> MapRepo,
        Mock<ICharacterRepository> CharacterRepo,
        Mock<ICharacterUnlockedLocationRepository> UnlockedRepo) MakeHandler(
        Action<CharacterEntity>? configure = null)
    {
        var character = new CharacterEntity
        {
            Id = CharacterId,
            CurrentMapId = CrestfallMapId,
            CurrentNodeId = SquareNodeId,
            CurrentZoneId = "crestfall",
        };

        configure?.Invoke(character);

        var mapRepo = new Mock<IMapGraphRepository>();
        var characterRepo = new Mock<ICharacterRepository>();
        var unlockedRepo = new Mock<ICharacterUnlockedLocationRepository>();

        characterRepo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        unlockedRepo.Setup(r => r.GetUnlockedSlugsAsync(character.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mapRepo.Setup(r => r.GetMapsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetMapGraphHubCommandHandler(
            mapRepo.Object,
            characterRepo.Object,
            unlockedRepo.Object,
            NullLogger<GetMapGraphHubCommandHandler>.Instance);

        return (handler, mapRepo, characterRepo, unlockedRepo);
    }

    private static MapGraph MakeGraph()
    {
        var entry = Node(
            EntryNodeId,
            MapNodeType.EntryPoint,
            refId: null,
            displayName: "Arrival",
            isEntryPoint: true,
            sortOrder: 0,
            layoutX: 10,
            layoutY: 8);

        var square = Node(
            SquareNodeId,
            MapNodeType.Location,
            refId: "crestfall-square",
            displayName: "The Crestfall Square",
            sortOrder: 20,
            layoutX: 10,
            layoutY: 10);

        var secret = Node(
            SecretNodeId,
            MapNodeType.Location,
            refId: "secret-location",
            displayName: "The Secret Cellar",
            sortOrder: 30,
            layoutX: 14,
            layoutY: 10,
            isHidden: true,
            unlockType: "skill_check_passive",
            discoverThreshold: 5);

        var exit = Node(
            ExitNodeId,
            MapNodeType.ZoneExit,
            refId: "the-droveway",
            displayName: "Road to The Droveway",
            isExitPoint: true,
            targetMapId: "zone:the-droveway",
            targetNodeId: "zone:the-droveway:entry",
            sortOrder: 40,
            layoutX: 10,
            layoutY: 2);

        return new MapGraph(
            CrestfallMapId,
            MapType.Zone,
            "crestfall",
            "Crestfall",
            "A prosperous crossroads market town.",
            EntryNodeId,
            [entry, square, secret, exit],
            [
                Edge("entry-square", entry.Id, square.Id, MapDirection.North),
                Edge("square-secret", square.Id, secret.Id, MapDirection.East),
                Edge("square-exit", square.Id, exit.Id, MapDirection.North),
                Edge("secret-square", secret.Id, square.Id, MapDirection.West, isHidden: true),
            ]);
    }

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
        string? unlockType = null,
        int? discoverThreshold = null,
        int sortOrder = 0,
        int? layoutX = null,
        int? layoutY = null) =>
        new(
            id,
            CrestfallMapId,
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
            unlockType,
            UnlockKey: null,
            DiscoverThreshold: discoverThreshold,
            SortOrder: sortOrder);

    private static MapEdgeEntry Edge(
        string key,
        string fromNodeId,
        string toNodeId,
        MapDirection direction,
        bool isHidden = false) =>
        new(
            Guid.NewGuid(),
            CrestfallMapId,
            fromNodeId,
            toNodeId,
            direction,
            MapEdgeType.Walk,
            Label: key,
            IsTraversable: true,
            IsHidden: isHidden);
}
