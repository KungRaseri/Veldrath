using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory implementation of INodeRepository for testing and development.
/// </summary>
public class InMemoryNodeRepository : INodeRepository
{
    private readonly ILogger<InMemoryNodeRepository> _logger;
    private readonly Dictionary<string, HarvestableNode> _nodes;
    private readonly Dictionary<string, List<string>> _locationIndex;
    private int _nextNodeId = 1;

    /// <summary>
    /// Initializes a new instance of InMemoryNodeRepository.
    /// </summary>
    public InMemoryNodeRepository(ILogger<InMemoryNodeRepository> logger)
    {
        _logger = logger;
        _nodes = new Dictionary<string, HarvestableNode>();
        _locationIndex = new Dictionary<string, List<string>>();
    }

    /// <inheritdoc />
    public Task<HarvestableNode?> GetNodeByIdAsync(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    /// <inheritdoc />
    public Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId)
    {
        if (!_locationIndex.TryGetValue(locationId, out var nodeIds))
        {
            return Task.FromResult(new List<HarvestableNode>());
        }

        var nodes = nodeIds
            .Select(id => _nodes.TryGetValue(id, out var node) ? node : null)
            .Where(n => n != null)
            .Cast<HarvestableNode>()
            .ToList();

        return Task.FromResult(nodes);
    }

    /// <inheritdoc />
    public Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius)
    {
        // For in-memory implementation, just return nodes in the same location
        // A real implementation would use spatial queries
        return GetNodesByLocationAsync(locationId);
    }

    /// <inheritdoc />
    public Task<bool> SaveNodeAsync(HarvestableNode node)
    {
        try
        {
            _nodes[node.NodeId] = node;

            // Update location index
            if (!_locationIndex.ContainsKey(node.LocationId))
            {
                _locationIndex[node.LocationId] = new List<string>();
            }

            if (!_locationIndex[node.LocationId].Contains(node.NodeId))
            {
                _locationIndex[node.LocationId].Add(node.NodeId);
            }

            _logger.LogDebug("Saved node {NodeId} at location {LocationId}", node.NodeId, node.LocationId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save node {NodeId}", node.NodeId);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> UpdateNodeHealthAsync(string nodeId, int newHealth)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            _logger.LogWarning("Node {NodeId} not found for health update", nodeId);
            return Task.FromResult(false);
        }

        node.CurrentHealth = newHealth;
        _logger.LogDebug("Updated node {NodeId} health to {Health}", nodeId, newHealth);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node)
    {
        if (string.IsNullOrEmpty(node.NodeId))
        {
            node.NodeId = $"node_{_nextNodeId++}";
        }

        _nodes[node.NodeId] = node;

        // Update location index
        if (!_locationIndex.ContainsKey(node.LocationId))
        {
            _locationIndex[node.LocationId] = new List<string>();
        }
        _locationIndex[node.LocationId].Add(node.NodeId);

        _logger.LogInformation("Spawned node {NodeId} ({NodeType}) at location {LocationId}", 
            node.NodeId, node.NodeType, node.LocationId);

        return Task.FromResult(node);
    }

    /// <inheritdoc />
    public Task<bool> RemoveNodeAsync(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            _logger.LogWarning("Node {NodeId} not found for removal", nodeId);
            return Task.FromResult(false);
        }

        _nodes.Remove(nodeId);

        // Remove from location index
        if (_locationIndex.TryGetValue(node.LocationId, out var nodeIds))
        {
            nodeIds.Remove(nodeId);
        }

        _logger.LogInformation("Removed node {NodeId} from location {LocationId}", nodeId, node.LocationId);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync()
    {
        var now = DateTime.UtcNow;
        var readyNodes = _nodes.Values
            .Where(n => n.CurrentHealth < n.MaxHealth && 
                       (now - n.LastHarvestedAt).TotalSeconds >= 60) // Default 60 second delay
            .ToList();

        _logger.LogDebug("Found {Count} nodes ready for regeneration", readyNodes.Count);
        return Task.FromResult(readyNodes);
    }

    /// <summary>
    /// Clears all nodes from the repository (for testing).
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _locationIndex.Clear();
        _nextNodeId = 1;
    }

    /// <summary>
    /// Gets the total count of nodes in the repository (for testing).
    /// </summary>
    public int Count => _nodes.Count;
}
