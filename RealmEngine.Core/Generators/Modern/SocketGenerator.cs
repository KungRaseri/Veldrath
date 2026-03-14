using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates socket slots for items using socket configuration from the database or defaults.</summary>
public class SocketGenerator(GameConfigService configService, ILogger<SocketGenerator> logger)
{
    private readonly Random _random = new();

    private static readonly Dictionary<SocketType, int> DefaultTypeWeights = new()
    {
        [SocketType.Gem] = 25,
        [SocketType.Rune] = 25,
        [SocketType.Crystal] = 25,
        [SocketType.Orb] = 25,
    };

    /// <summary>Generates sockets for an item based on rarity and item type.</summary>
    public Dictionary<SocketType, List<Socket>> GenerateSockets(ItemRarity rarity, ItemType itemType, string? material)
    {
        var sockets = new Dictionary<SocketType, List<Socket>>();

        try
        {
            var configJson = configService.GetData("socket-config");
            var config = configJson is not null ? JObject.Parse(configJson) : null;

            var socketCount = DetermineSocketCount(config, rarity);
            if (socketCount == 0) return sockets;

            var typeWeights = GetTypeWeights(config, itemType.ToString());

            var allSockets = new List<(SocketType type, Socket socket)>();
            for (int i = 0; i < socketCount; i++)
            {
                var socketType = SelectSocketType(typeWeights);
                allSockets.Add((socketType, new Socket { Type = socketType }));
            }

            foreach (var (socketType, socket) in allSockets)
            {
                if (!sockets.ContainsKey(socketType))
                    sockets[socketType] = [];
                sockets[socketType].Add(socket);
            }

            logger.LogDebug("Generated {Count} sockets for {Rarity} {ItemType}", socketCount, rarity, itemType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sockets for {Rarity} {ItemType}", rarity, itemType);
        }

        return sockets;
    }

    private int DetermineSocketCount(JObject? config, ItemRarity rarity)
    {
        var chances = config?["socketCounts"]?[rarity.ToString()]?["chances"]?.Values<int>().ToArray();
        if (chances is null || chances.Length == 0)
        {
            // Default: rare+ gets 1-2 sockets, others none
            return rarity >= ItemRarity.Rare ? _random.Next(1, 3) : 0;
        }

        var total = chances.Sum();
        if (total == 0) return 0;

        var roll = _random.Next(total);
        var cumulative = 0;
        for (int i = 0; i < chances.Length; i++)
        {
            cumulative += chances[i];
            if (roll < cumulative) return i;
        }
        return 0;
    }

    private Dictionary<SocketType, int> GetTypeWeights(JObject? config, string itemTypeName)
    {
        var node = config?["socketTypeWeights"]?[itemTypeName];
        if (node is null) return DefaultTypeWeights;

        return new Dictionary<SocketType, int>
        {
            [SocketType.Gem] = node["Gem"]?.Value<int>() ?? 25,
            [SocketType.Rune] = node["Rune"]?.Value<int>() ?? 25,
            [SocketType.Crystal] = node["Crystal"]?.Value<int>() ?? 25,
            [SocketType.Orb] = node["Orb"]?.Value<int>() ?? 25,
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
