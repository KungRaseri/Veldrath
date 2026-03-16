using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Configuration for socket slot generation, loaded from the <c>socket-config</c> game config key.
/// </summary>
public class SocketConfig
{
    /// <summary>
    /// Per-rarity socket count distributions.
    /// Key = <see cref="RealmEngine.Shared.Models.ItemRarity"/> name (e.g. "Rare").
    /// Value = weighted chance array where each index represents that socket count.
    /// E.g. [0, 50, 50] → 50% chance of 1 socket, 50% chance of 2 sockets.
    /// </summary>
    [JsonProperty("socketCounts")]
    public Dictionary<string, SocketCountConfig> SocketCounts { get; set; } = new();

    /// <summary>
    /// Per-item-type socket type selection weights.
    /// Key = <see cref="RealmEngine.Shared.Models.ItemType"/> name (e.g. "Weapon").
    /// </summary>
    [JsonProperty("socketTypeWeights")]
    public Dictionary<string, SocketTypeWeightConfig> SocketTypeWeights { get; set; } = new();
}

/// <summary>Weighted chance distribution for the number of sockets generated.</summary>
public class SocketCountConfig
{
    /// <summary>
    /// Probability weights indexed by socket count.
    /// [100, 0, 0] = always 0 sockets.  [0, 50, 50] = 50% one socket, 50% two sockets.
    /// </summary>
    [JsonProperty("chances")]
    public int[] Chances { get; set; } = [];
}

/// <summary>Socket type selection weights for a specific item type.</summary>
public class SocketTypeWeightConfig
{
    [JsonProperty("Gem")]     public int Gem     { get; set; } = 25;
    [JsonProperty("Rune")]    public int Rune    { get; set; } = 25;
    [JsonProperty("Crystal")] public int Crystal { get; set; } = 25;
    [JsonProperty("Orb")]     public int Orb     { get; set; } = 25;
}
