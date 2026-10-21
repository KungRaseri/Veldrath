using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Validation;

[Trait("Category", "Validation")]
public class MapGraphValidatorTests
{
    private static MapNodeEntry Node(
        string id,
        MapNodeType nodeType,
        bool isEntry = false,
        bool isExit = false,
        string? targetMapId = null,
        string? targetNodeId = null,
        string? refId = null) =>
        new(
            id,
            "zone:test",
            nodeType,
            refId ?? id,
            $"{id} name",
            $"{id} description",
            "Place",
            0,
            0,
            isEntry,
            isExit,
            targetMapId,
            targetNodeId,
            false,
            null,
            null,
            null,
            0);

    private static MapEdgeEntry Edge(
        string from,
        string to,
        MapDirection direction,
        string mapId = "zone:test") =>
        new(Guid.NewGuid(), mapId, from, to, direction, MapEdgeType.Walk, null, true, false);

    private static MapGraph ValidGraph(
        IReadOnlyList<MapNodeEntry>? nodes = null,
        IReadOnlyList<MapEdgeEntry>? edges = null) =>
        new(
            "zone:test",
            MapType.Zone,
            "test",
            "Test Map",
            "Test map",
            "entry",
            nodes ??
            [
                Node("entry", MapNodeType.EntryPoint, isEntry: true),
                Node("square", MapNodeType.Location),
                Node("exit", MapNodeType.ZoneExit, isExit: true, targetMapId: "zone:other", targetNodeId: "entry"),
            ],
            edges ??
            [
                Edge("entry", "square", MapDirection.North),
                Edge("square", "exit", MapDirection.East),
            ]);

    [Fact]
    public void Validate_ReturnsValid_ForConnectedGraph()
    {
        var result = MapGraphValidator.Validate(ValidGraph());

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReportsMissingEntryNode()
    {
        var graph = ValidGraph() with { EntryNodeId = "missing" };

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "entry_not_found");
    }

    [Fact]
    public void Validate_ReportsExitNodeWithoutTarget()
    {
        var graph = ValidGraph(
            nodes:
            [
                Node("entry", MapNodeType.EntryPoint, isEntry: true),
                Node("exit", MapNodeType.ZoneExit, isExit: true),
            ],
            edges:
            [
                Edge("entry", "exit", MapDirection.East),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "exit_target_missing");
    }

    [Fact]
    public void Validate_ReportsDuplicateDirectionFromNode()
    {
        var graph = ValidGraph(
            edges:
            [
                Edge("entry", "square", MapDirection.North),
                Edge("entry", "exit", MapDirection.North),
                Edge("square", "exit", MapDirection.East),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "edge_direction_duplicate");
    }

    [Fact]
    public void Validate_ReportsSelfEdge()
    {
        var graph = ValidGraph(
            edges:
            [
                Edge("entry", "entry", MapDirection.North),
                Edge("entry", "square", MapDirection.South),
                Edge("square", "exit", MapDirection.East),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "edge_self_reference");
    }

    [Fact]
    public void Validate_ReportsUnknownNodeReference()
    {
        var graph = ValidGraph(
            edges:
            [
                Edge("entry", "missing", MapDirection.North),
                Edge("entry", "square", MapDirection.South),
                Edge("square", "exit", MapDirection.East),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "edge_target_missing");
    }

    [Fact]
    public void Validate_WarnsAboutUnreachableNode()
    {
        var graph = ValidGraph(
            nodes:
            [
                Node("entry", MapNodeType.EntryPoint, isEntry: true),
                Node("square", MapNodeType.Location),
                Node("island", MapNodeType.Landmark),
                Node("exit", MapNodeType.ZoneExit, isExit: true, targetMapId: "zone:other", targetNodeId: "entry"),
            ],
            edges:
            [
                Edge("entry", "square", MapDirection.North),
                Edge("square", "exit", MapDirection.East),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "node_unreachable" && i.NodeId == "island");
    }

    [Fact]
    public void Validate_ReportsUnknownCrossContextReference()
    {
        var graph = ValidGraph(
            nodes:
            [
                Node("entry", MapNodeType.EntryPoint, isEntry: true),
                Node("zone", MapNodeType.Zone, refId: "unknown-zone"),
            ],
            edges:
            [
                Edge("entry", "zone", MapDirection.North),
            ]);

        var result = MapGraphValidator.Validate(
            graph,
            knownZoneSlugs: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "known-zone" });

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "node_reference_unknown");
    }

    [Fact]
    public void Validate_ReportsNodeMapMismatch()
    {
        var graph = ValidGraph(
            nodes:
            [
                Node("entry", MapNodeType.EntryPoint, isEntry: true),
                Node("square", MapNodeType.Location) with { MapId = "zone:other" },
            ],
            edges:
            [
                Edge("entry", "square", MapDirection.North),
            ]);

        var result = MapGraphValidator.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "node_map_mismatch");
    }
}
