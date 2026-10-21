using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for region and zone map graphs.</summary>
public class EfCoreMapGraphRepository(ContentDbContext db, ILogger<EfCoreMapGraphRepository> logger)
    : IMapGraphRepository
{
    /// <inheritdoc />
    public async Task<MapGraph?> GetMapAsync(string mapId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            return null;

        var map = await db.Maps.AsNoTracking()
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .FirstOrDefaultAsync(m => m.Id == mapId && m.IsActive, cancellationToken);

        if (map is null)
        {
            logger.LogDebug("Map graph {MapId} was not found or is inactive", mapId);
            return null;
        }

        return MapToGraph(map);
    }

    /// <inheritdoc />
    public Task<MapGraph?> GetMapForOwnerAsync(
        MapType mapType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return Task.FromResult<MapGraph?>(null);

        return GetMapByOwnerAsync(mapType, ownerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MapGraph?> GetMapForZoneAsync(string zoneId, CancellationToken cancellationToken = default) =>
        GetMapForOwnerAsync(MapType.Zone, zoneId, cancellationToken);

    /// <inheritdoc />
    public Task<MapGraph?> GetMapForRegionAsync(string regionId, CancellationToken cancellationToken = default) =>
        GetMapForOwnerAsync(MapType.Region, regionId, cancellationToken);

    /// <inheritdoc />
    public async Task<MapNodeEntry?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        var node = await db.MapNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

        return node is null ? null : MapToNode(node);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MapEdgeEntry>> GetOutgoingEdgesAsync(
        string mapId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(nodeId))
            return [];

        var edges = await db.MapEdges.AsNoTracking()
            .Where(e => e.MapId == mapId && e.FromNodeId == nodeId)
            .OrderBy(e => e.Direction)
            .ThenBy(e => e.ToNodeId)
            .ToListAsync(cancellationToken);

        return edges.Select(MapToEdge).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MapSummary>> GetMapsAsync(CancellationToken cancellationToken = default)
    {
        var maps = await db.Maps.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.MapType)
            .ThenBy(m => m.Name)
            .Select(m => new MapSummary(
                m.Id,
                m.MapType,
                m.OwnerId,
                m.Name,
                m.Description,
                m.EntryNodeId,
                m.Nodes.Count,
                m.Edges.Count))
            .ToListAsync(cancellationToken);

        return maps;
    }

    private async Task<MapGraph?> GetMapByOwnerAsync(
        MapType mapType,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var map = await db.Maps.AsNoTracking()
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .FirstOrDefaultAsync(
                m => m.IsActive && m.MapType == mapType && m.OwnerId == ownerId,
                cancellationToken);

        return map is null ? null : MapToGraph(map);
    }

    private static MapGraph MapToGraph(Map map) =>
        new(
            map.Id,
            map.MapType,
            map.OwnerId,
            map.Name,
            map.Description,
            map.EntryNodeId,
            map.Nodes
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.DisplayName)
                .ThenBy(n => n.Id)
                .Select(MapToNode)
                .ToList(),
            map.Edges
                .OrderBy(e => e.FromNodeId)
                .ThenBy(e => e.Direction)
                .ThenBy(e => e.ToNodeId)
                .Select(MapToEdge)
                .ToList());

    private static MapNodeEntry MapToNode(MapNode node) =>
        new(
            node.Id,
            node.MapId,
            node.NodeType,
            node.RefId,
            node.DisplayName,
            node.Description,
            node.IconKey,
            node.LayoutX,
            node.LayoutY,
            node.IsEntryPoint,
            node.IsExitPoint,
            node.TargetMapId,
            node.TargetNodeId,
            node.IsHidden,
            node.UnlockType,
            node.UnlockKey,
            node.DiscoverThreshold,
            node.SortOrder);

    private static MapEdgeEntry MapToEdge(MapEdge edge) =>
        new(
            edge.Id,
            edge.MapId,
            edge.FromNodeId,
            edge.ToNodeId,
            edge.Direction,
            edge.EdgeType,
            edge.Label,
            edge.IsTraversable,
            edge.IsHidden);
}
