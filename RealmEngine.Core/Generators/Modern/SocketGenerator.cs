using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates sockets for items based on rarity, item type, and material.
/// Configuration loaded from general/socket_config.json.
/// </summary>
public class SocketGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<SocketGenerator> _logger;
    private readonly Random _random;
    private readonly JObject? _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketGenerator"/> class.
    /// </summary>
    public SocketGenerator(GameDataCache dataCache, ILogger<SocketGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
        
        // Load socket configuration
        var configPath = "general/socket_config.json";
        if (_dataCache.FileExists(configPath))
        {
            var file = _dataCache.GetFile(configPath);
            _config = file?.JsonData;
            
            if (_config != null)
            {
                _logger.LogInformation("Successfully loaded socket configuration from {Path}", configPath);
            }
        }
        
        if (_config == null)
        {
            _logger.LogWarning("Socket configuration not found at {Path}. Socket generation will be limited.", configPath);
        }
    }

    /// <summary>
    /// Generate sockets for an item based on rarity, item type, and material.
    /// </summary>
    public List<Socket> GenerateSockets(ItemRarity rarity, ItemType itemType, string? material)
    {
        if (_config == null) return new List<Socket>();
        
        try
        {
            // Determine socket count
            int socketCount = GetSocketCount(rarity, material);
            if (socketCount == 0) return new List<Socket>();
            
            // Generate sockets with appropriate types
            var sockets = new List<Socket>();
            var typeWeights = GetSocketTypeWeights(itemType);
            
            for (int i = 0; i < socketCount; i++)
            {
                var socketType = SelectSocketType(typeWeights);
                sockets.Add(new Socket 
                { 
                    Type = socketType, 
                    LinkGroup = -1 // Unlinked by default
                });
            }
            
            // Apply material-specific socket type bonuses
            ApplyMaterialSocketTypeBias(sockets, material);
            
            // Assign link groups
            AssignLinkGroups(sockets, rarity, material);
            
            return sockets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sockets for {Rarity} {ItemType} with material {Material}", 
                rarity, itemType, material);
            return new List<Socket>();
        }
    }

    /// <summary>
    /// Get socket count based on rarity and material bonuses.
    /// </summary>
    private int GetSocketCount(ItemRarity rarity, string? material)
    {
        if (_config?["socketCounts"] == null) return 0;
        
        var rarityKey = rarity.ToString();
        var socketCountConfig = _config["socketCounts"]?[rarityKey];
        if (socketCountConfig == null)
        {
            _logger.LogWarning("No socket count configuration for rarity {Rarity}", rarity);
            return 0;
        }
        
        // Get base chances array
        var chances = socketCountConfig["chances"]?.ToObject<int[]>();
        if (chances == null || chances.Length == 0) return 0;
        
        // Apply material bonuses (Rare+ materials only)
        var materialBonus = GetMaterialBonus(material);
        if (materialBonus != null)
        {
            chances = ApplySocketCountBonus(chances, materialBonus);
        }
        
        // Select weighted random socket count
        return SelectWeightedIndex(chances);
    }

    /// <summary>
    /// Apply material bonus to socket count chances.
    /// </summary>
    private int[] ApplySocketCountBonus(int[] baseChances, MaterialBonus bonus)
    {
        var modifiedChances = new int[baseChances.Length];
        Array.Copy(baseChances, modifiedChances, baseChances.Length);
        
        // Increase chance for higher socket counts
        int bonusPercent = bonus.SocketChanceBonus;
        for (int i = 1; i < modifiedChances.Length; i++)
        {
            modifiedChances[i] = (int)(modifiedChances[i] * (1.0 + bonusPercent / 100.0));
        }
        
        // Apply guaranteed sockets if specified
        if (bonus.GuaranteedSockets > 0 && bonus.GuaranteedSockets < modifiedChances.Length)
        {
            // Zero out chances below guaranteed minimum
            for (int i = 0; i < bonus.GuaranteedSockets; i++)
            {
                modifiedChances[i] = 0;
            }
        }
        
        return modifiedChances;
    }

    /// <summary>
    /// Get socket type weights based on item type.
    /// </summary>
    private Dictionary<SocketType, int> GetSocketTypeWeights(ItemType itemType)
    {
        if (_config?["socketTypeWeights"] == null)
        {
            // Default balanced weights
            return new Dictionary<SocketType, int>
            {
                { SocketType.Gem, 25 },
                { SocketType.Rune, 25 },
                { SocketType.Crystal, 25 },
                { SocketType.Orb, 25 }
            };
        }
        
        // Determine weight category based on item type
        string category = GetSocketCategory(itemType);
        
        var weightsConfig = _config["socketTypeWeights"]?[category];
        if (weightsConfig == null)
        {
            _logger.LogWarning("No socket type weights for category {Category}", category);
            category = "Accessory"; // Fallback to balanced
            weightsConfig = _config["socketTypeWeights"]?[category];
        }
        
        return new Dictionary<SocketType, int>
        {
            { SocketType.Gem, weightsConfig?["Gem"]?.Value<int>() ?? 25 },
            { SocketType.Rune, weightsConfig?["Rune"]?.Value<int>() ?? 25 },
            { SocketType.Crystal, weightsConfig?["Crystal"]?.Value<int>() ?? 25 },
            { SocketType.Orb, weightsConfig?["Orb"]?.Value<int>() ?? 25 }
        };
    }

    /// <summary>
    /// Select a socket type based on weighted probabilities.
    /// </summary>
    private SocketType SelectSocketType(Dictionary<SocketType, int> weights)
    {
        int totalWeight = weights.Values.Sum();
        int roll = _random.Next(totalWeight);
        int cumulative = 0;
        
        foreach (var kvp in weights)
        {
            cumulative += kvp.Value;
            if (roll < cumulative)
            {
                return kvp.Key;
            }
        }
        
        return SocketType.Gem; // Fallback
    }

    /// <summary>
    /// Apply material-specific socket type bias (e.g., Dragonscale favors Crystal).
    /// </summary>
    private void ApplyMaterialSocketTypeBias(List<Socket> sockets, string? material)
    {
        if (string.IsNullOrEmpty(material)) return;
        
        var materialBonus = GetMaterialBonus(material);
        if (materialBonus?.SpecialEffect != null && 
            materialBonus.SpecialEffect.Contains("Crystal", StringComparison.OrdinalIgnoreCase))
        {
            // Dragonscale: Higher chance for Crystal socket type
            // Convert one random socket to Crystal if none exist
            if (sockets.Any() && !sockets.Any(s => s.Type == SocketType.Crystal))
            {
                var randomIndex = _random.Next(sockets.Count);
                sockets[randomIndex].Type = SocketType.Crystal;
            }
        }
    }

    /// <summary>
    /// Assign link groups to sockets based on rarity and material.
    /// </summary>
    private void AssignLinkGroups(List<Socket> sockets, ItemRarity rarity, string? material)
    {
        if (sockets.Count < 2) return; // Need at least 2 sockets to link
        if (_config?["linkChances"] == null) return;
        
        var rarityKey = rarity.ToString();
        var linkConfig = _config["linkChances"]?[rarityKey];
        if (linkConfig == null) return;
        
        // Get link chances (modified by material)
        var materialBonus = GetMaterialBonus(material);
        int linkBonus = materialBonus?.LinkChanceBonus ?? 0;
        
        // Legendary: Guaranteed 3-link
        if (rarity == ItemRarity.Legendary)
        {
            AssignLinkGroup(sockets, 0, 3, 0); // Link first 3 sockets
            
            // Check for 4-link (30% + bonus)
            int fourLinkChance = (linkConfig["fourLink"]?.Value<int>() ?? 30) + linkBonus;
            if (_random.Next(100) < fourLinkChance && sockets.Count >= 4)
            {
                sockets[3].LinkGroup = 0; // Extend to 4-link
            }
            
            // Check for 5-link (10% + bonus)
            int fiveLinkChance = (linkConfig["fiveLink"]?.Value<int>() ?? 10) + linkBonus;
            if (_random.Next(100) < fiveLinkChance && sockets.Count >= 5 && sockets[3].LinkGroup == 0)
            {
                sockets[4].LinkGroup = 0; // Extend to 5-link
            }
            
            // Assign remaining sockets to separate groups
            AssignRemainingLinks(sockets, 1);
        }
        // Epic: 50% for 2-link, 20% for 3-link
        else if (rarity == ItemRarity.Epic)
        {
            int twoLinkChance = (linkConfig["twoLink"]?.Value<int>() ?? 50) + linkBonus;
            int threeLinkChance = (linkConfig["threeLink"]?.Value<int>() ?? 20) + linkBonus;
            
            if (_random.Next(100) < threeLinkChance && sockets.Count >= 3)
            {
                AssignLinkGroup(sockets, 0, 3, 0);
            }
            else if (_random.Next(100) < twoLinkChance)
            {
                AssignLinkGroup(sockets, 0, 2, 0);
            }
            
            AssignRemainingLinks(sockets, 1);
        }
        // Rare: 30% for 2-link
        else if (rarity == ItemRarity.Rare)
        {
            int twoLinkChance = (linkConfig["twoLink"]?.Value<int>() ?? 30) + linkBonus;
            
            if (_random.Next(100) < twoLinkChance)
            {
                AssignLinkGroup(sockets, 0, 2, 0);
            }
            
            AssignRemainingLinks(sockets, 1);
        }
    }

    /// <summary>
    /// Assign a specific link group to a range of sockets.
    /// </summary>
    private void AssignLinkGroup(List<Socket> sockets, int startIndex, int count, int groupId)
    {
        int endIndex = Math.Min(startIndex + count, sockets.Count);
        for (int i = startIndex; i < endIndex; i++)
        {
            sockets[i].LinkGroup = groupId;
        }
    }

    /// <summary>
    /// Assign remaining unlinked sockets to individual groups or small links.
    /// </summary>
    private void AssignRemainingLinks(List<Socket> sockets, int startingGroupId)
    {
        int currentGroup = startingGroupId;
        for (int i = 0; i < sockets.Count; i++)
        {
            if (sockets[i].LinkGroup == -1)
            {
                // Check if we can make a 2-link with next socket
                if (i + 1 < sockets.Count && sockets[i + 1].LinkGroup == -1 && _random.Next(100) < 20)
                {
                    sockets[i].LinkGroup = currentGroup;
                    sockets[i + 1].LinkGroup = currentGroup;
                    currentGroup++;
                    i++; // Skip next socket
                }
                // Otherwise leave unlinked (-1)
            }
        }
    }

    /// <summary>
    /// Get material bonus configuration from socket_config.json.
    /// </summary>
    private MaterialBonus? GetMaterialBonus(string? material)
    {
        if (string.IsNullOrEmpty(material)) return null;
        if (_config?["materialBonuses"] == null) return null;
        
        var bonusConfig = _config["materialBonuses"]?[material];
        if (bonusConfig == null) return null;
        
        return new MaterialBonus
        {
            SocketChanceBonus = bonusConfig["socketChanceBonus"]?.Value<int>() ?? 0,
            LinkChanceBonus = bonusConfig["linkChanceBonus"]?.Value<int>() ?? 0,
            GuaranteedSockets = bonusConfig["guaranteedSockets"]?.Value<int>() ?? 0,
            Tier = bonusConfig["tier"]?.Value<string>() ?? "Common",
            SpecialEffect = bonusConfig["specialEffect"]?.Value<string>()
        };
    }

    /// <summary>
    /// Select a weighted random index from an array of weights.
    /// </summary>
    private int SelectWeightedIndex(int[] weights)
    {
        int totalWeight = weights.Sum();
        if (totalWeight == 0) return 0;
        
        int roll = _random.Next(totalWeight);
        int cumulative = 0;
        
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                return i;
            }
        }
        
        return 0;
    }

    /// <summary>
    /// Categorize item type for socket weight selection (Weapon, Armor, Accessory).
    /// </summary>
    private string GetSocketCategory(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "Weapon",
            ItemType.Shield => "Armor",
            ItemType.OffHand => "Accessory",
            ItemType.Helmet => "Armor",
            ItemType.Shoulders => "Armor",
            ItemType.Chest => "Armor",
            ItemType.Bracers => "Armor",
            ItemType.Gloves => "Armor",
            ItemType.Belt => "Armor",
            ItemType.Legs => "Armor",
            ItemType.Boots => "Armor",
            ItemType.Necklace => "Accessory",
            ItemType.Ring => "Accessory",
            _ => "Accessory" // Default for quest items, materials, etc.
        };
    }

    /// <summary>
    /// Internal class to represent material bonus configuration.
    /// </summary>
    private class MaterialBonus
    {
        public int SocketChanceBonus { get; set; }
        public int LinkChanceBonus { get; set; }
        public int GuaranteedSockets { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string? SpecialEffect { get; set; }
    }
}
