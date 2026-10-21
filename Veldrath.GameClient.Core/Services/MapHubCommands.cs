using Veldrath.Contracts.Map;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Core.Services;

/// <summary>
/// Convenience helpers for the map-graph hub methods on <see cref="IGameHubConnectionService"/>.
/// </summary>
public static class MapHubCommands
{
    /// <summary>Requests the current map graph, or a specific map when <paramref name="mapId"/> is supplied.</summary>
    /// <param name="hub">Game hub connection.</param>
    /// <param name="mapId">Optional map identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task RequestMapGraphAsync(
        this IGameHubConnectionService hub,
        string? mapId = null,
        CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(mapId)
            ? hub.SendAsync("GetMapGraph", ct)
            : hub.SendAsync("GetMapGraph", mapId, ct);

    /// <summary>Requests travel to a node on the map graph.</summary>
    /// <param name="hub">Game hub connection.</param>
    /// <param name="mapId">Map containing the selected node.</param>
    /// <param name="nodeId">Destination node identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task TravelToNodeAsync(
        this IGameHubConnectionService hub,
        string mapId,
        string nodeId,
        CancellationToken ct = default) =>
        hub.SendAsync("TravelToNode", new TravelToNodeHubRequest(mapId, nodeId), ct);
}
