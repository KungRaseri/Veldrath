using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading region and zone map graphs.</summary>
public interface IMapGraphRepository
{
    /// <summary>Returns the map graph for <paramref name="mapId"/>, or <see langword="null"/> when not found or inactive.</summary>
    /// <param name="mapId">Stable map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MapGraph?> GetMapAsync(string mapId, CancellationToken cancellationToken = default);

    /// <summary>Returns the map graph owned by a region or zone slug, or <see langword="null"/> when not found.</summary>
    /// <param name="mapType">Whether to look up a region or zone map.</param>
    /// <param name="ownerId">Region or zone slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MapGraph?> GetMapForOwnerAsync(
        MapType mapType,
        string ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the zone map for <paramref name="zoneId"/>, or <see langword="null"/> when not found.</summary>
    /// <param name="zoneId">Zone slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MapGraph?> GetMapForZoneAsync(string zoneId, CancellationToken cancellationToken = default);

    /// <summary>Returns the region map for <paramref name="regionId"/>, or <see langword="null"/> when not found.</summary>
    /// <param name="regionId">Region slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MapGraph?> GetMapForRegionAsync(string regionId, CancellationToken cancellationToken = default);

    /// <summary>Returns a single node by identifier, or <see langword="null"/> when not found.</summary>
    /// <param name="nodeId">Stable node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MapNodeEntry?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>Returns the outgoing edges for a node on a map.</summary>
    /// <param name="mapId">Stable map identifier.</param>
    /// <param name="nodeId">Stable origin node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<MapEdgeEntry>> GetOutgoingEdgesAsync(
        string mapId,
        string nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns summaries for every active map.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<MapSummary>> GetMapsAsync(CancellationToken cancellationToken = default);
}
