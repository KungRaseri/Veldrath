using Veldrath.Contracts.Map;

namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received after requesting a map graph.
/// </summary>
/// <param name="Success">Whether the server returned the graph.</param>
/// <param name="ErrorMessage">Failure reason when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="Map">The filtered map graph, when available.</param>
/// <param name="CurrentNodeId">The node the character occupies on the returned map.</param>
/// <param name="AvailableMaps">Summaries of all active maps.</param>
public sealed record MapGraphReceivedPayload(
    bool Success,
    string? ErrorMessage,
    MapGraphDto? Map,
    string? CurrentNodeId,
    IReadOnlyList<MapSummaryDto>? AvailableMaps = null);

/// <summary>
/// Hub event payload received after travelling to a map node.
/// </summary>
/// <param name="Success">Whether the character moved.</param>
/// <param name="ErrorMessage">Failure reason when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="MapId">Map the character occupies after the action.</param>
/// <param name="NodeId">Node the character occupies after the action.</param>
/// <param name="NodeType">Node type at the destination.</param>
/// <param name="MapChanged">Whether the action crossed into another map.</param>
/// <param name="Message">Human-readable outcome message.</param>
/// <param name="ZoneId">Zone context after the action, or <see langword="null"/> on a region map.</param>
/// <param name="RegionId">Region context after the action.</param>
/// <param name="LocationSlug">Location slug when the destination is a location node.</param>
/// <param name="PassiveDiscoveries">Hidden locations newly discovered on arrival.</param>
/// <param name="SpawnedEnemies">Live enemies at the arrived location.</param>
public sealed record NodeTraveledPayload(
    bool Success,
    string? ErrorMessage,
    string? MapId,
    string? NodeId,
    string? NodeType,
    bool MapChanged,
    string? Message,
    string? ZoneId,
    string? RegionId,
    string? LocationSlug,
    IReadOnlyList<RealmEngine.Shared.Models.ZoneLocationEntry>? PassiveDiscoveries = null,
    IReadOnlyList<TravelSpawnedEnemy>? SpawnedEnemies = null);

/// <summary>A live enemy present at a map location.</summary>
/// <param name="Id">Instance identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Level">Enemy level.</param>
/// <param name="CurrentHealth">Remaining health.</param>
/// <param name="MaxHealth">Maximum health.</param>
public sealed record TravelSpawnedEnemy(
    Guid Id,
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth);
