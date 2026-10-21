using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Features.Map;

/// <summary>
/// Moves a character to an adjacent node on their current map, or through an exit node to
/// the linked destination map. Validates visibility, adjacency, and destination availability.
/// </summary>
/// <param name="CharacterId">Character performing the travel.</param>
/// <param name="MapId">Map containing the selected node.</param>
/// <param name="NodeId">Selected destination node.</param>
/// <param name="ZoneGroup">SignalR group used to key the enemy roster; derived from the zone when empty.</param>
public record TravelToNodeHubCommand(
    Guid CharacterId,
    string MapId,
    string NodeId,
    string ZoneGroup = "")
    : IRequest<TravelToNodeHubResult>;

/// <summary>Result returned by <see cref="TravelToNodeHubCommandHandler"/>.</summary>
public record TravelToNodeHubResult
{
    /// <summary>Gets a value indicating whether the character moved.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the map the character occupies after the action.</summary>
    public string? MapId { get; init; }

    /// <summary>Gets the node the character occupies after the action.</summary>
    public string? NodeId { get; init; }

    /// <summary>Gets the node type at the destination, such as <c>Location</c> or <c>EntryPoint</c>.</summary>
    public string? NodeType { get; init; }

    /// <summary>Gets whether the action crossed into another map.</summary>
    public bool MapChanged { get; init; }

    /// <summary>Gets a human-readable outcome message.</summary>
    public string? Message { get; init; }

    /// <summary>Gets the zone the character is in after the action, or <see langword="null"/> on a region map.</summary>
    public string? ZoneId { get; init; }

    /// <summary>Gets the region the character is in after the action.</summary>
    public string? RegionId { get; init; }

    /// <summary>Gets the location slug when the destination is a <c>Location</c> node.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets hidden locations newly discovered by the passive skill-check sweep.</summary>
    public IReadOnlyList<ZoneLocationEntry> PassiveDiscoveries { get; init; } = [];

    /// <summary>Gets the live enemies present at the arrived location.</summary>
    public IReadOnlyList<SpawnedEnemySummary> SpawnedEnemies { get; init; } = [];
}

/// <summary>
/// Handles <see cref="TravelToNodeHubCommand"/> by validating the selected node against the
/// current map graph, resolving exit destinations, persisting the new map/node on both the
/// character and active session, and running zone arrival effects (passive discovery and rosters).
/// </summary>
public class TravelToNodeHubCommandHandler
    : IRequestHandler<TravelToNodeHubCommand, TravelToNodeHubResult>
{
    private const string PassiveCheckType = "skill_check_passive";

    private readonly IMapGraphRepository _mapRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly IZoneRepository _zoneRepo;
    private readonly IZoneLocationRepository _locationRepo;
    private readonly ActorPoolResolver _actorPoolResolver;
    private readonly ILogger<TravelToNodeHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="TravelToNodeHubCommandHandler"/>.</summary>
    /// <param name="mapRepo">Map graph repository.</param>
    /// <param name="characterRepo">Character repository.</param>
    /// <param name="unlockedRepo">Character discovery repository.</param>
    /// <param name="sessionRepo">Active player-session repository.</param>
    /// <param name="zoneRepo">Zone catalog repository used to resolve the destination region.</param>
    /// <param name="locationRepo">Zone-location repository used for discovery and rosters.</param>
    /// <param name="actorPoolResolver">Resolver that spawns the destination enemy roster.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TravelToNodeHubCommandHandler(
        IMapGraphRepository mapRepo,
        ICharacterRepository characterRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        IPlayerSessionRepository sessionRepo,
        IZoneRepository zoneRepo,
        IZoneLocationRepository locationRepo,
        ActorPoolResolver actorPoolResolver,
        ILogger<TravelToNodeHubCommandHandler> logger)
    {
        _mapRepo = mapRepo;
        _characterRepo = characterRepo;
        _unlockedRepo = unlockedRepo;
        _sessionRepo = sessionRepo;
        _zoneRepo = zoneRepo;
        _locationRepo = locationRepo;
        _actorPoolResolver = actorPoolResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TravelToNodeHubResult> Handle(
        TravelToNodeHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CharacterId == Guid.Empty)
            return Fail("Character ID cannot be empty");
        if (string.IsNullOrWhiteSpace(request.MapId))
            return Fail("Map ID cannot be empty");
        if (string.IsNullOrWhiteSpace(request.NodeId))
            return Fail("Node ID cannot be empty");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character '{request.CharacterId}' was not found");

        var map = await _mapRepo.GetMapAsync(request.MapId, cancellationToken);
        if (map is null)
            return Fail($"Map '{request.MapId}' was not found");

        if (!string.IsNullOrWhiteSpace(character.CurrentMapId) &&
            !string.Equals(character.CurrentMapId, map.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Character is not on the requested map");
        }

        var currentNodeId = string.IsNullOrWhiteSpace(character.CurrentNodeId)
            ? map.EntryNodeId
            : character.CurrentNodeId;

        if (string.IsNullOrWhiteSpace(currentNodeId))
            return Fail("Character has no valid position on this map");

        var currentNode = FindNode(map, currentNodeId);
        if (currentNode is null)
            return Fail($"Current node '{currentNodeId}' does not exist on map '{map.Id}'");

        var selectedNode = FindNode(map, request.NodeId);
        if (selectedNode is null)
            return Fail($"Node '{request.NodeId}' does not exist on map '{map.Id}'");

        var unlockedSlugs = await _unlockedRepo.GetUnlockedSlugsAsync(
            request.CharacterId,
            cancellationToken);

        if (!IsVisible(selectedNode, unlockedSlugs, currentNode.Id))
            return Fail($"Node '{selectedNode.DisplayName}' has not been discovered");

        if (string.IsNullOrWhiteSpace(selectedNode.TargetMapId) &&
            string.Equals(selectedNode.Id, currentNode.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new TravelToNodeHubResult
            {
                Success = true,
                MapId = map.Id,
                NodeId = selectedNode.Id,
                NodeType = selectedNode.NodeType.ToString(),
                MapChanged = false,
                Message = $"Already at {selectedNode.DisplayName}.",
                ZoneId = map.MapType == MapType.Zone ? map.OwnerId : null,
                LocationSlug = selectedNode.NodeType == MapNodeType.Location
                    ? selectedNode.RefId
                    : null,
            };
        }

        if (!string.Equals(selectedNode.Id, currentNode.Id, StringComparison.OrdinalIgnoreCase))
        {
            var edges = await _mapRepo.GetOutgoingEdgesAsync(
                map.Id,
                currentNode.Id,
                cancellationToken);

            var adjacent = edges.Any(edge =>
                edge.IsTraversable &&
                string.Equals(edge.ToNodeId, selectedNode.Id, StringComparison.OrdinalIgnoreCase));

            if (!adjacent)
                return Fail($"There is no direct path from '{currentNode.DisplayName}' to '{selectedNode.DisplayName}'");
        }

        var destinationMap = map;
        var destinationNode = selectedNode;
        var mapChanged = false;

        if (!string.IsNullOrWhiteSpace(selectedNode.TargetMapId))
        {
            var targetMap = await _mapRepo.GetMapAsync(selectedNode.TargetMapId, cancellationToken);
            if (targetMap is null)
                return Fail($"Destination map '{selectedNode.TargetMapId}' is not available");

            var targetNodeId = string.IsNullOrWhiteSpace(selectedNode.TargetNodeId)
                ? targetMap.EntryNodeId
                : selectedNode.TargetNodeId;

            if (string.IsNullOrWhiteSpace(targetNodeId))
                return Fail($"Destination map '{targetMap.Id}' has no arrival node");

            var targetNode = FindNode(targetMap, targetNodeId);
            if (targetNode is null)
                return Fail($"Destination node '{targetNodeId}' does not exist on map '{targetMap.Id}'");

            if (!IsVisible(targetNode, unlockedSlugs, currentNodeId: null))
                return Fail($"Destination '{targetNode.DisplayName}' has not been discovered");

            destinationMap = targetMap;
            destinationNode = targetNode;
            mapChanged = true;
        }

        var zoneId = destinationMap.MapType == MapType.Zone
            ? destinationMap.OwnerId
            : null;

        var regionId = destinationMap.MapType == MapType.Region
            ? destinationMap.OwnerId
            : (await _zoneRepo.GetByIdAsync(zoneId ?? string.Empty))?.RegionId;

        var locationSlug = destinationNode.NodeType == MapNodeType.Location
            ? destinationNode.RefId
            : null;

        await _characterRepo.UpdateMapPositionAsync(
            request.CharacterId,
            destinationMap.Id,
            destinationNode.Id,
            zoneId,
            locationSlug,
            cancellationToken);

        await _sessionRepo.UpdateMapPositionAsync(
            request.CharacterId,
            destinationMap.Id,
            destinationNode.Id,
            zoneId,
            regionId);

        if (zoneId is not null &&
            destinationNode.LayoutX is not null &&
            destinationNode.LayoutY is not null)
        {
            await _characterRepo.UpdateTilePositionAsync(
                request.CharacterId,
                destinationNode.LayoutX.Value,
                destinationNode.LayoutY.Value,
                zoneId,
                cancellationToken);
        }

        var discoveries = destinationMap.MapType == MapType.Zone &&
                          destinationNode.NodeType == MapNodeType.Location &&
                          zoneId is not null
            ? await RunPassiveDiscoveryAsync(
                request.CharacterId,
                zoneId,
                unlockedSlugs,
                cancellationToken)
            : [];

        var spawnedEnemies = await EnsureRosterAsync(
            request.ZoneGroup,
            destinationMap,
            destinationNode,
            zoneId);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} travelled to {MapId}/{NodeId} (map changed: {MapChanged})",
            request.CharacterId.ToString()[..8],
            destinationMap.Id,
            destinationNode.Id,
            mapChanged);

        var message = mapChanged
            ? $"Entered {destinationMap.Name} at {destinationNode.DisplayName}."
            : $"Travelled to {destinationNode.DisplayName}.";

        return new TravelToNodeHubResult
        {
            Success = true,
            MapId = destinationMap.Id,
            NodeId = destinationNode.Id,
            NodeType = destinationNode.NodeType.ToString(),
            MapChanged = mapChanged,
            Message = message,
            ZoneId = zoneId,
            RegionId = regionId,
            LocationSlug = locationSlug,
            PassiveDiscoveries = discoveries,
            SpawnedEnemies = spawnedEnemies,
        };
    }

    private async Task<IReadOnlyList<SpawnedEnemySummary>> EnsureRosterAsync(
        string zoneGroup,
        MapGraph destinationMap,
        MapNodeEntry destinationNode,
        string? zoneId)
    {
        if (destinationMap.MapType != MapType.Zone ||
            destinationNode.NodeType != MapNodeType.Location ||
            string.IsNullOrWhiteSpace(destinationNode.RefId) ||
            zoneId is null)
        {
            return [];
        }

        var effectiveZoneGroup = string.IsNullOrWhiteSpace(zoneGroup)
            ? $"zone:{zoneId}"
            : zoneGroup;

        var storeKey = ZoneLocationEnemyStore.MakeKey(effectiveZoneGroup, destinationNode.RefId);

        if (!ZoneLocationEnemyStore.HasRoster(storeKey))
        {
            var location = await _locationRepo.GetBySlugAsync(destinationNode.RefId);
            if (location is not null)
            {
                var roster = await _actorPoolResolver.SpawnRosterAsync(location.ActorPool);
                ZoneLocationEnemyStore.TryAddRoster(storeKey, roster);

                _logger.LogInformation(
                    "Spawned {Count} enemies at {StoreKey}",
                    roster.Count,
                    storeKey);
            }
        }

        return ZoneLocationEnemyStore.GetAlive(storeKey)
            .Select(enemy => new SpawnedEnemySummary(
                enemy.Id,
                enemy.Name,
                enemy.Level,
                enemy.CurrentHealth,
                enemy.MaxHealth))
            .ToList();
    }

    private async Task<IReadOnlyList<ZoneLocationEntry>> RunPassiveDiscoveryAsync(
        Guid characterId,
        string zoneId,
        IReadOnlySet<string> alreadyUnlocked,
        CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(characterId, cancellationToken);
        if (character is null)
            return [];

        var hiddenLocations = await _locationRepo.GetHiddenByZoneIdAsync(zoneId);
        var discovered = new List<ZoneLocationEntry>();

        foreach (var location in hiddenLocations)
        {
            if (!string.Equals(location.UnlockType, PassiveCheckType, StringComparison.OrdinalIgnoreCase) ||
                alreadyUnlocked.Contains(location.Slug))
            {
                continue;
            }

            var threshold = location.DiscoverThreshold.GetValueOrDefault(int.MaxValue);
            if (character.Level < threshold)
                continue;

            await _unlockedRepo.AddUnlockAsync(
                characterId,
                location.Slug,
                PassiveCheckType,
                cancellationToken);

            discovered.Add(location);

            _logger.LogInformation(
                "Character {CharacterIdPrefix} passively discovered {LocationSlug} (level {Level} >= threshold {Threshold})",
                characterId.ToString()[..8],
                location.Slug,
                character.Level,
                threshold);
        }

        return discovered;
    }

    private static MapNodeEntry? FindNode(MapGraph map, string nodeId) =>
        map.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));

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

    private static TravelToNodeHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
