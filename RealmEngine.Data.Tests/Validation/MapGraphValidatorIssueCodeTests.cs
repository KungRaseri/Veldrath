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
        string? refId = null,
        bool isHidden = false,
        string? unlockType = null,
        string? unlockKey = null) =>
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
            isHidden,
            unlockType,
            unlockKey,
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
        string entryNodeId = "entry",
        MapType mapType = MapType.Zone) =>
        new(id, mapType, ownerId, name, "description", entryNodeId, nodes, edges ?? []);

    private static IReadOnlyList<MapNodeEntry> MinimalGraph(string mapId = "zone:test") =>
    [
        Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: mapId),
        Node(
            "exit",
            MapNodeType.ZoneExit,
            isExit: true,
            targetMapId: "zone:other",
            targetNodeId: "entry",
            mapId: mapId),
    ];

    private static IReadOnlyList<MapEdgeEntry> MinimalEdges(string mapId = "zone:test") =>
        [Edge("entry", "exit", MapDirection.North, mapId)];

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

    // ── Unlock keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReportsAQuestGatedNodeWithNoKey()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("vault", MapNodeType.Location, isHidden: true, unlockType: "quest"),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, [Edge("entry", "vault", MapDirection.North)]));

        // Assert
        Codes(result).Should().Contain("unlock_key_missing");
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("skill_check_passive")]
    [InlineData("skill_check_active")]
    [InlineData("manual")]
    public void Validate_AcceptsAnUnlockTypeThatCarriesNoKey(string unlockType)
    {
        // Arrange — a skill check gates on the roll and DiscoverThreshold, not on a named quest or item.
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("shrine", MapNodeType.Location, isHidden: true, unlockType: unlockType),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, [Edge("entry", "shrine", MapDirection.North)]));

        // Assert
        Codes(result).Should().NotContain("unlock_key_missing");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsAQuestGatedNodeThatNamesItsKey()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("vault", MapNodeType.Location, isHidden: true, unlockType: "quest", unlockKey: "the-vault"),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, [Edge("entry", "vault", MapDirection.North)]));

        // Assert
        Codes(result).Should().NotContain("unlock_key_missing");
    }

    // ── Ways out ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WarnsAboutAZoneWithNoWayOut()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("room", MapNodeType.Location),
        };
        var edges = new List<MapEdgeEntry>
        {
            Edge("entry", "room", MapDirection.North),
            Edge("room", "entry", MapDirection.South),
        };

        // Act
        var result = MapGraphValidator.Validate(Graph(nodes, edges));

        // Assert
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "map_without_exit" && issue.Severity == MapGraphValidationSeverity.Warning);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DoesNotAskAnInteriorMapForAWayOut()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("room", MapNodeType.Location),
        };

        // Act
        var result = MapGraphValidator.Validate(
            Graph(nodes, [Edge("entry", "room", MapDirection.North)], mapType: MapType.Interior));

        // Assert
        Codes(result).Should().NotContain("map_without_exit");
    }

    // ── Interior ownership ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ReportsAnInteriorMapOwnedByAnUnknownLocation()
    {
        // Act
        var result = MapGraphValidator.Validate(
            Graph(MinimalGraph(), MinimalEdges(), ownerId: "ghost-cellar", mapType: MapType.Interior),
            knownLocationSlugs: new HashSet<string> { "real-cellar" });

        // Assert
        Codes(result).Should().Contain("interior_owner_unknown");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsAnInteriorMapOwnedByAKnownLocation()
    {
        // Act
        var result = MapGraphValidator.Validate(
            Graph(MinimalGraph(), MinimalEdges(), ownerId: "real-cellar", mapType: MapType.Interior),
            knownLocationSlugs: new HashSet<string> { "real-cellar" });

        // Assert
        Codes(result).Should().NotContain("interior_owner_unknown");
    }

    // ── World-level rules ───────────────────────────────────────────────────

    [Fact]
    public void ValidateWorld_AcceptsAWorldWhoseExitsAllPointAtRealMaps()
    {
        // Act
        var result = MapGraphValidator.ValidateWorld(
        [
            Graph(MinimalGraph(), MinimalEdges()),
            Graph(
                [
                    Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: "zone:other"),
                    Node(
                        "gate",
                        MapNodeType.ZoneExit,
                        isExit: true,
                        targetMapId: "zone:test",
                        targetNodeId: "entry",
                        mapId: "zone:other"),
                ],
                [Edge("entry", "gate", MapDirection.North, "zone:other")],
                id: "zone:other",
                ownerId: "other"),
        ]);

        // Assert
        Codes(result).Should().NotContain("target_map_unknown");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateWorld_ReportsAnExitToAMapThatDoesNotExist()
    {
        // Act
        var result = MapGraphValidator.ValidateWorld([Graph(MinimalGraph(), MinimalEdges())]);

        // Assert
        Codes(result).Should().Contain("target_map_unknown");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateWorld_ReportsAnExitToANodeTheTargetMapDoesNotHave()
    {
        // Arrange — the map exists, so this is the typo case: a plausible name that resolves to nothing.
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node(
                "exit",
                MapNodeType.ZoneExit,
                isExit: true,
                targetMapId: "zone:other",
                targetNodeId: "ghost"),
        };

        // Act
        var result = MapGraphValidator.ValidateWorld(
        [
            Graph(nodes, MinimalEdges()),
            Graph([Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: "zone:other")], id: "zone:other", ownerId: "other"),
        ]);

        // Assert
        Codes(result).Should().Contain("target_node_unknown");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateWorld_DoesNotReportABlankTargetNodeTwice()
    {
        // Arrange — a blank TargetNodeId is exit_target_missing's job, not target_node_unknown's.
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("exit", MapNodeType.ZoneExit, isExit: true, targetMapId: "zone:other"),
        };

        // Act
        var result = MapGraphValidator.ValidateWorld(
        [
            Graph(nodes, MinimalEdges()),
            Graph([Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: "zone:other")], id: "zone:other", ownerId: "other"),
        ]);

        // Assert
        Codes(result).Should().Contain("exit_target_missing").And.NotContain("target_node_unknown");
    }

    [Fact]
    public void ValidateWorld_PromotesAStrandedRoomToAnErrorInsideADungeon()
    {
        // Arrange — "island" has no edge either way, which is how the Halrow's cellars shipped.
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("island", MapNodeType.Location),
            Node("exit", MapNodeType.RegionExit, isExit: true, targetMapId: "region:test", targetNodeId: "entry"),
        };
        var edges = new List<MapEdgeEntry> { Edge("entry", "exit", MapDirection.North) };
        var world = new List<MapGraph>
        {
            Graph(nodes, edges),
            Graph(
                [
                    Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: "region:test"),
                    Node(
                        "gate",
                        MapNodeType.RegionExit,
                        isExit: true,
                        targetMapId: "zone:test",
                        targetNodeId: "entry",
                        mapId: "region:test"),
                ],
                [Edge("entry", "gate", MapDirection.North, "region:test")],
                id: "region:test",
                ownerId: "test",
                mapType: MapType.Region),
        };

        // Act
        var result = MapGraphValidator.ValidateWorld(world, dungeonZoneSlugs: new HashSet<string> { "test" });

        // Assert
        result.Issues.Should().Contain(issue =>
            issue.Code == "node_unreachable" &&
            issue.NodeId == "island" &&
            issue.Severity == MapGraphValidationSeverity.Error);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateWorld_LeavesAStrandedRoomAWarningOutsideADungeon()
    {
        // Arrange — the same graph, but no dungeon owns it.
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("island", MapNodeType.Location),
            Node("exit", MapNodeType.RegionExit, isExit: true, targetMapId: "region:test", targetNodeId: "entry"),
        };
        var edges = new List<MapEdgeEntry> { Edge("entry", "exit", MapDirection.North) };
        var world = new List<MapGraph>
        {
            Graph(nodes, edges),
            Graph(
                [
                    Node("entry", MapNodeType.EntryPoint, isEntry: true, mapId: "region:test"),
                    Node(
                        "gate",
                        MapNodeType.RegionExit,
                        isExit: true,
                        targetMapId: "zone:test",
                        targetNodeId: "entry",
                        mapId: "region:test"),
                ],
                [Edge("entry", "gate", MapDirection.North, "region:test")],
                id: "region:test",
                ownerId: "test",
                mapType: MapType.Region),
        };

        // Act
        var result = MapGraphValidator.ValidateWorld(world);

        // Assert
        result.Issues.Should().Contain(issue =>
            issue.Code == "node_unreachable" && issue.Severity == MapGraphValidationSeverity.Warning);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateWorld_ReportsAMissingWayOutAsAnErrorInsideADungeon()
    {
        // Arrange
        var nodes = new List<MapNodeEntry>
        {
            Node("entry", MapNodeType.EntryPoint, isEntry: true),
            Node("room", MapNodeType.Location),
        };
        var edges = new List<MapEdgeEntry>
        {
            Edge("entry", "room", MapDirection.North),
            Edge("room", "entry", MapDirection.South),
        };

        // Act
        var result = MapGraphValidator.ValidateWorld(
            [Graph(nodes, edges)],
            dungeonZoneSlugs: new HashSet<string> { "test" });

        // Assert
        result.Issues.Should().Contain(issue =>
            issue.Code == "map_without_exit" && issue.Severity == MapGraphValidationSeverity.Error);
        result.IsValid.Should().BeFalse();
    }
}
