using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Handler for applying enchantment scrolls to items.
/// Implements skill-based success rates with slot difficulty progression.
/// </summary>
public class ApplyEnchantmentHandler : IRequestHandler<ApplyEnchantmentCommand, ApplyEnchantmentResult>
{
    /// <summary>
    /// Handles the enchantment application request.
    /// </summary>
    public Task<ApplyEnchantmentResult> Handle(ApplyEnchantmentCommand request, CancellationToken cancellationToken)
    {
        var character = request.Character;
        var item = request.Item;
        var scroll = request.EnchantmentScroll;

        // Validate item has available slots
        if (!item.CanAddPlayerEnchantment())
        {
            return Task.FromResult(new ApplyEnchantmentResult
            {
                Success = false,
                Message = $"{item.Name} has no available enchantment slots ({item.PlayerEnchantments.Count}/{item.MaxPlayerEnchantments}).",
                ScrollConsumed = false,
                SuccessRate = 0
            });
        }

        // Extract enchantment from scroll
        // For now, assume scroll has an Enchantment in its Enchantments list
        var enchantment = scroll.Enchantments.FirstOrDefault();
        if (enchantment == null)
        {
            return Task.FromResult(new ApplyEnchantmentResult
            {
                Success = false,
                Message = "The scroll does not contain a valid enchantment.",
                ScrollConsumed = false,
                SuccessRate = 0
            });
        }

        // Calculate success rate based on slot count
        var currentSlotCount = item.PlayerEnchantments.Count;
        var successRate = CalculateSuccessRate(currentSlotCount, character);

        // Roll for success
        var random = new Random();
        var roll = random.NextDouble() * 100.0; // 0-100
        var succeeded = roll <= successRate;

        if (succeeded)
        {
            // Apply enchantment
            var clonedEnchantment = new Enchantment
            {
                Id = Guid.NewGuid().ToString(),
                Name = enchantment.Name,
                Description = enchantment.Description,
                Rarity = enchantment.Rarity,
                Traits = new Dictionary<string, TraitValue>(enchantment.Traits),
                Position = enchantment.Position,
                RarityWeight = enchantment.RarityWeight,
                SpecialEffect = enchantment.SpecialEffect,
                Level = enchantment.Level
            };

            item.PlayerEnchantments.Add(clonedEnchantment);

            return Task.FromResult(new ApplyEnchantmentResult
            {
                Success = true,
                Message = $"Successfully applied {enchantment.Name} to {item.Name}!",
                AppliedEnchantment = clonedEnchantment,
                SuccessRate = successRate,
                ScrollConsumed = true
            });
        }
        else
        {
            return Task.FromResult(new ApplyEnchantmentResult
            {
                Success = false,
                Message = $"Failed to apply {enchantment.Name}. The scroll crumbles to dust. (Success rate was {successRate:F1}%)",
                SuccessRate = successRate,
                ScrollConsumed = true
            });
        }
    }

    /// <summary>
    /// Calculate success rate based on current enchantment count and Enchanting skill.
    /// Slot 1: 100% (guaranteed)
    /// Slot 2: 75% + (skill * 0.3%), max 100%
    /// Slot 3: 50% + (skill * 0.3%), max 100%
    /// </summary>
    private double CalculateSuccessRate(int currentSlotCount, Character character)
    {
        // Get Enchanting skill level
        var enchantingSkill = character.Skills.TryGetValue("Enchanting", out var skill) ? skill.CurrentRank : 0;

        // Base rates by slot
        var baseRate = currentSlotCount switch
        {
            0 => 100.0, // First slot always succeeds
            1 => 75.0,  // Second slot
            2 => 50.0,  // Third slot
            _ => 25.0   // Fourth slot (if somehow allowed)
        };

        // Add skill bonus (0.3% per level)
        var skillBonus = enchantingSkill * 0.3;
        var successRate = baseRate + skillBonus;

        // Cap at 100%
        return Math.Min(successRate, 100.0);
    }
}
