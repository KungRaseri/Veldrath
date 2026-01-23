using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Handler for querying nearby harvestable nodes.
/// </summary>
public class GetNearbyNodesQueryHandler : IRequestHandler<GetNearbyNodesQuery, NearbyNodesResult>
{
    private readonly ILogger<GetNearbyNodesQueryHandler> _logger;
    private readonly INodeRepository _nodeRepository;
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the GetNearbyNodesQueryHandler class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="nodeRepository">Repository for accessing harvesting nodes.</param>
    /// <param name="saveGameService">Service for accessing current game state and character location.</param>
    public GetNearbyNodesQueryHandler(
        ILogger<GetNearbyNodesQueryHandler> logger,
        INodeRepository nodeRepository,
        ISaveGameService saveGameService)
    {
        _logger = logger;
        _nodeRepository = nodeRepository;
        _saveGameService = saveGameService;
    }

    /// <inheritdoc />
    public async Task<NearbyNodesResult> Handle(GetNearbyNodesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Determine location to query
            string locationId;
            if (!string.IsNullOrEmpty(request.LocationId))
            {
                locationId = request.LocationId;
            }
            else
            {
                // Get character's current location from save game
                var currentSave = _saveGameService.GetCurrentSave();
                if (currentSave == null)
                {
                    _logger.LogWarning("No active save game found for character {CharacterName}", request.CharacterName);
                    return new NearbyNodesResult
                    {
                        Success = false,
                        Nodes = new List<NearbyNodeInfo>()
                    };
                }
                
                // Use last visited location or default to "starting-area"
                locationId = currentSave.VisitedLocations.LastOrDefault() ?? "starting-area";
            }

            _logger.LogInformation(
                "Character {CharacterName} querying nearby nodes in location {LocationId}",
                request.CharacterName, locationId
            );

            // Query nodes from repository
            var nodes = await _nodeRepository.GetNodesByLocationAsync(locationId);
            
            // Map nodes to DTOs
            var nodeInfos = new List<NearbyNodeInfo>();
            foreach (var node in nodes)
            {
                nodeInfos.Add(new NearbyNodeInfo
                {
                    NodeId = node.NodeId,
                    DisplayName = node.DisplayName,
                    NodeType = node.NodeType,
                    MaterialTier = node.MaterialTier,
                    HealthPercent = node.GetHealthPercent(),
                    CanHarvest = node.CanHarvest(),
                    StateDescription = node.GetNodeState().ToString(),
                    IsRichNode = node.IsRichNode
                });
            }

            // Apply filters
            var filteredNodes = nodeInfos;
            if (request.OnlyHarvestable)
            {
                filteredNodes = filteredNodes.Where(n => n.CanHarvest).ToList();
            }

            if (!string.IsNullOrEmpty(request.NodeTypeFilter))
            {
                filteredNodes = filteredNodes
                    .Where(n => n.NodeType.Equals(request.NodeTypeFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _logger.LogDebug("Found {Count} nodes in {Location} (filtered from {Total})",
                filteredNodes.Count, locationId, nodes.Count);

            return new NearbyNodesResult
            {
                Success = true,
                LocationId = locationId,
                LocationName = locationId, // Could be enhanced with location service
                BiomeType = "unknown", // Could be enhanced with location service
                Nodes = filteredNodes,
                TotalNodes = nodes.Count,
                HarvestablNodes = filteredNodes.Count(n => n.CanHarvest),
                DepletedNodes = filteredNodes.Count(n => !n.CanHarvest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying nearby nodes for character {CharacterName}", request.CharacterName);

            return new NearbyNodesResult
            {
                Success = false,
                ErrorMessage = "Failed to load nearby nodes."
            };
        }
    }
}
