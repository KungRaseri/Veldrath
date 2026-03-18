using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Queries;

/// <summary>
/// Query to retrieve all enchantments on an item, including slot availability and rate information.
/// </summary>
public record GetEnchantmentsQuery(Item Item, int EnchantingSkillRank = 0) : IRequest<GetEnchantmentsResult>;

/// <summary>
/// Result containing enchantment and slot details for an item.
/// </summary>
public class GetEnchantmentsResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the item name.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Gets or sets the item rarity.</summary>
    public ItemRarity Rarity { get; set; }

    /// <summary>Gets or sets the enchantments currently applied by the player.</summary>
    public List<EnchantmentSlotInfo> PlayerEnchantments { get; set; } = new();

    /// <summary>Gets or sets the enchantments baked in at creation (read-only).</summary>
    public List<EnchantmentSlotInfo> InherentEnchantments { get; set; } = new();

    /// <summary>Gets or sets the number of slots currently unlocked.</summary>
    public int UnlockedSlots { get; set; }

    /// <summary>Gets or sets the maximum slots for this item's rarity.</summary>
    public int MaxPossibleSlots { get; set; }

    /// <summary>Gets or sets the per-slot success rate summary.</summary>
    public EnchantingRateSummary? RateSummary { get; set; }

    /// <summary>Gets or sets a value indicating whether the item can accept more enchantments.</summary>
    public bool CanEnchant { get; set; }
}

/// <summary>
/// Detail about a single enchantment on an item.
/// </summary>
public class EnchantmentSlotInfo
{
    /// <summary>Gets or sets the zero-based slot index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the enchantment name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the enchantment description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the enchantment rarity.</summary>
    public EnchantmentRarity Rarity { get; set; }

    /// <summary>Gets or sets the stat traits granted by this enchantment.</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
}
