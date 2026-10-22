using RealmEngine.Shared.Models;

namespace RealmForge.Tests.Infrastructure;

/// <summary>Shared map-graph fixtures for RealmForge tests.</summary>
internal static class MapGraphTestData
{
    /// <summary>Creates a valid three-node zone map used across service and view-model tests.</summary>
    /// <param name="mapId">Map identifier to use.</param>
    /// <returns>A zone graph with an entry point, a location, and an exit.</returns>
    public static MapGraph CreateZoneGraph(string mapId = "zone:crestfall") => new(
        mapId,
        MapType.Zone,
        "crestfall",
        "Crestfall",
        "A test zone map",
        $"{mapId}:entry",
        [
            Node($"{mapId}:entry", mapId, MapNodeType.EntryPoint, "Entry", 0, isEntryPoint: true),
            Node($"{mapId}:square", mapId, MapNodeType.Location, "Square", 1),
            Node(
                $"{mapId}:east",
                mapId,
                MapNodeType.ZoneExit,
                "East Road",
                2,
                isExitPoint: true,
                targetMapId: "zone:ember",
                targetNodeId: "zone:ember:entry"),
        ],
        [
            new MapEdgeEntry(
                Guid.NewGuid(),
                mapId,
                $"{mapId}:square",
                $"{mapId}:east",
                MapDirection.East,
                MapEdgeType.Walk,
                "East",
                true,
                false),
        ]);

    private static MapNodeEntry Node(
        string id,
        string mapId,
        MapNodeType nodeType,
        string displayName,
        int sortOrder,
        bool isEntryPoint = false,
        bool isExitPoint = false,
        string? targetMapId = null,
        string? targetNodeId = null) => new(
        id,
        mapId,
        nodeType,
        null,
        displayName,
        $"{displayName} description",
        null,
        sortOrder,
        0,
        isEntryPoint,
        isExitPoint,
        targetMapId,
        targetNodeId,
        false,
        null,
        null,
        null,
        sortOrder);
}
