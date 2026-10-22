using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Authoring;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;
using MapEntity = RealmEngine.Data.Entities.Map;

namespace RealmForge.Services;

/// <summary>
/// Loads, saves, validates, and bulk-imports/exports map graphs through
/// <see cref="ContentDbContext"/>. Used by the RealmForge map-graph editor.
/// </summary>
public sealed class MapGraphEditorService(
    IServiceScopeFactory scopeFactory,
    ILogger<MapGraphEditorService> logger)
{
    /// <summary>Gets the last error message produced by a failed operation, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>Loads every map graph from the content database.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All maps ordered by identifier.</returns>
    public async Task<IReadOnlyList<MapGraph>> LoadAllAsync(CancellationToken ct = default)
    {
        LastError = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null)
            {
                LastError = "Content database is not configured.";
                return [];
            }

            var maps = await db.Maps
                .AsNoTracking()
                .Include(m => m.Nodes)
                .Include(m => m.Edges)
                .OrderBy(m => m.Id)
                .ToListAsync(ct);

            return [.. maps.Select(ToGraph)];
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to load map graphs");
            return [];
        }
    }

    /// <summary>Loads a single map graph by identifier.</summary>
    /// <param name="mapId">Stable map identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The graph, or <see langword="null"/> when it does not exist.</returns>
    public async Task<MapGraph?> LoadAsync(string mapId, CancellationToken ct = default)
    {
        LastError = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null)
            {
                LastError = "Content database is not configured.";
                return null;
            }

            var map = await db.Maps
                .AsNoTracking()
                .Include(m => m.Nodes)
                .Include(m => m.Edges)
                .FirstOrDefaultAsync(m => m.Id == mapId, ct);

            return map is null ? null : ToGraph(map);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to load map graph {MapId}", mapId);
            return null;
        }
    }

    /// <summary>Creates or updates a map graph and synchronizes its nodes and edges.</summary>
    /// <param name="graph">The graph to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the graph was saved.</returns>
    public async Task<bool> SaveAsync(MapGraph graph, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        LastError = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null)
            {
                LastError = "Content database is not configured.";
                return false;
            }

            await SaveInternalAsync(db, graph, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to save map graph {MapId}", graph.Id);
            return false;
        }
    }

    /// <summary>Deletes a map graph and its nodes/edges.</summary>
    /// <param name="mapId">Stable map identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a map was deleted.</returns>
    public async Task<bool> DeleteAsync(string mapId, CancellationToken ct = default)
    {
        LastError = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null)
            {
                LastError = "Content database is not configured.";
                return false;
            }

            var map = await db.Maps.FirstOrDefaultAsync(m => m.Id == mapId, ct);
            if (map is null)
            {
                LastError = $"Map '{mapId}' was not found.";
                return false;
            }

            db.Maps.Remove(map);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to delete map graph {MapId}", mapId);
            return false;
        }
    }

    /// <summary>Validates a graph's structure and references.</summary>
    /// <param name="graph">The graph to validate.</param>
    /// <returns>The validation result.</returns>
    public static MapGraphValidationResult Validate(MapGraph graph) =>
        MapGraphValidator.Validate(graph);

    /// <summary>Exports every map graph as a JSON authoring document.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Indented JSON, or an empty string when loading failed.</returns>
    public async Task<string> ExportJsonAsync(CancellationToken ct = default)
    {
        var maps = await LoadAllAsync(ct);
        return maps.Count == 0 && LastError is not null
            ? string.Empty
            : MapGraphJsonSerializer.Export(maps);
    }

    /// <summary>Exports every map graph to a JSON file.</summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async Task<bool> ExportToFileAsync(string path, CancellationToken ct = default)
    {
        LastError = null;
        try
        {
            var json = await ExportJsonAsync(ct);
            if (string.IsNullOrEmpty(json))
                return false;

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(path, json, ct);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to export map graphs to {Path}", path);
            return false;
        }
    }

    /// <summary>Imports map graphs from a JSON document and upserts them.</summary>
    /// <param name="json">The JSON authoring document.</param>
    /// <param name="overwriteExisting">Whether existing maps with the same id are replaced.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of maps written.</returns>
    public async Task<int> ImportJsonAsync(
        string json,
        bool overwriteExisting = true,
        CancellationToken ct = default)
    {
        LastError = null;
        try
        {
            var document = MapGraphJsonSerializer.Import(json);
            var written = 0;
            foreach (var graph in document.Maps)
            {
                if (!overwriteExisting && await ExistsAsync(graph.Id, ct))
                    continue;

                if (await SaveAsync(graph, ct))
                    written++;
                else
                    return written;
            }

            return written;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to import map graphs");
            return 0;
        }
    }

    /// <summary>Imports map graphs from a JSON file.</summary>
    /// <param name="path">Source file path.</param>
    /// <param name="overwriteExisting">Whether existing maps with the same id are replaced.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of maps written.</returns>
    public async Task<int> ImportFromFileAsync(
        string path,
        bool overwriteExisting = true,
        CancellationToken ct = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return await ImportJsonAsync(json, overwriteExisting, ct);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogError(ex, "Failed to import map graphs from {Path}", path);
            return 0;
        }
    }

    private async Task<bool> ExistsAsync(string mapId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<ContentDbContext>();
        return db is not null && await db.Maps.AnyAsync(m => m.Id == mapId, ct);
    }

    private static async Task SaveInternalAsync(
        ContentDbContext db,
        MapGraph graph,
        CancellationToken ct)
    {
        var map = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .FirstOrDefaultAsync(m => m.Id == graph.Id, ct);

        var isNew = map is null;
        if (map is null)
        {
            map = new MapEntity { Id = graph.Id };
            db.Maps.Add(map);
        }

        map.MapType = graph.MapType;
        map.OwnerId = graph.OwnerId;
        map.Name = graph.Name;
        map.Description = graph.Description;
        map.EntryNodeId = graph.EntryNodeId;
        map.UpdatedAt = DateTimeOffset.UtcNow;
        map.Version = isNew ? 1 : map.Version + 1;

        SyncEdges(db, map, graph.Edges);
        SyncNodes(db, map, graph.Nodes);
    }

    private static void SyncNodes(ContentDbContext db, MapEntity map, IReadOnlyList<MapNodeEntry> nodes)
    {
        var incomingIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in map.Nodes.ToList())
        {
            if (!incomingIds.Contains(existing.Id))
            {
                map.Nodes.Remove(existing);
                db.MapNodes.Remove(existing);
            }
        }

        foreach (var entry in nodes)
        {
            var node = map.Nodes.FirstOrDefault(n => n.Id == entry.Id);
            if (node is null)
            {
                node = new MapNode { Id = entry.Id, MapId = map.Id };
                map.Nodes.Add(node);
            }

            node.MapId = map.Id;
            node.NodeType = entry.NodeType;
            node.RefId = entry.RefId;
            node.DisplayName = entry.DisplayName;
            node.Description = entry.Description;
            node.IconKey = entry.IconKey;
            node.LayoutX = entry.LayoutX;
            node.LayoutY = entry.LayoutY;
            node.IsEntryPoint = entry.IsEntryPoint;
            node.IsExitPoint = entry.IsExitPoint;
            node.TargetMapId = entry.TargetMapId;
            node.TargetNodeId = entry.TargetNodeId;
            node.IsHidden = entry.IsHidden;
            node.UnlockType = entry.UnlockType;
            node.UnlockKey = entry.UnlockKey;
            node.DiscoverThreshold = entry.DiscoverThreshold;
            node.SortOrder = entry.SortOrder;
        }
    }

    private static void SyncEdges(ContentDbContext db, MapEntity map, IReadOnlyList<MapEdgeEntry> edges)
    {
        var incomingIds = edges.Select(e => e.Id).ToHashSet();

        foreach (var existing in map.Edges.ToList())
        {
            if (!incomingIds.Contains(existing.Id))
            {
                map.Edges.Remove(existing);
                db.MapEdges.Remove(existing);
            }
        }

        foreach (var entry in edges)
        {
            var edge = map.Edges.FirstOrDefault(e => e.Id == entry.Id);
            if (edge is null)
            {
                edge = new MapEdge { Id = entry.Id, MapId = map.Id };
                map.Edges.Add(edge);
            }

            edge.MapId = map.Id;
            edge.FromNodeId = entry.FromNodeId;
            edge.ToNodeId = entry.ToNodeId;
            edge.Direction = entry.Direction;
            edge.EdgeType = entry.EdgeType;
            edge.Label = entry.Label;
            edge.IsTraversable = entry.IsTraversable;
            edge.IsHidden = entry.IsHidden;
        }
    }

    private static MapGraph ToGraph(MapEntity map) => new(
        map.Id,
        map.MapType,
        map.OwnerId,
        map.Name,
        map.Description,
        map.EntryNodeId,
        [.. map.Nodes
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .Select(n => new MapNodeEntry(
                n.Id, n.MapId, n.NodeType, n.RefId, n.DisplayName, n.Description, n.IconKey,
                n.LayoutX, n.LayoutY, n.IsEntryPoint, n.IsExitPoint, n.TargetMapId, n.TargetNodeId,
                n.IsHidden, n.UnlockType, n.UnlockKey, n.DiscoverThreshold, n.SortOrder))],
        [.. map.Edges
            .OrderBy(e => e.Id)
            .Select(e => new MapEdgeEntry(
                e.Id, e.MapId, e.FromNodeId, e.ToNodeId, e.Direction, e.EdgeType,
                e.Label, e.IsTraversable, e.IsHidden))]);
}
