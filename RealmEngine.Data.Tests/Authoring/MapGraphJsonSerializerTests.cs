using System.Text.Json;
using FluentAssertions;
using RealmEngine.Data.Authoring;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>Tests for <see cref="MapGraphJsonSerializer"/> bulk authoring files.</summary>
public class MapGraphJsonSerializerTests
{
    private static readonly Guid EdgeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static MapGraph CreateRegionGraph() => new(
        "region:thornveil",
        MapType.Region,
        "thornveil",
        "Thornveil",
        "A forested region.",
        "region:thornveil:entry",
        [
            new MapNodeEntry(
                "region:thornveil:entry", "region:thornveil", MapNodeType.EntryPoint, null,
                "Region Entry", "Arrival point.", null, 0, 0, true, false, null, null,
                false, null, null, null, 0),
            new MapNodeEntry(
                "region:thornveil:crestfall", "region:thornveil", MapNodeType.Zone, "crestfall",
                "Crestfall", "A fortified town.", "castle", 1, 0, false, false,
                "zone:crestfall", "zone:crestfall:entry", false, null, null, null, 1),
        ],
        [
            new MapEdgeEntry(
                EdgeId, "region:thornveil",
                "region:thornveil:entry", "region:thornveil:crestfall",
                MapDirection.East, MapEdgeType.Walk, "East Road", true, false),
        ]);

    /// <summary>Verifies enums are serialized as strings and properties use camelCase.</summary>
    [Fact]
    public void Export_Uses_String_Enums_And_CamelCase()
    {
        var json = MapGraphJsonSerializer.Export([CreateRegionGraph()]);

        json.Should().Contain("\"formatVersion\": 1");
        json.Should().Contain("\"mapType\": \"Region\"");
        json.Should().Contain("\"nodeType\": \"EntryPoint\"");
        json.Should().Contain("\"direction\": \"East\"");
        json.Should().Contain("\"edgeType\": \"Walk\"");
        json.Should().Contain("\"entryNodeId\": \"region:thornveil:entry\"");
    }

    /// <summary>Verifies a map graph survives an export/import round trip.</summary>
    [Fact]
    public void ExportImport_RoundTrips_MapGraph()
    {
        var original = CreateRegionGraph();

        var imported = MapGraphJsonSerializer.Import(
            MapGraphJsonSerializer.Export([original]));

        imported.FormatVersion.Should().Be(MapGraphFile.CurrentFormatVersion);
        imported.Maps.Should().ContainSingle();
        var map = imported.Maps[0];
        map.Id.Should().Be(original.Id);
        map.MapType.Should().Be(MapType.Region);
        map.OwnerId.Should().Be("thornveil");
        map.EntryNodeId.Should().Be(original.EntryNodeId);
        map.Nodes.Should().HaveCount(2);
        map.Nodes[1].NodeType.Should().Be(MapNodeType.Zone);
        map.Nodes[1].TargetMapId.Should().Be("zone:crestfall");
        map.Edges.Should().ContainSingle();
        map.Edges[0].Id.Should().Be(EdgeId);
        map.Edges[0].Direction.Should().Be(MapDirection.East);
        map.Edges[0].Label.Should().Be("East Road");
    }

    /// <summary>Verifies duplicate map identifiers are rejected on import.</summary>
    [Fact]
    public void Import_Rejects_Duplicate_Map_Ids()
    {
        var map = CreateRegionGraph();
        var json = $$"""
        {
          "formatVersion": 1,
          "exportedAt": "2026-10-22T00:00:00Z",
          "maps": [
            { "id": "{{map.Id}}", "mapType": "Region", "ownerId": "a", "name": "A", "description": "", "entryNodeId": "n", "nodes": [], "edges": [] },
            { "id": "{{map.Id}}", "mapType": "Region", "ownerId": "b", "name": "B", "description": "", "entryNodeId": "n", "nodes": [], "edges": [] }
          ]
        }
        """;

        var act = () => MapGraphJsonSerializer.Import(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*Duplicate map id*");
    }

    /// <summary>Verifies unsupported schema versions are rejected.</summary>
    [Fact]
    public void Import_Rejects_Unsupported_Format_Version()
    {
        var json = """
        { "formatVersion": 99, "exportedAt": "2026-10-22T00:00:00Z", "maps": [] }
        """;

        var act = () => MapGraphJsonSerializer.Import(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*Unsupported*99*");
    }

    /// <summary>Verifies malformed and empty documents are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not-json")]
    public void Import_Rejects_Invalid_Or_Empty_Json(string json)
    {
        var act = () => MapGraphJsonSerializer.Import(json);

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>Verifies file export/import round trips through UTF-8 files.</summary>
    [Fact]
    public void ExportToFile_And_ImportFromFile_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mapgraph-{Guid.NewGuid():N}.json");
        try
        {
            MapGraphJsonSerializer.ExportToFile(path, [CreateRegionGraph()]);

            var imported = MapGraphJsonSerializer.ImportFromFile(path);

            imported.Maps.Should().ContainSingle();
            imported.Maps[0].Nodes.Should().HaveCount(2);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>Verifies nodes and edges are exported in deterministic order.</summary>
    [Fact]
    public void Export_Orders_Nodes_And_Edges_Deterministically()
    {
        var graph = CreateRegionGraph();
        var json = MapGraphJsonSerializer.Export([graph]);

        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement
            .GetProperty("maps")[0]
            .GetProperty("nodes");

        nodes[0].GetProperty("id").GetString().Should().Be("region:thornveil:entry");
        nodes[1].GetProperty("id").GetString().Should().Be("region:thornveil:crestfall");
    }
}
