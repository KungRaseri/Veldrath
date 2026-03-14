using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service responsible for spawning harvestable resource nodes in game locations based on biome and density rules.
/// </summary>
public class NodeSpawnerService
{
    private readonly ILogger<NodeSpawnerService> _logger;
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly Random _random;

    public NodeSpawnerService(
        ILogger<NodeSpawnerService> logger,
        IDbContextFactory<ContentDbContext> dbFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _random = new Random();
    }

    /// <summary>
    /// Spawns resource nodes in a location based on biome and density settings.
    /// </summary>
    public List<HarvestableNode> SpawnNodes(string locationId, string biome, string density = "medium")
    {
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Location ID cannot be null or empty", nameof(locationId));

        if (string.IsNullOrWhiteSpace(biome))
            throw new ArgumentException("Biome cannot be null or empty", nameof(biome));

        _logger.LogInformation("Spawning nodes for location {LocationId}, biome: {Biome}, density: {Density}",
            locationId, biome, density);

        var availableNodes = LoadNodesByBiome(biome);

        if (availableNodes.Count == 0)
        {
            _logger.LogWarning("No nodes found for biome: {Biome}", biome);
            return [];
        }

        int spawnCount = GetSpawnCountForDensity(density);
        _logger.LogDebug("Spawn count for density '{Density}': {SpawnCount}", density, spawnCount);

        var spawnedNodes = new List<HarvestableNode>();
        for (int i = 0; i < spawnCount; i++)
        {
            var selectedNode = SelectNodeByRarityWeight(availableNodes);
            if (selectedNode == null)
            {
                _logger.LogWarning("Failed to select a node (all weights may be 0)");
                continue;
            }

            var nodeInstance = new HarvestableNode
            {
                NodeId = $"{locationId}_{selectedNode.NodeType}_{i + 1}",
                NodeType = selectedNode.NodeType,
                DisplayName = selectedNode.Name,
                MaterialTier = selectedNode.Tier,
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

    // Biome → material family mapping used to derive harvestable nodes from material catalog
    private static readonly Dictionary<string, string[]> BiomeMaterialFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"]     = ["wood", "leather"],
        ["mountains"]  = ["metal", "stone", "gem"],
        ["plains"]     = ["leather", "bone"],
        ["desert"]     = ["stone", "gem"],
        ["swamp"]      = ["bone", "leather"],
        ["tundra"]     = ["bone", "stone"],
        ["dungeon"]    = ["metal", "gem"],
    };

    /// <summary>
    /// Derives HarvestableNodeReference list from material catalog filtered by biome.
    /// </summary>
    private List<HarvestableNodeReference> LoadNodesByBiome(string biome)
    {
        try
        {
            if (!BiomeMaterialFamilies.TryGetValue(biome, out var families))
                families = ["wood", "stone"];

            using var db = _dbFactory.CreateDbContext();
            var materials = db.Materials
                .Where(m => m.IsActive && families.Contains(m.MaterialFamily))
                .ToList();

            return materials.Select(m => new HarvestableNodeReference
            {
                NodeType = m.Slug,
                Name = m.DisplayName ?? m.Slug,
                Tier = GetTierFromRarity(m.RarityWeight),
                Health = 100,
                BaseYield = 1,
                LootTable = string.Empty,
                Biomes = [biome],
                RarityWeight = m.RarityWeight,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading nodes for biome {Biome}", biome);
            return [];
        }
    }

    private static string GetTierFromRarity(int rarityWeight) => rarityWeight switch
    {
        >= 50 => "common",
        >= 20 => "uncommon",
        >= 10 => "rare",
        >= 5  => "epic",
        _     => "legendary",
    };

    private int GetSpawnCountForDensity(string density) => density.ToLowerInvariant() switch
    {
        "abundant" => _random.Next(5, 8),
        "high"     => _random.Next(3, 5),
        "medium"   => _random.Next(2, 4),
        "low"      => _random.Next(1, 3),
        "rare"     => _random.Next(0, 2),
        _          => _random.Next(2, 4),
    };

    private HarvestableNodeReference? SelectNodeByRarityWeight(List<HarvestableNodeReference> nodes)
    {
        if (nodes.Count == 0) return null;

        int totalWeight = nodes.Sum(n => n.RarityWeight);
        if (totalWeight <= 0) return null;

        int roll = _random.Next(1, totalWeight + 1);
        int current = 0;
        foreach (var node in nodes)
        {
            current += node.RarityWeight;
            if (roll <= current)
                return node;
        }
        return nodes[0];
    }

    /// <summary>
    /// Respawns an exhausted node after a cooldown period.
    /// </summary>
    public bool RespawnNode(HarvestableNode node, int cooldownMinutes = 60)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (node.CanHarvest())
            return false;

        if (node.LastHarvestedAt == DateTime.MinValue)
            return false;

        var elapsed = DateTime.UtcNow - node.LastHarvestedAt;
        if (elapsed.TotalMinutes < cooldownMinutes)
            return false;

        node.CurrentHealth = node.MaxHealth;
        node.LastHarvestedAt = DateTime.MinValue;
        node.TimesHarvested = 0;

        _logger.LogInformation("Respawned node {NodeId} at location {LocationId}", node.NodeId, node.LocationId);
        return true;
    }
}
