using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models.Harvesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealmEngine.Data.Services
{
    /// <summary>
    /// Service responsible for loading and caching resource node definitions from JSON.
    /// Provides fast lookup of node types by ID and supports filtering by biome/tier.
    /// Uses GameDataCache for efficient data access.
    /// </summary>
    public class ResourceNodeLoaderService
    {
        private readonly ILogger<ResourceNodeLoaderService> _logger;
        private readonly GameDataCache _dataCache;
        private Dictionary<string, HarvestableNodeReference>? _nodeCache;
        private bool _isLoaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceNodeLoaderService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for diagnostic output.</param>
        /// <param name="dataCache">The game data cache instance.</param>
        public ResourceNodeLoaderService(ILogger<ResourceNodeLoaderService> logger, GameDataCache dataCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
            _isLoaded = false;
        }

        /// <summary>
        /// Loads resource nodes from resource-nodes.json and caches them in memory.
        /// Safe to call multiple times - subsequent calls return cached data.
        /// </summary>
        public void LoadNodes()
        {
            if (_isLoaded)
            {
                _logger.LogDebug("Resource nodes already loaded, returning cached data");
                return;
            }

            var cachedFile = _dataCache.GetFile("configuration/resource-nodes.json");
            if (cachedFile == null)
            {
                _logger.LogError("Resource nodes file not found in cache: configuration/resource-nodes.json");
                throw new FileNotFoundException("Resource nodes file not found: configuration/resource-nodes.json");
            }

            try
            {
                var rawNodeData = cachedFile.JsonData;
                _nodeCache = new Dictionary<string, HarvestableNodeReference>();

                var nodeTypes = rawNodeData["node_types"] as JObject;
                if (nodeTypes == null)
                {
                    _logger.LogError("Invalid resource-nodes.json structure: missing 'node_types'");
                    throw new InvalidOperationException("Invalid resource-nodes.json structure");
                }

                // Parse all node categories (ore_veins, trees, herb_patches, etc.)
                foreach (var category in nodeTypes.Properties())
                {
                    var categoryNodes = category.Value as JObject;
                    if (categoryNodes == null) continue;

                    foreach (var nodeProperty in categoryNodes.Properties())
                    {
                        var nodeId = nodeProperty.Name;
                        var nodeData = nodeProperty.Value as JObject;
                        if (nodeData == null) continue;

                        try
                        {
                            var nodeRef = ParseNodeReference(nodeId, nodeData);
                            _nodeCache[nodeId] = nodeRef;
                            _logger.LogTrace("Loaded node: {NodeId} ({NodeName})", nodeId, nodeRef.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to parse node: {NodeId}", nodeId);
                        }
                    }
                }

                _isLoaded = true;
                _logger.LogInformation("Loaded {NodeCount} resource nodes from cache", _nodeCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load resource nodes from cache");
                throw;
            }
        }

        /// <summary>
        /// Gets a specific resource node by its ID.
        /// </summary>
        /// <param name="nodeId">The node ID (e.g., "copper_vein", "oak_tree")</param>
        /// <returns>The node reference, or null if not found</returns>
        public HarvestableNodeReference? GetNodeById(string nodeId)
        {
            if (!_isLoaded)
            {
                LoadNodes();
            }

            return _nodeCache?.TryGetValue(nodeId, out var node) == true ? node : null;
        }

        /// <summary>
        /// Gets all resource nodes.
        /// </summary>
        public IReadOnlyList<HarvestableNodeReference> GetAllNodes()
        {
            if (!_isLoaded)
            {
                LoadNodes();
            }

            return _nodeCache?.Values.ToList() ?? new List<HarvestableNodeReference>();
        }

        /// <summary>
        /// Gets all nodes that can spawn in the specified biome.
        /// </summary>
        /// <param name="biome">The biome name (e.g., "forest", "mountains")</param>
        /// <returns>List of nodes that spawn in this biome</returns>
        public IReadOnlyList<HarvestableNodeReference> GetNodesByBiome(string biome)
        {
            if (!_isLoaded)
            {
                LoadNodes();
            }

            return _nodeCache?.Values
                .Where(n => n.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase))
                .ToList() ?? new List<HarvestableNodeReference>();
        }

        /// <summary>
        /// Gets all nodes of a specific tier.
        /// </summary>
        /// <param name="tier">The tier (e.g., "common", "uncommon", "rare", "epic")</param>
        public IReadOnlyList<HarvestableNodeReference> GetNodesByTier(string tier)
        {
            if (!_isLoaded)
            {
                LoadNodes();
            }

            return _nodeCache?.Values
                .Where(n => n.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<HarvestableNodeReference>();
        }

        /// <summary>
        /// Gets all nodes that require a specific skill.
        /// </summary>
        /// <param name="skillRef">The skill reference (e.g., "@skills/profession:mining")</param>
        public IReadOnlyList<HarvestableNodeReference> GetNodesBySkill(string skillRef)
        {
            if (!_isLoaded)
            {
                LoadNodes();
            }

            return _nodeCache?.Values
                .Where(n => n.SkillRef.Equals(skillRef, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<HarvestableNodeReference>();
        }

        /// <summary>
        /// Parses a single node from JSON into a HarvestableNodeReference object.
        /// </summary>
        private HarvestableNodeReference ParseNodeReference(string nodeId, JObject nodeData)
        {
            var name = nodeData["name"]?.Value<string>() ?? nodeId;
            var tier = nodeData["tier"]?.Value<string>() ?? "common";
            var skillRef = nodeData["skill"]?.Value<string>() ?? string.Empty;
            var minToolTier = nodeData["minToolTier"]?.Value<int>() ?? 0;
            var health = nodeData["health"]?.Value<int>() ?? 100;
            var baseYield = nodeData["baseYield"]?.Value<int>() ?? 1;
            var lootTable = nodeData["lootTable"]?.Value<string>() ?? string.Empty;
            var rarityWeight = nodeData["rarityWeight"]?.Value<int>() ?? 50;
            var icon = nodeData["icon"]?.Value<string>() ?? "TreePine";

            var biomes = nodeData["biomes"]?.Values<string>()
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b!)
                .ToList() ?? new List<string>();

            return new HarvestableNodeReference
            {
                NodeType = nodeId,
                Name = name,
                Tier = tier,
                SkillRef = skillRef,
                MinToolTier = minToolTier,
                Health = health,
                BaseYield = baseYield,
                LootTable = lootTable,
                Biomes = biomes,
                RarityWeight = rarityWeight,
                Icon = icon
            };
        }

        /// <summary>
        /// Clears the cached node data. Useful for testing or reloading config.
        /// </summary>
        public void ClearCache()
        {
            _nodeCache?.Clear();
            _isLoaded = false;
            _logger.LogDebug("Resource node cache cleared");
        }
    }
}
