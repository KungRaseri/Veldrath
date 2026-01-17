namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an inscribed rune that can be socketed into items.
/// Provides skill modifiers and special effects.
/// </summary>
public class Rune : ISocketable
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this rune (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Socket type this rune fits into (always Rune).
    /// </summary>
    public SocketType SocketType { get; set; } = SocketType.Rune;
    
    /// <summary>
    /// Thematic category (offensive, defensive, utility, etc.) for organization only.
    /// Does not restrict socket compatibility.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Rarity of the rune.
    /// </summary>
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    
    /// <summary>
    /// Market value of this rune.
    /// </summary>
    public int Price { get; set; }
    
    /// <summary>
    /// Traits provided by this rune when socketed.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>
    /// Rarity weight for procedural generation.
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Get a display string showing rune stats.
    /// </summary>
    public string GetDisplayName()
    {
        var traitsSummary = string.Join(", ", Traits.Select(t => $"{t.Key}: {t.Value.AsString()}"));
        return $"{Name} ({Category} Rune) - {traitsSummary}";
    }
}
