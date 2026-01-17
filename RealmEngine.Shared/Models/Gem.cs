namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a gem that can be socketed into items (player-customizable enhancement).
/// Part of the Hybrid Enhancement System v1.0.
/// </summary>
public class Gem : ISocketable
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this gem (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Socket type this gem fits into (always Gem).
    /// </summary>
    public SocketType SocketType { get; set; } = SocketType.Gem;
    
    /// <summary>
    /// Thematic category (red, blue, green, etc.) for organization only.
    /// Does not restrict socket compatibility.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Legacy color property for backward compatibility.
    /// Maps to Category for organizational purposes.
    /// </summary>
    public GemColor Color { get; set; }
    
    /// <summary>
    /// Rarity of the gem.
    /// </summary>
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    
    /// <summary>
    /// Market value of this gem.
    /// </summary>
    public int Price { get; set; }
    
    /// <summary>
    /// Traits provided by this gem when socketed.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>
    /// Rarity weight for procedural generation.
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Get a display string showing gem stats.
    /// </summary>
    public string GetDisplayName()
    {
        var traitsSummary = string.Join(", ", Traits.Select(t => $"{t.Key}: {t.Value.AsString()}"));
        return $"{Name} ({Color} Gem) - {traitsSummary}";
    }
}
