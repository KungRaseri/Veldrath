using Microsoft.Extensions.Logging;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service responsible for spawning harvestable resource nodes in game locations based on biome and density rules.
/// </summary>
public class NodeSpawnerService
{
    private readonly ILogger<NodeSpawnerService> _logger;
    private readonly ResourceNodeLoaderService _nodeLoader;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeSpawnerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="nodeLoader">The service for loading resource node definitions.</param>
    public NodeSpawnerService(
        ILogger<NodeSpawnerService> logger,
        ResourceNodeLoaderService nodeLoader)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nodeLoader = nodeLoader ?? throw new ArgumentNullException(nameof(nodeLoader));
        _random = new Random();
    }

    /// <summary>
    /// Spawns resource nodes in a location based on biome and density settings.
    /// </summary>
    /// <param name="locationId">The unique identifier for the location.</param>
    /// <param name="biome">The biome type (e.g., "forest", "mountains", "desert").</param>
    /// <param name="density">The density level determining spawn count ("abundant", "high", "medium", "low", "rare").</param>
    /// <returns>A list of spawned harvestable nodes with unique IDs and full health.</returns>
    public List<HarvestableNode> SpawnNodes(string locationId, string biome, string density = "medium")
    {
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));

        if (string.IsNullOrWhiteSpace(biome))
            throw new ArgumentException("Biome cannot be null or empty", nameof(biome));

        _logger.LogInformation("Spawning nodes for location {LocationId}, biome: {Biome}, density: {Density}",
            locationId, biome, density);

        // Load node definitions matching the biome
        _nodeLoader.LoadNodes();
        var availableNodes = _nodeLoader.GetNodesByBiome(biome);

        if (availableNodes.Count == 0)
        {
            _logger.LogWarning("No nodes found for biome: {Biome}", biome);
            return new List<HarvestableNode>();
        }

        // Determine spawn count based on density
        int spawnCount = GetSpawnCountForDensity(density);
        _logger.LogDebug("Spawn count for density '{Density}': {SpawnCount}", density, spawnCount);

        // Spawn nodes using weighted random selection
        var spawnedNodes = new List<HarvestableNode>();
        for (int i = 0; i < spawnCount; i++)
        {
            var selectedNode = SelectNodeByRarityWeight(availableNodes.ToList());
            if (selectedNode == null)
            {
                _logger.LogWarning("Failed to select a node (all weights may be 0)");
                continue;
            }

            // Create instance with unique ID
            var nodeInstance = new HarvestableNode
            {
                NodeId = $"{locationId}_{selectedNode.NodeType}_{i + 1}",
                NodeType = selectedNode.NodeType,
                DisplayName = selectedNode.Name,
                MaterialTier = selectedNode.Tier.ToString(),
                CurrentHealth = selectedNode.Health,
                MaxHealth = selectedNode.Health,
                BaseYield = selectedNode.BaseYield,
                LootTableRef = selectedNode.LootTable,
                BiomeType = biome,
                LocationId = locationId,
                MinToolTier = selectedNode.MinToolTier,
                LastHarvestedAt = DateTime.MinValue,
                TimesHarvested = 0,
                IsRichNode = false
            };

            spawnedNodes.Add(nodeInstance);
            _logger.LogDebug("Spawned node: {NodeId} ({NodeType}) at location {LocationId}",
                nodeInstance.NodeId, nodeInstance.NodeType, locationId);
        }

        _logger.LogInformation("Spawned {Count} nodes at location {LocationId}", spawnedNodes.Count, locationId);
        return spawnedNodes;
    }

    /// <summary>
    /// Gets the number of nodes to spawn based on density level.
    /// </summary>
    /// <param name="density">The density level string.</param>
    /// <returns>The number of nodes to spawn.</returns>
    private int GetSpawnCountForDensity(string density)
    {
        return density.ToLowerInvariant() switch
        {
            "abundant" => _random.Next(5, 8),   // 5-7 nodes
            "high" => _random.Next(3, 5),       // 3-4 nodes
            "medium" => _random.Next(2, 4),     // 2-3 nodes
            "low" => _random.Next(1, 3),        // 1-2 nodes
            "rare" => _random.Next(0, 2),       // 0-1 nodes
            _ => _random.Next(2, 4)             // Default: medium
        };
    }

    /// <summary>
    /// Selects a node from the available nodes using weighted random selection based on rarityWeight.
    /// Higher rarityWeight = more common = higher selection probability.
    /// </summary>
    /// <param name="nodes">The list of available node references.</param>
    /// <returns>The selected node reference, or null if selection failed.</returns>
    private HarvestableNodeReference? SelectNodeByRarityWeight(List<HarvestableNodeReference> nodes)
    {
        if (nodes.Count == 0)
            return null;

        // Calculate total weight
        int totalWeight = nodes.Sum(n => n.RarityWeight);
        if (totalWeight <= 0)
        {
            _logger.LogWarning("Total rarity weight is 0 or negative, cannot select node");
            return null;
        }

        // Roll random number
        int roll = _random.Next(1, totalWeight + 1);

        // Select node based on weight
        int currentWeight = 0;
        foreach (var node in nodes)
        {
            currentWeight += node.RarityWeight;
            if (roll <= currentWeight)
            {
                _logger.LogDebug("Selected node {NodeType} (weight: {Weight}, roll: {Roll}/{TotalWeight})",
                    node.NodeType, node.RarityWeight, roll, totalWeight);
                return node;
            }
        }

        // Fallback (should never reach here)
        _logger.LogWarning("Node selection fallback triggered, returning first node");
        return nodes[0];
    }

    /// <summary>
    /// Respawns an exhausted node after a cooldown period.
    /// </summary>
    /// <param name="node">The exhausted node to respawn.</param>
    /// <param name="cooldownMinutes">The cooldown period in minutes (default: 60 minutes).</param>
    /// <returns>True if the node was respawned, false if cooldown hasn't elapsed.</returns>
    public bool RespawnNode(HarvestableNode node, int cooldownMinutes = 60)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (node.CanHarvest())
        {
            _logger.LogDebug("Node {NodeId} can still be harvested, no respawn needed", node.NodeId);
            return false;
        }

        if (node.LastHarvestedAt == DateTime.MinValue)
        {
            _logger.LogWarning("Node {NodeId} is exhausted but has no LastHarvestedAt time", node.NodeId);
            return false;
        }

        var elapsedTime = DateTime.UtcNow - node.LastHarvestedAt;
        if (elapsedTime.TotalMinutes < cooldownMinutes)
        {
            _logger.LogDebug("Node {NodeId} cooldown not elapsed: {Elapsed}/{Required} minutes",
                node.NodeId, elapsedTime.TotalMinutes, cooldownMinutes);
            return false;
        }

        // Respawn the node
        node.CurrentHealth = node.MaxHealth;
        node.LastHarvestedAt = DateTime.MinValue;
        node.TimesHarvested = 0;

        _logger.LogInformation("Respawned node {NodeId} at location {LocationId}",
            node.NodeId, node.LocationId);

        return true;
    }
}
