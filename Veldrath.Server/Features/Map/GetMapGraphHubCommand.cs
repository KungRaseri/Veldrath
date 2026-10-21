using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Map;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Map;

/// <summary>
/// Returns the map graph the character is currently on, or a specific requested map,
/// with hidden nodes and edges filtered by the character's discovery state.
/// </summary>
/// <param name="CharacterId">Character requesting the map.</param>
/// <param name="MapId">Optional map to load; defaults to the character's current map.</param>
public record GetMapGraphHubCommand(Guid CharacterId, string? MapId = null)
    : IRequest<GetMapGraphHubResult>;

/// <summary>Result returned by <see cref="GetMapGraphHubCommandHandler"/>.</summary>
public record GetMapGraphHubResult
{
    /// <summary>Gets a value indicating whether the map was loaded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the filtered map graph, or <see langword="null"/> on failure.</summary>
    public MapGraphDto? Map { get; init; }

    /// <summary>Gets the node the character occupies on the returned map, when known.</summary>
    public string? CurrentNodeId { get; init; }

    /// <summary>Gets summaries for every active map.</summary>
    public IReadOnlyList<MapSummaryDto> AvailableMaps { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetMapGraphHubCommand"/> by resolving the character's current map,
/// loading the graph from <see cref="IMapGraphRepository"/>, and removing hidden nodes and
/// edges unless the character has unlocked the referenced location.
/// </summary>
public class GetMapGraphHubCommandHandler
    : IRequestHandler<GetMapGraphHubCommand, GetMapGraphHubResult>
{
    private readonly IMapGraphRepository _mapRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly ILogger<GetMapGraphHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetMapGraphHubCommandHandler"/>.</summary>
    /// <param name="mapRepo">Map graph repository.</param>
    /// <param name="characterRepo">Character repository.</param>
    /// <param name="unlockedRepo">Character discovery repository.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetMapGraphHubCommandHandler(
        IMapGraphRepository mapRepo,
        ICharacterRepository characterRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        ILogger<GetMapGraphHubCommandHandler> logger)
    {
        _mapRepo = mapRepo;
        _characterRepo = characterRepo;
        _unlockedRepo = unlockedRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GetMapGraphHubResult> Handle(
        GetMapGraphHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CharacterId == Guid.Empty)
            return Fail("Character ID cannot be empty");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character '{request.CharacterId}' was not found");

        var mapId = request.MapId;
        if (string.IsNullOrWhiteSpace(mapId))
            mapId = character.CurrentMapId;

        if (string.IsNullOrWhiteSpace(mapId))
        {
            var currentZoneMap = await _mapRepo.GetMapForZoneAsync(
                character.CurrentZoneId,
                cancellationToken);
            mapId = currentZoneMap?.Id;
        }

        if (string.IsNullOrWhiteSpace(mapId))
            return Fail("No map is associated with the character's current zone");

        var graph = await _mapRepo.GetMapAsync(mapId, cancellationToken);
        if (graph is null)
            return Fail($"Map '{mapId}' was not found");

        var unlockedSlugs = await _unlockedRepo.GetUnlockedSlugsAsync(
            request.CharacterId,
            cancellationToken);

        var currentNodeId = string.Equals(
            character.CurrentMapId,
            graph.Id,
            StringComparison.OrdinalIgnoreCase)
            ? character.CurrentNodeId
            : graph.EntryNodeId;

        var visibleNodes = graph.Nodes
            .Where(node => IsVisible(node, unlockedSlugs, currentNodeId))
            .ToList();

        var visibleNodeIds = new HashSet<string>(
            visibleNodes.Select(node => node.Id),
            StringComparer.OrdinalIgnoreCase);

        var visibleEdges = graph.Edges
            .Where(edge => !edge.IsHidden)
            .Where(edge =>
                visibleNodeIds.Contains(edge.FromNodeId) &&
                visibleNodeIds.Contains(edge.ToNodeId))
            .ToList();

        var summaries = (await _mapRepo.GetMapsAsync(cancellationToken))
            .Select(ToSummary)
            .ToList();

        _logger.LogDebug(
            "Returning map {MapId} for character {CharacterIdPrefix}: {NodeCount} nodes, {EdgeCount} edges",
            graph.Id,
            request.CharacterId.ToString()[..8],
            visibleNodes.Count,
            visibleEdges.Count);

        return new GetMapGraphHubResult
        {
            Success = true,
            Map = new MapGraphDto(
                graph.Id,
                graph.MapType.ToString(),
                graph.OwnerId,
                graph.Name,
                graph.Description,
                graph.EntryNodeId,
                visibleNodes.Select(ToNodeDto).ToList(),
                visibleEdges.Select(ToEdgeDto).ToList()),
            CurrentNodeId = currentNodeId,
            AvailableMaps = summaries,
        };
    }

    private static bool IsVisible(
        MapNodeEntry node,
        IReadOnlySet<string> unlockedSlugs,
        string? currentNodeId)
    {
        if (!node.IsHidden)
            return true;

        if (!string.IsNullOrWhiteSpace(currentNodeId) &&
            string.Equals(node.Id, currentNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return node.RefId is not null && unlockedSlugs.Contains(node.RefId);
    }

    private static MapNodeDto ToNodeDto(MapNodeEntry node) =>
        new(
            node.Id,
            node.MapId,
            node.NodeType.ToString(),
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

    private static MapEdgeDto ToEdgeDto(MapEdgeEntry edge) =>
        new(
            edge.Id,
            edge.MapId,
            edge.FromNodeId,
            edge.ToNodeId,
            edge.Direction.ToString(),
            edge.EdgeType.ToString(),
            edge.Label,
            edge.IsTraversable,
            edge.IsHidden);

    private static MapSummaryDto ToSummary(MapSummary summary) =>
        new(
            summary.Id,
            summary.MapType.ToString(),
            summary.OwnerId,
            summary.Name,
            summary.Description,
            summary.EntryNodeId,
            summary.NodeCount,
            summary.EdgeCount);

    private static GetMapGraphHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
