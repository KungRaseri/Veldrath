using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Covers the <see cref="MapGraphValidator"/> issue codes that the primary suite does not reach:
/// missing map metadata, duplicate or unnamed nodes, entry-point flag mismatches, and edge
/// bookkeeping errors.
/// </summary>
[Trait("Category", "Validation")]
public class MapGraphValidatorIssueCodeTests
{
    private static MapNodeEntry Node(
        string id,
        MapNodeType nodeType,
        bool isEntry = false,
        bool isExit = false,
        string? displayName = "name",
        string mapId = "zone:test",
        string? targetMapId = null,
        string? targetNodeId = null,
        string? refId = null) =>
        new(
            id,
            mapId,
            nodeType,
            refId ?? id,
            displayName!,
            "description",
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
        MapDirection direction = MapDirection.North,
        string mapId = "zone:test",
        Guid? id = null) =>
        new(id ?? Guid.NewGuid(), mapId, from, to, direction, MapEdgeType.Walk, null, true, false);

    private static MapGraph Graph(
        IReadOnlyList<MapNodeEntry> nodes,
        IReadOnlyList<MapEdgeEntry>? edges = null,
        string id = "zone:test",
        string ownerId = "test",
        string name = "Test Map",
        string entryNodeId = "entry") =>
        new(id, MapType.Zone, ownerId, name, "description", entryNodeId, nodes, edges ?? []);

    private static IReadOnlyList<MapNodeEntry> MinimalGraph() =>
    [
        Node("entry", MapNodeType.EntryPoint, isEntry: true),
        Node("exit", MapNodeType.ZoneExit, isExit: true, targetMapId: "zone:other", targetNodeId: "entry"),
    ];

    private static IReadOnlyList<MapEdgeEntry> MinimalEdges() =>
        [Edge("entry", "exit", MapDirection.North)];

    private static string[] Codes(MapGraphValidationResult result) =>
        [.. result.Issues.Select(issue => issue.Code)];

    // ── Map metadata ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReportsAMissingMapId(string mapId)
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges(), id: mapId));

        // Assert
        Codes(result).Should().Contain("map_id_missing");
    }

    [Fact]
    public void Validate_ReportsAMissingMapOwner()
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges(), ownerId: ""));

        // Assert
        Codes(result).Should().Contain("map_owner_missing");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ReportsAMissingMapName()
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges(), name: ""));

        // Assert
        Codes(result).Should().Contain("map_name_missing");
    }

    [Fact]
    public void Validate_WithNoIssues_IsValid()
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges()));

        // Assert
        result.Issues.Should().BeEmpty();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOnlyWarnings_StaysValid()
    {
        // Arrange — the exit node is not flagged as an exit point
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("exit", MapNodeType.ZoneExit, isExit: false, targetMapId: "zone:other", targetNodeId: "entry"),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, MinimalEdges()));

        // Assert
        result.Issues.Should().Contain(issue => issue.Code == "exit_flag_missing" &&
                                               issue.Severity == MapGraphValidationSeverity.Warning);
        result.IsValid.Should().BeTrue();
    }

    // ── Nodes ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReportsANodeWithoutAnIdAndSkipsIt()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("", MapNodeType.Location),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        Codes(result).Should().Contain("node_id_missing");
    }

    [Fact]
    public void Validate_ReportsDuplicateNodeIds()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("dup", MapNodeType.Location),
            Node("dup", MapNodeType.Location),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        Codes(result).Should().Contain("node_duplicate");
    }

    [Fact]
    public void Validate_ReportsANodeWithoutADisplayName()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("blank", MapNodeType.Location, displayName: ""),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        Codes(result).Should().Contain("node_name_missing");
    }

    [Fact]
    public void Validate_WarnsWhenANonExitNodeIsFlaggedAsAnExitPoint()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("square", MapNodeType.Location, isExit: true),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        Codes(result).Should().Contain("exit_flag_unexpected");
    }

    [Fact]
    public void Validate_ReportsAnEntryPointNodeThatIsNotFlaggedAsEntry()
    {
        // Arrange — the node is the map entry but its IsEntryPoint flag is false
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: false),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        Codes(result).Should().Contain("entry_flag_missing");
        Codes(result).Should().Contain("entry_flag_mismatch");
    }

    // ── Entry node ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReportsAMissingEntryNodeId()
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges(), entryNodeId: ""));

        // Assert
        Codes(result).Should().Contain("entry_missing");
    }

    [Fact]
    public void Validate_ReportsAnEntryNodeIdThatDoesNotExist()
    {
        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), MinimalEdges(), entryNodeId: "ghost"));

        // Assert
        Codes(result).Should().Contain("entry_not_found");
    }

    [Fact]
    public void Validate_WarnsWhenTheEntryNodeHasTheWrongNodeType()
    {
        // Arrange — flagged as an entry point but typed as a Location
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.Location, isEntry: true),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, []));

        // Assert
        result.Issues.Should().Contain(issue => issue.Code == "entry_type_mismatch" &&
                                               issue.Severity == MapGraphValidationSeverity.Warning);
    }

    // ── Edges ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReportsADuplicateEdgeId()
    {
        // Arrange
        var sharedId = Guid.NewGuid();
        var edges = new List<MapEdgeEntry>
        {
            Edge("entry", "exit", MapDirection.North, id: sharedId),
            Edge("exit", "entry", MapDirection.South, id: sharedId),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), edges));

        // Assert
        Codes(result).Should().Contain("edge_duplicate");
    }

    [Fact]
    public void Validate_ReportsAnEdgeFromAnotherMap()
    {
        // Arrange
        var edges = new List<MapEdgeEntry> { Edge("entry", "exit", MapDirection.North, mapId: "zone:other") };

        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), edges));

        // Assert
        Codes(result).Should().Contain("edge_map_mismatch");
    }

    [Fact]
    public void Validate_ReportsAnEdgeWithAnUnknownOrigin()
    {
        // Arrange
        var edges = new List<MapEdgeEntry> { Edge("ghost", "exit", MapDirection.North) };

        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), edges));

        // Assert
        Codes(result).Should().Contain("edge_origin_missing");
    }

    [Fact]
    public void Validate_ReportsAnEdgeWithAnUnknownDestination()
    {
        // Arrange
        var edges = new List<MapEdgeEntry> { Edge("entry", "ghost", MapDirection.North) };

        // Act
        var result = MapGraphValidator.Validate(Graph(MinimalGraph(), edges));

        // Assert
        Codes(result).Should().Contain("edge_target_missing");
    }

    // ── Cross-context references ────────────────────────────────────────────

    [Fact]
    public void Validate_AcceptsKnownZoneAndLocationReferences()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("zone-node", MapNodeType.Zone, refId: "crestfall"),
            Node("loc-node", MapNodeType.Location, refId: "grey-cup"),
        };
        var edges = new List<MapEdgeEntry>
        {
            Edge("entry", "zone-node", MapDirection.North),
            Edge("zone-node", "loc-node", MapDirection.East),
        };

        // Act
        var result = MapGraphValidator.Validate(
            Graph(nodes, edges),
            knownZoneSlugs: new HashSet<string> { "crestfall" },
            knownLocationSlugs: new HashSet<string> { "grey-cup" });

        // Assert
        Codes(result).Should().NotContain("node_reference_unknown");
    }

    [Fact]
    public void Validate_ReportsAnUnknownLocationReference()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("loc-node", MapNodeType.Location, refId: "nowhere"),
        };

        // Act
        var result = MapGraphValidator.Validate(
            Graph(nodes, [Edge("entry", "loc-node", MapDirection.North)]),
            knownLocationSlugs: new HashSet<string> { "grey-cup" });

        // Assert
        Codes(result).Should().Contain("node_reference_unknown");
    }

    [Fact]
    public void Validate_ReportsAnUnknownRegionExitReference()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node(
                "region-exit",
                MapNodeType.RegionExit,
                isExit: true,
                targetMapId: "region:other",
                targetNodeId: "entry",
                refId: "unknown-region"),
        };

        // Act
        var result = MapGraphValidator.Validate(
            Graph(nodes, [Edge("entry", "region-exit", MapDirection.North)]),
            knownRegionSlugs: new HashSet<string> { "varenmark" });

        // Assert
        Codes(result).Should().Contain("node_reference_unknown");
    }

    [Fact]
    public void Validate_IgnoresReferencesWhenNoKnownSlugsAreSupplied()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("loc-node", MapNodeType.Location, refId: "anything-goes"),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, [Edge("entry", "loc-node", MapDirection.North)]));

        // Assert
        Codes(result).Should().NotContain("node_reference_unknown");
    }
}
