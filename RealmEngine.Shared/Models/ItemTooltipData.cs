namespace RealmEngine.Shared.Models;

/// <summary>
/// Structured tooltip data for items, showing bonuses broken down by source.
/// Enables clear attribution of traits to specific components.
/// </summary>
public class ItemTooltipData
{
    /// <summary>
    /// Gets or sets the full display name of the item.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item rarity.
    /// </summary>
    public ItemRarity Rarity { get; set; }

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public ItemType Type { get; set; }

    /// <summary>
    /// Gets or sets the base item section (damage, armor, weight, requirements).
    /// </summary>
    public TooltipSection BaseSection { get; set; } = new();

    /// <summary>
    /// Gets or sets the quality section (if quality is present).
    /// </summary>
    public TooltipSection? QualitySection { get; set; }

    /// <summary>
    /// Gets or sets the material section (if material is present).
    /// </summary>
    public TooltipSection? MaterialSection { get; set; }

    /// <summary>
    /// Gets or sets the prefix sections (one per prefix).
    /// </summary>
    public List<TooltipSection> PrefixSections { get; set; } = new();

    /// <summary>
    /// Gets or sets the suffix sections (one per suffix).
    /// </summary>
    public List<TooltipSection> SuffixSections { get; set; } = new();

    /// <summary>
    /// Gets or sets the enchantment sections (one per enchantment).
    /// </summary>
    public List<TooltipSection> EnchantmentSections { get; set; } = new();

    /// <summary>
    /// Gets or sets the socket sections (one per socketed gem/rune).
    /// </summary>
    public List<TooltipSection> SocketSections { get; set; } = new();

    /// <summary>
    /// Gets or sets the lore/flavor text for the item.
    /// </summary>
    public string? Lore { get; set; }
}

/// <summary>
/// Represents a section of tooltip data with a header and list of bonuses.
/// </summary>
public class TooltipSection
{
    /// <summary>
    /// Gets or sets the section header (e.g., "Quality: Fine", "Material: Iron", "Flaming").
    /// </summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bonuses provided by this section.
    /// Each string is a formatted line (e.g., "+5 Fire Damage", "+10% Attack Speed").
    /// </summary>
    public List<string> Bonuses { get; set; } = new();
}
