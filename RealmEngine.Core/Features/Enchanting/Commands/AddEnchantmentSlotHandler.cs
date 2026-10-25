using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Handler for adding enchantment slots to items using socket crystals.
/// Validates skill requirements and rarity-based maximum slots.
/// </summary>
public class AddEnchantmentSlotHandler : IRequestHandler<AddEnchantmentSlotCommand, AddEnchantmentSlotResult>
{
    /// <summary>
    /// Handles the add enchantment slot request.
    /// </summary>
    public Task<AddEnchantmentSlotResult> Handle(AddEnchantmentSlotCommand request, CancellationToken cancellationToken)
    {
        var character = request.Character;
        var item = request.Item;
        var crystal = request.SocketCrystal;

        // Get Enchanting skill level
        var enchantingSkill = character.Skills.TryGetValue("Enchanting", out var skill) ? skill.CurrentRank : 0;

        // Calculate max slots for this rarity
        var maxSlotsForRarity = GetMaxSlotsForRarity(item.Rarity);

        // Check if already at hard cap (3 slots) first
        if (item.MaxPlayerEnchantments >= 3)
        {
            return Task.FromResult(new AddEnchantmentSlotResult
            {
                Success = false,
                Message = $"{item.Name} already has the maximum possible enchantment slots (3).",
                NewMaxSlots = item.MaxPlayerEnchantments,
                CrystalConsumed = false
            });
        }

        // Check if already at max slots for rarity
        if (item.MaxPlayerEnchantments >= maxSlotsForRarity)
        {
            return Task.FromResult(new AddEnchantmentSlotResult
            {
                Success = false,
                Message = $"{item.Name} already has maximum enchantment slots for its rarity ({maxSlotsForRarity}).",
                NewMaxSlots = item.MaxPlayerEnchantments,
                CrystalConsumed = false
            });
        }

        // Calculate required skill for next slot
        var nextSlotNumber = item.MaxPlayerEnchantments + 1;
        var requiredSkill = GetRequiredSkillForSlot(nextSlotNumber);

        // Validate skill requirement
        if (enchantingSkill < requiredSkill)
        {
            return Task.FromResult(new AddEnchantmentSlotResult
            {
                Success = false,
                Message = $"Requires Enchanting skill {requiredSkill} to add slot {nextSlotNumber}. You have {enchantingSkill}.",
                NewMaxSlots = item.MaxPlayerEnchantments,
                CrystalConsumed = false
            });
        }

        // Add the slot
        item.MaxPlayerEnchantments++;

        return Task.FromResult(new AddEnchantmentSlotResult
        {
            Success = true,
            Message = $"Successfully added enchantment slot {nextSlotNumber} to {item.Name}!",
            NewMaxSlots = item.MaxPlayerEnchantments,
            CrystalConsumed = true
        });
    }

    /// <summary>
    /// Get maximum enchantment slots allowed for a given rarity.
    /// Common=1, Rare=2, Legendary=3
    /// </summary>
    private int GetMaxSlotsForRarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Uncommon => 1,
        ItemRarity.Rare => 2,
        ItemRarity.Epic => 2,
        ItemRarity.Legendary => 3,
        _ => 1
    };

    /// <summary>
    /// Get required Enchanting skill level for a given slot number.
    /// Slot 1: 0 (base), Slot 2: 25, Slot 3: 50
    /// </summary>
    private int GetRequiredSkillForSlot(int slotNumber) => slotNumber switch
    {
        1 => 0,   // Base slot (always available)
        2 => 25,  // Second slot
        3 => 50,  // Third slot
        _ => 999  // Shouldn't happen
    };
}
