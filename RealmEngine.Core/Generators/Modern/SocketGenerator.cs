using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates socket slots for items using database-driven socket configuration.</summary>
public class SocketGenerator(SocketConfig socketConfig, ILogger<SocketGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates sockets for an item based on its rarity and item type.</summary>
    public Dictionary<SocketType, List<Socket>> GenerateSockets(ItemRarity rarity, ItemType itemType, string? material)
    {
        var sockets = new Dictionary<SocketType, List<Socket>>();

        try
        {
            var socketCount = DetermineSocketCount(rarity);
            if (socketCount == 0) return sockets;

            var typeWeights = GetTypeWeights(itemType.ToString());

            for (int i = 0; i < socketCount; i++)
            {
                var socketType = SelectSocketType(typeWeights);
                if (!sockets.ContainsKey(socketType))
                    sockets[socketType] = [];
                sockets[socketType].Add(new Socket { Type = socketType });
            }

            logger.LogDebug("Generated {Count} sockets for {Rarity} {ItemType}", socketCount, rarity, itemType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sockets for {Rarity} {ItemType}", rarity, itemType);
        }

        return sockets;
    }

    private int DetermineSocketCount(ItemRarity rarity)
    {
        if (socketConfig.SocketCounts.TryGetValue(rarity.ToString(), out var entry)
            && entry.Chances.Length > 0)
        {
            var total = entry.Chances.Sum();
            if (total == 0) return 0;

            var roll = _random.Next(total);
            var cumulative = 0;
            for (int i = 0; i < entry.Chances.Length; i++)
            {
                cumulative += entry.Chances[i];
                if (roll < cumulative) return i;
            }
            return 0;
        }

        // Type-default fallback (used when DB has no socket-config rows yet)
        return rarity >= ItemRarity.Rare ? _random.Next(1, 3) : 0;
    }

    private Dictionary<SocketType, int> GetTypeWeights(string itemTypeName)
    {
        if (socketConfig.SocketTypeWeights.TryGetValue(itemTypeName, out var w))
        {
            return new Dictionary<SocketType, int>
            {
                [SocketType.Gem]     = w.Gem,
                [SocketType.Rune]    = w.Rune,
                [SocketType.Crystal] = w.Crystal,
                [SocketType.Orb]     = w.Orb,
            };
        }

        // Type-default fallback: equal distribution across all socket types
        return new Dictionary<SocketType, int>
        {
            [SocketType.Gem] = 25, [SocketType.Rune] = 25,
            [SocketType.Crystal] = 25, [SocketType.Orb] = 25,
        };
    }

    private SocketType SelectSocketType(Dictionary<SocketType, int> weights)
    {
        var total = weights.Values.Sum();
        var roll = _random.Next(total);
        var cumulative = 0;
        foreach (var (type, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative) return type;
        }
        return SocketType.Gem;
    }
}

