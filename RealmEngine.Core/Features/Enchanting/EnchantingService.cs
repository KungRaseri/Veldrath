using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Enchanting;

/// <summary>
/// Service providing calculation and validation logic for the enchanting system.
/// </summary>
public class EnchantingService
{
    private readonly ILogger<EnchantingService> _logger;

    /// <summary>Initializes a new instance of <see cref="EnchantingService"/>.</summary>
    public EnchantingService(ILogger<EnchantingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates the success rate for applying an enchantment to the next available slot.
    /// Slot 1: 100% guaranteed. Slot 2: 75% + skill*0.3%. Slot 3: 50% + skill*0.3%.
    /// </summary>
    /// <param name="currentSlotCount">Number of enchantments already applied.</param>
    /// <param name="enchantingSkillRank">Character's Enchanting skill rank (0–100).</param>
    /// <returns>Success rate as a percentage (0–100).</returns>
    public double CalculateSuccessRate(int currentSlotCount, int enchantingSkillRank)
    {
        var baseRate = currentSlotCount switch
        {
            0 => 100.0,
            1 => 75.0,
            2 => 50.0,
            _ => 25.0
        };

        var skillBonus = enchantingSkillRank * 0.3;
        return Math.Min(baseRate + skillBonus, 100.0);
    }

    /// <summary>
    /// Returns the maximum number of enchantment slots allowed for a given item rarity.
    /// Common=1, Uncommon=1, Rare=2, Epic=3, Legendary=3.
    /// </summary>
    /// <param name="rarity">The item rarity.</param>
    /// <returns>Maximum player enchantment slots.</returns>
    public int GetMaxSlotsForRarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Uncommon => 1,
        ItemRarity.Rare => 2,
        ItemRarity.Epic => 3,
        ItemRarity.Legendary => 3,
        _ => 1
    };

    /// <summary>
    /// Returns the minimum Enchanting skill required to add a given slot number.
    /// Slot 1: skill 0, Slot 2: skill 25, Slot 3: skill 50.
    /// </summary>
    /// <param name="slotNumber">The 1-based slot number to unlock (1–3).</param>
    /// <returns>Required Enchanting skill rank.</returns>
    public int GetRequiredSkillForSlot(int slotNumber) => slotNumber switch
    {
        1 => 0,
        2 => 25,
        3 => 50,
        _ => 75
    };

    /// <summary>
    /// Returns a human-readable description of the success rate for the next enchanting attempt.
    /// </summary>
    /// <param name="item">The item being enchanted.</param>
    /// <param name="enchantingSkillRank">Character's Enchanting skill rank.</param>
    /// <returns>Display-friendly description of the success rates per slot.</returns>
    public EnchantingRateSummary GetRateSummary(Item item, int enchantingSkillRank)
    {
        var maxSlots = GetMaxSlotsForRarity(item.Rarity);
        var rates = new List<SlotRateInfo>();

        for (var slotIndex = 0; slotIndex < maxSlots; slotIndex++)
        {
            var rate = CalculateSuccessRate(slotIndex, enchantingSkillRank);
            rates.Add(new SlotRateInfo
            {
                SlotNumber = slotIndex + 1,
                SuccessRate = rate,
                IsFilled = slotIndex < item.PlayerEnchantments.Count,
                IsAvailable = slotIndex < item.MaxPlayerEnchantments
            });
        }

        _logger.LogDebug("Enchanting rate summary computed for {ItemName} with skill {Skill}", item.Name, enchantingSkillRank);

        return new EnchantingRateSummary
        {
            MaxSlots = maxSlots,
            CurrentSlots = item.PlayerEnchantments.Count,
            UnlockedSlots = item.MaxPlayerEnchantments,
            Rates = rates
        };
    }
}

/// <summary>
/// Summary of enchanting success rates for an item.
/// </summary>
public class EnchantingRateSummary
{
    /// <summary>Gets or sets the maximum possible slots for this item's rarity.</summary>
    public int MaxSlots { get; set; }

    /// <summary>Gets or sets the number of slots already unlocked via socket crystals.</summary>
    public int UnlockedSlots { get; set; }

    /// <summary>Gets or sets the number of slots currently filled with enchantments.</summary>
    public int CurrentSlots { get; set; }

    /// <summary>Gets or sets the per-slot success rate breakdown.</summary>
    public List<SlotRateInfo> Rates { get; set; } = new();
}

/// <summary>
/// Success rate information for a single enchantment slot.
/// </summary>
public class SlotRateInfo
{
    /// <summary>Gets or sets the 1-based slot number.</summary>
    public int SlotNumber { get; set; }

    /// <summary>Gets or sets the success rate percentage for applying to this slot.</summary>
    public double SuccessRate { get; set; }

    /// <summary>Gets or sets a value indicating whether this slot is already filled.</summary>
    public bool IsFilled { get; set; }

    /// <summary>Gets or sets a value indicating whether this slot has been unlocked.</summary>
    public bool IsAvailable { get; set; }
}
