namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an enchantment that can be applied to an item.
/// Part of the Hybrid Enhancement System v1.0.
/// </summary>
public class Enchantment : ITraitable
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this enchantment (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name shown in UI.
    /// May differ from internal Name. Maps to "displayName" in JSON.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the rarity.</summary>
    public EnchantmentRarity Rarity { get; set; } = EnchantmentRarity.Minor;
    
    /// <summary>
    /// Gets or sets the monetary value of this enchantment.
    /// Used in budgeting and pricing calculations.
    /// </summary>
    public int Value { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the weight of this enchantment.
    /// Typically 0 for magical effects.
    /// </summary>
    public double Weight { get; set; } = 0.0;
    
    /// <summary>
    /// Gets or sets the magical effect description.
    /// Examples: "Deals fire damage", "Increases defense"
    /// </summary>
    public string? Effect { get; set; }
    
    /// <summary>
    /// Gets or sets the power level of the enchantment (1-100).
    /// Higher values = stronger effects.
    /// </summary>
    public int Power { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the duration of the effect in rounds/turns.
    /// 0 = permanent/passive effect.
    /// </summary>
    public int Duration { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets attribute bonuses provided by this enchantment.
    /// Key = attribute name (strength, intelligence, etc.), Value = bonus amount.
    /// Maps to "attributes" in JSON.
    /// </summary>
    public Dictionary<string, int> Attributes { get; set; } = new();

    /// <summary>Gets or sets the trait-based bonuses.</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Gets or sets the position in the name (prefix or suffix).</summary>
    public EnchantmentPosition Position { get; set; } = EnchantmentPosition.Suffix;
    
    /// <summary>Gets or sets the rarity weight for procedural generation.</summary>
    public int RarityWeight { get; set; } = 50;



    /// <summary>Gets or sets the special effect.</summary>
    public string? SpecialEffect { get; set; }

    /// <summary>Gets or sets the enchantment level.</summary>
    public int Level { get; set; } = 1;
}

/// <summary>
/// Position of enchantment in item name.
/// </summary>
public enum EnchantmentPosition
{
    /// <summary>Prefix position (e.g., "Flaming" in "Flaming Sword").</summary>
    Prefix,
    /// <summary>Suffix position (e.g., "of Fire" in "Sword of Fire").</summary>
    Suffix
}

/// <summary>
/// Rarity levels for enchantments.
/// </summary>
public enum EnchantmentRarity
{
    /// <summary>Minor enchantment.</summary>
    Minor,
    /// <summary>Lesser enchantment.</summary>
    Lesser,
    /// <summary>Greater enchantment.</summary>
    Greater,
    /// <summary>Superior enchantment.</summary>
    Superior,
    /// <summary>Legendary enchantment.</summary>
    Legendary
}
