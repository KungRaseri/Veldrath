using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Import;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Data.Tests.Import;

[Trait("Category", "Import")]
public class TmxToMapGraphImporterTests
{
    // ── Region map fixtures ───────────────────────────────────────────────────

    [Fact]
    public void Import_BuildsRegionMapFromTmxZoneEntriesExitsAndPaths()
    {
        var catalog = RegionCatalog();
        var tmx = VarenmarkTmx();

        var result = TmxToMapGraphImporter.Import(
            catalog,
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = tmx,
                ["greymoor"] = Map(),
            });

        var region = result.Maps.Single(m => m.Id == "region:varenmark");

        region.MapType.Should().Be(MapType.Region);
        region.OwnerId.Should().Be("varenmark");
        region.EntryNodeId.Should().Be("region:varenmark:entry");
        region.Nodes.Should().HaveCount(4);

        var entry = region.Nodes.Single(n => n.Id == "region:varenmark:entry");
        entry.NodeType.Should().Be(MapNodeType.EntryPoint);
        entry.IsEntryPoint.Should().BeTrue();
        entry.LayoutX.Should().Be(13);
        entry.LayoutY.Should().Be(12);

        var crestfall = region.Nodes.Single(n => n.Id == "region:varenmark:zone:crestfall");
        crestfall.NodeType.Should().Be(MapNodeType.Zone);
        crestfall.RefId.Should().Be("crestfall");
        crestfall.DisplayName.Should().Be("Crestfall");
        crestfall.LayoutX.Should().Be(14);
        crestfall.LayoutY.Should().Be(11);

        var droveway = region.Nodes.Single(n => n.Id == "region:varenmark:zone:the-droveway");
        droveway.LayoutX.Should().Be(14);
        droveway.LayoutY.Should().Be(4);

        var exit = region.Nodes.Single(n => n.NodeType == MapNodeType.RegionExit);
        exit.Id.Should().Be("region:varenmark:exit:greymoor");
        exit.IsExitPoint.Should().BeTrue();
        exit.TargetMapId.Should().Be("region:greymoor");
        exit.TargetNodeId.Should().Be("region:greymoor:entry");

        region.Edges.Should().Contain(e =>
            e.FromNodeId == "region:varenmark:zone:the-droveway" &&
            e.ToNodeId == "region:varenmark:zone:crestfall" &&
            e.Direction == MapDirection.South &&
            e.EdgeType == MapEdgeType.Walk);

        region.Edges.Should().Contain(e =>
            e.FromNodeId == "region:varenmark:zone:crestfall" &&
            e.ToNodeId == "region:varenmark:exit:greymoor" &&
            e.Direction == MapDirection.East &&
            e.EdgeType == MapEdgeType.ExitToRegion);

        result.Report.IsValid.Should().BeTrue(Describe(result));
        result.Report.Issues.Should().Contain(i => i.Code == "node_auto_connected");
    }

    private static string Describe(MapGraphImportResult result) =>
        string.Join(" | ", result.Report.Issues.Select(i => $"{i.Severity} {i.Code}: {i.Message}"))
        + " || "
        + string.Join(" | ", result.Report.Maps.SelectMany(m =>
            m.ValidationIssues.Select(v => $"{m.MapId} {v.Severity} {v.Code}: {v.Message}")));

    [Fact]
    public void Import_AutoConnectsAllNodes_WhenRegionTmxHasNoPaths()
    {
        var tmx = Map(
            ObjectLayer(
                "zones",
                Obj("crestfall", 14, 11),
                Obj("the-droveway", 14, 4)),
            ObjectLayer("spawns", Obj("default-spawn", 13, 12)));

        var result = TmxToMapGraphImporter.Import(
            RegionCatalog(),
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = tmx,
                ["greymoor"] = Map(),
            });

        var region = result.Maps.Single(m => m.Id == "region:varenmark");

        region.Nodes.Should().NotBeEmpty();
        foreach (var node in region.Nodes)
        {
            region.Edges.Should().Contain(e =>
                e.FromNodeId == node.Id || e.ToNodeId == node.Id,
                $"node '{node.Id}' should have at least one edge");
        }

        result.Report.Issues.Should().Contain(i =>
            i.Code == "node_auto_connected" &&
            i.Severity == MapGraphImportIssueSeverity.Warning);
        result.Report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Import_ReportsUnknownZoneReference_OnRegionMap()
    {
        var tmx = Map(
            ObjectLayer("zones", Obj("missing-zone", 10, 10)),
            ObjectLayer("spawns", Obj("default-spawn", 0, 0)));

        var result = TmxToMapGraphImporter.Import(
            RegionCatalog(),
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = tmx,
                ["greymoor"] = Map(),
            });

        var region = result.Maps.Single(m => m.Id == "region:varenmark");

        result.Report.IsValid.Should().BeFalse();
        result.Report.Issues.Should().Contain(i => i.Code == "zone_catalog_missing");
        region.Nodes.Single(n => n.RefId == "missing-zone").NodeType.Should().Be(MapNodeType.Zone);
        result.Report.Maps.Single(m => m.MapId == "region:varenmark").IsValid.Should().BeFalse();
    }

    // ── Zone map fixtures ─────────────────────────────────────────────────────

    [Fact]
    public void Import_BuildsZoneMapFromLocationsAndConnectionsWithoutTmx()
    {
        var catalog = ConnectionCatalog();

        var result = TmxToMapGraphImporter.Import(
            catalog,
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = Map(),
            });

        var zone = result.Maps.Single(m => m.Id == "zone:crestfall");

        zone.MapType.Should().Be(MapType.Zone);
        zone.Nodes.Should().HaveCount(6); // entry + 3 locations + 2 exits

        var squareId = "zone:crestfall:location:crestfall-square";
        var marketId = "zone:crestfall:location:aurelian-market";
        var innId = "zone:crestfall:location:hearthbound-inn";

        zone.Nodes.Single(n => n.Id == squareId).LayoutX.Should().Be(10);
        zone.Nodes.Single(n => n.Id == squareId).LayoutY.Should().Be(10);
        zone.Nodes.Single(n => n.Id == squareId).RefId.Should().Be("crestfall-square");

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == squareId &&
            e.ToNodeId == marketId &&
            e.Direction == MapDirection.East &&
            e.EdgeType == MapEdgeType.Walk);

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == squareId &&
            e.ToNodeId == innId &&
            e.Direction == MapDirection.North &&
            e.EdgeType == MapEdgeType.Walk);

        var zoneExit = zone.Nodes.Single(n => n.NodeType == MapNodeType.ZoneExit);
        zoneExit.TargetMapId.Should().Be("zone:the-droveway");
        zoneExit.TargetNodeId.Should().Be("zone:the-droveway:entry");
        zoneExit.RefId.Should().Be("the-droveway");

        var regionExit = zone.Nodes.Single(n => n.NodeType == MapNodeType.RegionExit);
        regionExit.TargetMapId.Should().Be("region:varenmark");
        regionExit.TargetNodeId.Should().Be("region:varenmark:entry");

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == squareId &&
            e.ToNodeId == zoneExit.Id &&
            e.Direction == MapDirection.South &&
            e.EdgeType == MapEdgeType.ExitToZone);

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == squareId &&
            e.ToNodeId == regionExit.Id &&
            e.Direction == MapDirection.West &&
            e.EdgeType == MapEdgeType.ExitToRegion);

        result.Report.IsValid.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Import_UsesZoneTmxLabelsExitsSpawnAndPaths()
    {
        var catalog = new MapGraphImportCatalog(
            Regions: [new MapCatalogEntry("varenmark", "Varenmark")],
            Zones:
            [
                new MapCatalogEntry("crestfall", "Crestfall", ParentRegionId: "varenmark"),
                new MapCatalogEntry("the-droveway", "The Droveway", ParentRegionId: "varenmark"),
            ],
            Locations:
            [
                new ZoneLocationSnapshot("crestfall-square", "crestfall", "The Crestfall Square", null, null),
                new ZoneLocationSnapshot("aurelian-market", "crestfall", "The Aurelian Market", null, null),
            ],
            ZoneConnections: [],
            RegionConnections: []);

        var zoneTmx = Map(
            ObjectLayer(
                "labels",
                PointObject("The Crestfall Square", 10, 10, Prop("zoneSlug", "crestfall-square")),
                PointObject("The Aurelian Market", 14, 10, Prop("zoneSlug", "aurelian-market"))),
            ObjectLayer(
                "exits",
                Obj("to-droveway", 20, 10,
                    Prop("displayName", "Road to The Droveway"),
                    Prop("toZoneId", "the-droveway")),
                Obj("to-varenmark", 10, 2,
                    Prop("displayName", "Road to Varenmark"),
                    Prop("toRegionId", "varenmark"))),
            ObjectLayer("spawns", Obj("default-spawn", 10, 8)),
            ObjectLayer(
                "paths",
                PathObject("north-road", 10, 2, (0, 0), (0, 8)),
                PathObject("east-street", 10, 10, (0, 0), (4, 0))));

        var result = TmxToMapGraphImporter.Import(
            catalog,
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = Map(),
            },
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["crestfall"] = zoneTmx,
            });

        var zone = result.Maps.Single(m => m.Id == "zone:crestfall");

        zone.Nodes.Single(n => n.Id == "zone:crestfall:entry").LayoutX.Should().Be(10);
        zone.Nodes.Single(n => n.Id == "zone:crestfall:entry").LayoutY.Should().Be(8);

        zone.Nodes.Single(n => n.Id == "zone:crestfall:location:crestfall-square").LayoutX.Should().Be(10);
        zone.Nodes.Single(n => n.Id == "zone:crestfall:location:aurelian-market").LayoutX.Should().Be(14);

        var zoneExit = zone.Nodes.Single(n => n.NodeType == MapNodeType.ZoneExit);
        zoneExit.DisplayName.Should().Be("Road to The Droveway");
        zoneExit.TargetMapId.Should().Be("zone:the-droveway");
        zoneExit.LayoutX.Should().Be(20);
        zoneExit.LayoutY.Should().Be(10);

        var regionExit = zone.Nodes.Single(n => n.NodeType == MapNodeType.RegionExit);
        regionExit.DisplayName.Should().Be("Road to Varenmark");
        regionExit.TargetMapId.Should().Be("region:varenmark");

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == "zone:crestfall:location:crestfall-square" &&
            e.ToNodeId == regionExit.Id &&
            e.Direction == MapDirection.North &&
            e.EdgeType == MapEdgeType.ExitToRegion);

        zone.Edges.Should().Contain(e =>
            e.FromNodeId == "zone:crestfall:location:aurelian-market" &&
            e.ToNodeId == zoneExit.Id &&
            e.Direction == MapDirection.East &&
            e.EdgeType == MapEdgeType.ExitToZone);

        result.Report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Import_CopiesDiscoveryMetadataToLocationNodes()
    {
        var catalog = new MapGraphImportCatalog(
            Regions: [new MapCatalogEntry("varenmark", "Varenmark")],
            Zones: [new MapCatalogEntry("crestfall", "Crestfall", ParentRegionId: "varenmark")],
            Locations:
            [
                new ZoneLocationSnapshot(
                    "sunken-fields",
                    "crestfall",
                    "The Sunken Fields",
                    10,
                    10,
                    IsHidden: true,
                    UnlockType: "skill_check_passive",
                    UnlockKey: "perception",
                    DiscoverThreshold: 5,
                    DominantLanguageSlug: "calethic"),
            ],
            ZoneConnections: [],
            RegionConnections: []);

        var result = TmxToMapGraphImporter.Import(
            catalog,
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = Map(),
            });

        var node = result.Maps
            .Single(m => m.Id == "zone:crestfall")
            .Nodes
            .Single(n => n.Id == "zone:crestfall:location:sunken-fields");

        node.IsHidden.Should().BeTrue();
        node.UnlockType.Should().Be("skill_check_passive");
        node.UnlockKey.Should().Be("perception");
        node.DiscoverThreshold.Should().Be(5);
        node.Description.Should().Contain("Hidden until discovered.");
        node.Description.Should().Contain("Language: calethic.");
    }

    [Fact]
    public void Import_ReportsExitToUnknownZone()
    {
        var catalog = new MapGraphImportCatalog(
            Regions: [new MapCatalogEntry("varenmark", "Varenmark")],
            Zones: [new MapCatalogEntry("crestfall", "Crestfall", ParentRegionId: "varenmark")],
            Locations: [new ZoneLocationSnapshot("crestfall-square", "crestfall", "The Crestfall Square", 10, 10)],
            ZoneConnections: [],
            RegionConnections: []);

        var zoneTmx = Map(
            ObjectLayer(
                "exits",
                Obj("to-nowhere", 20, 10,
                    Prop("displayName", "Road to Nowhere"),
                    Prop("toZoneId", "nowhere"))));

        var result = TmxToMapGraphImporter.Import(
            catalog,
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["varenmark"] = Map(),
            },
            new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
            {
                ["crestfall"] = zoneTmx,
            });

        result.Report.IsValid.Should().BeFalse();
        result.Report.Issues.Should().Contain(i => i.Code == "zone_catalog_missing");
        result.Report.Issues.Should().Contain(i => i.Code == "target_map_missing");
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Import_IsDeterministic()
    {
        var catalog = ConnectionCatalog();
        var regionMaps = new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
        {
            ["varenmark"] = Map(),
        };
        var zoneMaps = new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase)
        {
            ["crestfall"] = Map(
                ObjectLayer(
                    "labels",
                    PointObject("The Crestfall Square", 10, 10, Prop("zoneSlug", "crestfall-square")),
                    PointObject("The Aurelian Market", 14, 10, Prop("zoneSlug", "aurelian-market")),
                    PointObject("The Hearthbound Inn", 10, 6, Prop("zoneSlug", "hearthbound-inn"))),
                ObjectLayer("spawns", Obj("default-spawn", 9, 10))),
        };

        var first = TmxToMapGraphImporter.Import(catalog, regionMaps, zoneMaps);
        var second = TmxToMapGraphImporter.Import(catalog, regionMaps, zoneMaps);

        first.Maps.Select(m => m.Id).Should().Equal(second.Maps.Select(m => m.Id));
        first.Maps.SelectMany(m => m.Nodes).Select(n => n.Id)
            .Should().Equal(second.Maps.SelectMany(m => m.Nodes).Select(n => n.Id));
        first.Maps.SelectMany(m => m.Edges).Select(e => e.Id)
            .Should().Equal(second.Maps.SelectMany(m => m.Edges).Select(e => e.Id));
        first.Report.NodeCount.Should().Be(second.Report.NodeCount);
        first.Report.EdgeCount.Should().Be(second.Report.EdgeCount);
    }

    // ── Real assets ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_RealVarenmarkAssets_ProducesValidGraphs()
    {
        var mapsDirectory = ResolveMapsDirectory();
        if (mapsDirectory is null)
            return; // Assets are not part of the engine test deployment; fixture tests cover the logic.

        var catalog = RealCatalog();
        var repository = new TiledFileMapRepository(
            mapsDirectory,
            NullLogger<TiledFileMapRepository>.Instance);

        var regionMaps = new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in catalog.Regions)
        {
            var map = await repository.GetByRegionIdAsync(region.Id);
            if (map is not null)
                regionMaps[region.Id] = map;
        }

        var zoneMaps = new Dictionary<string, TiledMap>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in catalog.Zones)
        {
            var map = await repository.GetByZoneIdAsync(zone.Id);
            if (map is not null)
                zoneMaps[zone.Id] = map;
        }

        var result = TmxToMapGraphImporter.Import(catalog, regionMaps, zoneMaps);

        result.Report.IsValid.Should().BeTrue(Describe(result));

        var varenmark = result.Maps.Single(m => m.Id == "region:varenmark");
        varenmark.Nodes.Should().Contain(n => n.RefId == "crestfall" && n.NodeType == MapNodeType.Zone);
        varenmark.Nodes.Should().Contain(n => n.RefId == "greymoor" && n.NodeType == MapNodeType.RegionExit);

        var crestfall = result.Maps.Single(m => m.Id == "zone:crestfall");
        crestfall.Nodes.Should().Contain(n =>
            n.RefId == "crestfall-square" && n.NodeType == MapNodeType.Location);
        crestfall.Nodes.Should().Contain(n =>
            n.RefId == "varenmark" && n.NodeType == MapNodeType.RegionExit);
        crestfall.Nodes.Should().Contain(n =>
            n.RefId == "the-droveway" && n.NodeType == MapNodeType.ZoneExit);
    }

    private static string? ResolveMapsDirectory()
    {
        var directory = AppContext.BaseDirectory;

        for (var i = 0; i < 8 && !string.IsNullOrEmpty(directory); i++)
        {
            var candidate = Path.Combine(
                directory,
                "Veldrath.Assets",
                "GameAssets",
                "tilemaps",
                "maps");

            if (Directory.Exists(candidate))
                return candidate;

            directory = Path.GetDirectoryName(
                directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return null;
    }

    private static MapGraphImportCatalog RealCatalog() =>
        new(
            Regions:
            [
                new MapCatalogEntry("varenmark", "Varenmark"),
                new MapCatalogEntry("greymoor", "Greymoor"),
                new MapCatalogEntry("saltcliff", "Saltcliff"),
                new MapCatalogEntry("cinderplain", "Cinderplain"),
            ],
            Zones:
            [
                new MapCatalogEntry("crestfall", "Crestfall", ParentRegionId: "varenmark"),
                new MapCatalogEntry("the-droveway", "The Droveway", ParentRegionId: "varenmark"),
                new MapCatalogEntry("ashlen-wood", "The Ashlen Wood", ParentRegionId: "varenmark"),
                new MapCatalogEntry("grevenmire", "Grevenmire", ParentRegionId: "varenmark"),
                new MapCatalogEntry("the-halrow", "The Halrow", ParentRegionId: "varenmark"),
                new MapCatalogEntry("drowning-pits", "The Drowning Pits", ParentRegionId: "varenmark"),
                new MapCatalogEntry("aldenmere", "Aldenmere", ParentRegionId: "greymoor"),
                new MapCatalogEntry("pale-moor", "The Pale Moor", ParentRegionId: "greymoor"),
                new MapCatalogEntry("soddenfen", "Soddenfen", ParentRegionId: "greymoor"),
                new MapCatalogEntry("barrow-deeps", "The Barrow Deeps", ParentRegionId: "greymoor"),
                new MapCatalogEntry("tolvaren", "Tolvaren", ParentRegionId: "saltcliff"),
                new MapCatalogEntry("tidewrack-flats", "The Tidewrack Flats", ParentRegionId: "saltcliff"),
                new MapCatalogEntry("saltcliff-heights", "The Saltcliff Heights", ParentRegionId: "saltcliff"),
                new MapCatalogEntry("sunken-name", "The Sunken Name", ParentRegionId: "saltcliff"),
                new MapCatalogEntry("skarhold", "Skarhold", ParentRegionId: "cinderplain"),
                new MapCatalogEntry("ashfields", "The Ashfields", ParentRegionId: "cinderplain"),
                new MapCatalogEntry("smoldering-reach", "The Smoldering Reach", ParentRegionId: "cinderplain"),
                new MapCatalogEntry("kaldrek-maw", "Kaldrek's Maw", ParentRegionId: "cinderplain"),
            ],
            Locations:
            [
                new ZoneLocationSnapshot("crestfall-square", "crestfall", "The Crestfall Square", null, null, SortOrder: 1),
                new ZoneLocationSnapshot("aurelian-market", "crestfall", "The Aurelian Market", null, null, SortOrder: 2),
                new ZoneLocationSnapshot("hearthbound-inn", "crestfall", "The Hearthbound Inn", null, null, SortOrder: 3),
                new ZoneLocationSnapshot("low-quarter", "crestfall", "The Low Quarter", null, null, SortOrder: 4),
            ],
            ZoneConnections:
            [
                new ZoneConnectionSnapshot("crestfall-square", ToLocationSlug: "aurelian-market"),
                new ZoneConnectionSnapshot("aurelian-market", ToLocationSlug: "crestfall-square"),
                new ZoneConnectionSnapshot("crestfall-square", ToLocationSlug: "hearthbound-inn"),
                new ZoneConnectionSnapshot("hearthbound-inn", ToLocationSlug: "crestfall-square"),
                new ZoneConnectionSnapshot("crestfall-square", ToLocationSlug: "low-quarter"),
                new ZoneConnectionSnapshot("low-quarter", ToLocationSlug: "crestfall-square"),
                new ZoneConnectionSnapshot("aurelian-market", ToLocationSlug: "hearthbound-inn"),
                new ZoneConnectionSnapshot("hearthbound-inn", ToLocationSlug: "aurelian-market"),
                new ZoneConnectionSnapshot("crestfall-square", ToZoneId: "the-droveway", ConnectionType: "exit"),
                new ZoneConnectionSnapshot("crestfall-square", ToZoneId: "ashlen-wood", ConnectionType: "exit"),
                new ZoneConnectionSnapshot("crestfall-square", ToRegionId: "varenmark", ConnectionType: "region_exit"),
            ],
            RegionConnections:
            [
                new RegionConnectionSnapshot("varenmark", "greymoor"),
                new RegionConnectionSnapshot("greymoor", "varenmark"),
                new RegionConnectionSnapshot("greymoor", "saltcliff"),
                new RegionConnectionSnapshot("saltcliff", "greymoor"),
                new RegionConnectionSnapshot("saltcliff", "cinderplain"),
                new RegionConnectionSnapshot("cinderplain", "greymoor"),
            ]);

    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static MapGraphImportCatalog RegionCatalog() =>
        new(
            Regions:
            [
                new MapCatalogEntry("varenmark", "Varenmark", "The oldest settled march in the lowlands."),
                new MapCatalogEntry("greymoor", "Greymoor", "Fog-wreathed highlands."),
            ],
            Zones:
            [
                new MapCatalogEntry("crestfall", "Crestfall", "A prosperous crossroads market town.", "varenmark"),
                new MapCatalogEntry("the-droveway", "The Droveway", "An old cattle track.", "varenmark"),
            ],
            Locations: [],
            ZoneConnections: [],
            RegionConnections: [new RegionConnectionSnapshot("varenmark", "greymoor")]);

    private static MapGraphImportCatalog ConnectionCatalog() =>
        new(
            Regions: [new MapCatalogEntry("varenmark", "Varenmark", "The oldest settled march in the lowlands.")],
            Zones:
            [
                new MapCatalogEntry("crestfall", "Crestfall", "A prosperous crossroads market town.", "varenmark"),
                new MapCatalogEntry("the-droveway", "The Droveway", "An old cattle track.", "varenmark"),
            ],
            Locations:
            [
                new ZoneLocationSnapshot("crestfall-square", "crestfall", "The Crestfall Square", 10, 10, SortOrder: 1),
                new ZoneLocationSnapshot("aurelian-market", "crestfall", "The Aurelian Market", 14, 10, SortOrder: 2),
                new ZoneLocationSnapshot("hearthbound-inn", "crestfall", "The Hearthbound Inn", 10, 6, SortOrder: 3),
            ],
            ZoneConnections:
            [
                new ZoneConnectionSnapshot("crestfall-square", ToLocationSlug: "aurelian-market"),
                new ZoneConnectionSnapshot("aurelian-market", ToLocationSlug: "crestfall-square"),
                new ZoneConnectionSnapshot("crestfall-square", ToLocationSlug: "hearthbound-inn"),
                new ZoneConnectionSnapshot("hearthbound-inn", ToLocationSlug: "crestfall-square"),
                new ZoneConnectionSnapshot("crestfall-square", ToZoneId: "the-droveway", ConnectionType: "exit"),
                new ZoneConnectionSnapshot("crestfall-square", ToRegionId: "varenmark", ConnectionType: "region_exit"),
            ],
            RegionConnections: []);

    private static TiledMap VarenmarkTmx() =>
        Map(
            ObjectLayer(
                "zones",
                Obj("crestfall", 14, 11, Prop("displayName", "Crestfall")),
                Obj("the-droveway", 14, 4, Prop("displayName", "The Droveway"))),
            ObjectLayer(
                "region_exits",
                Obj("greymoor", 24, 11, Prop("displayName", "Greymoor"))),
            ObjectLayer("spawns", Obj("default-spawn", 13, 12)),
            ObjectLayer(
                "paths",
                PathObject("droveway-road", 14, 5, (0, 0), (0, 6)),
                PathObject("greymoor-road", 14, 11, (0, 0), (10, 0))));

    private static TiledMap Map(params TiledLayer[] layers) =>
        new()
        {
            Width = 64,
            Height = 64,
            TileWidth = 16,
            TileHeight = 16,
            Layers = [.. layers],
        };

    private static TiledLayer ObjectLayer(string name, params TiledObject[] objects) =>
        new()
        {
            Type = "objectgroup",
            Name = name,
            Objects = [.. objects],
        };

    private static TiledObject Obj(
        string name,
        int tileX,
        int tileY,
        params TiledProperty[] properties) =>
        new()
        {
            Name = name,
            X = tileX * 16d,
            Y = tileY * 16d,
            Width = 16,
            Height = 16,
            Properties = [.. properties],
        };

    private static TiledObject PointObject(
        string name,
        int tileX,
        int tileY,
        params TiledProperty[] properties) =>
        new()
        {
            Name = name,
            X = tileX * 16d,
            Y = tileY * 16d,
            Point = true,
            Properties = [.. properties],
        };

    private static TiledObject PathObject(
        string name,
        int tileX,
        int tileY,
        params (int X, int Y)[] points) =>
        new()
        {
            Name = name,
            X = tileX * 16d,
            Y = tileY * 16d,
            Polyline = points
                .Select(p => new TiledPoint { X = p.X * 16d, Y = p.Y * 16d })
                .ToList(),
        };

    private static TiledProperty Prop(string name, string value) =>
        new() { Name = name, Value = JsonSerializer.SerializeToElement(value) };
}
