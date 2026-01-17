namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a magical essence that can be socketed into items.
/// Provides elemental effects and bonuses.
/// </summary>
public class Essence : ISocketable
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this essence (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Socket type this essence fits into (always Essence).
    /// </summary>
    public SocketType SocketType { get; set; } = SocketType.Essence;
    
    /// <summary>
    /// Thematic category (fire, shadow, arcane, etc.) for organization only.
    /// Does not restrict socket compatibility.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Rarity of the essence.
    /// </summary>
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    
    /// <summary>
    /// Market value of this essence.
    /// </summary>
    public int Price { get; set; }
    
    /// <summary>
    /// Traits provided by this essence when socketed.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>
    /// Rarity weight for procedural generation.
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Get a display string showing essence stats.
    /// </summary>
    public string GetDisplayName()
    {
        var traitsSummary = string.Join(", ", Traits.Select(t => $"{t.Key}: {t.Value.AsString()}"));
        return $"{Name} ({Category} Essence) - {traitsSummary}";
    }
}
