namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an energy crystal that can be socketed into items.
/// Provides resource-related effects (mana, life, energy, stamina).
/// </summary>
public class Crystal : ISocketable
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this crystal (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Socket type this crystal fits into (always Crystal).
    /// </summary>
    public SocketType SocketType { get; set; } = SocketType.Crystal;
    
    /// <summary>
    /// Thematic category (mana, life, energy, stamina, etc.) for organization only.
    /// Does not restrict socket compatibility.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Rarity of the crystal.
    /// </summary>
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    
    /// <summary>
    /// Market value of this crystal.
    /// </summary>
    public int Price { get; set; }
    
    /// <summary>
    /// Traits provided by this crystal when socketed.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>
    /// Rarity weight for procedural generation.
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Get a display string showing crystal stats.
    /// </summary>
    public string GetDisplayName()
    {
        var traitsSummary = string.Join(", ", Traits.Select(t => $"{t.Key}: {t.Value.AsString()}"));
        return $"{Name} ({Category} Crystal) - {traitsSummary}";
    }
}
