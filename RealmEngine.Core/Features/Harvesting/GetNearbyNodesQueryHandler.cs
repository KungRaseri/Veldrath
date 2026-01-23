using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Handler for querying nearby harvestable nodes.
/// </summary>
public class GetNearbyNodesQueryHandler : IRequestHandler<GetNearbyNodesQuery, NearbyNodesResult>
{
    private readonly ILogger<GetNearbyNodesQueryHandler> _logger;
    // TODO: Add INodeRepository when persistence is implemented
    // TODO: Add ILocationRepository for location info

    public GetNearbyNodesQueryHandler(
        ILogger<GetNearbyNodesQueryHandler> logger)
    {
        _logger = logger;
    }

    public async Task<NearbyNodesResult> Handle(GetNearbyNodesQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // TODO: Replace with actual async repository calls
        
        try
        {
            _logger.LogInformation(
                "Character {CharacterName} querying nearby nodes in location {LocationId}",
                request.CharacterName, request.LocationId ?? "current"
            );

            // TODO: Get character's location if not provided
            // TODO: Query nodes from repository

            // Mock data for demonstration
            var mockNodes = new List<NearbyNodeInfo>
            {
                new NearbyNodeInfo
                {
                    NodeId = "node_copper_1",
                    DisplayName = "Copper Vein",
                    NodeType = "ore_vein",
                    MaterialTier = "common",
                    HealthPercent = 85,
                    CanHarvest = true,
                    StateDescription = "Healthy",
                    IsRichNode = false
                },
                new NearbyNodeInfo
                {
                    NodeId = "node_oak_1",
                    DisplayName = "Oak Tree",
                    NodeType = "tree",
                    MaterialTier = "common",
                    HealthPercent = 60,
                    CanHarvest = true,
                    StateDescription = "Depleted",
                    IsRichNode = false
                },
                new NearbyNodeInfo
                {
                    NodeId = "node_iron_1",
                    DisplayName = "Iron Vein",
                    NodeType = "ore_vein",
                    MaterialTier = "uncommon",
                    HealthPercent = 5,
                    CanHarvest = false,
                    StateDescription = "Empty - Regenerating",
                    IsRichNode = false
                }
            };

            // Apply filters
            var filteredNodes = mockNodes;
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

            return new NearbyNodesResult
            {
                Success = true,
                LocationId = request.LocationId ?? "test_location",
                LocationName = "Mountain Pass",
                BiomeType = "mountains",
                Nodes = filteredNodes,
                TotalNodes = mockNodes.Count,
                HarvestablNodes = mockNodes.Count(n => n.CanHarvest),
                DepletedNodes = mockNodes.Count(n => !n.CanHarvest)
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
