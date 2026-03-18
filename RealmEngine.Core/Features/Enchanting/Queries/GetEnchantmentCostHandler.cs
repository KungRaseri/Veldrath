using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Queries;

/// <summary>
/// Handler for <see cref="GetEnchantmentCostQuery"/>.
/// Returns feasibility, success rate, and consumable requirements for enchanting operations.
/// </summary>
public class GetEnchantmentCostHandler : IRequestHandler<GetEnchantmentCostQuery, GetEnchantmentCostResult>
{
    private readonly EnchantingService _enchantingService;

    /// <summary>Initializes a new instance of <see cref="GetEnchantmentCostHandler"/>.</summary>
    public GetEnchantmentCostHandler(EnchantingService enchantingService)
    {
        _enchantingService = enchantingService;
    }

    /// <summary>Handles the enchantment cost query.</summary>
    public Task<GetEnchantmentCostResult> Handle(GetEnchantmentCostQuery request, CancellationToken cancellationToken)
    {
        var item = request.Item;
        var skill = request.EnchantingSkillRank;

        return request.OperationType switch
        {
            EnchantmentOperationType.ApplyEnchantment => Task.FromResult(GetApplyCost(item, skill)),
            EnchantmentOperationType.RemoveEnchantment => Task.FromResult(GetRemoveCost(item)),
            EnchantmentOperationType.UnlockSlot => Task.FromResult(GetUnlockSlotCost(item, skill)),
            _ => Task.FromResult(new GetEnchantmentCostResult
            {
                Success = false,
                Message = "Unknown enchantment operation type.",
                OperationType = request.OperationType
            })
        };
    }

    private GetEnchantmentCostResult GetApplyCost(Item item, int skill)
    {
        if (!item.CanAddPlayerEnchantment())
        {
            return new GetEnchantmentCostResult
            {
                Success = true,
                OperationType = EnchantmentOperationType.ApplyEnchantment,
                IsPossible = false,
                BlockedReason = $"{item.Name} has no available enchantment slots ({item.PlayerEnchantments.Count}/{item.MaxPlayerEnchantments}).",
                SuccessRate = 0,
                RequiredConsumable = "Enchantment Scroll"
            };
        }

        var successRate = _enchantingService.CalculateSuccessRate(item.PlayerEnchantments.Count, skill);

        return new GetEnchantmentCostResult
        {
            Success = true,
            Message = $"Applying to slot {item.PlayerEnchantments.Count + 1} has a {successRate:F1}% success rate.",
            OperationType = EnchantmentOperationType.ApplyEnchantment,
            IsPossible = true,
            SuccessRate = successRate,
            RequiredConsumable = "Enchantment Scroll"
        };
    }

    private GetEnchantmentCostResult GetRemoveCost(Item item)
    {
        if (!item.PlayerEnchantments.Any())
        {
            return new GetEnchantmentCostResult
            {
                Success = true,
                OperationType = EnchantmentOperationType.RemoveEnchantment,
                IsPossible = false,
                BlockedReason = $"{item.Name} has no player-applied enchantments to remove.",
                SuccessRate = 0,
                RequiredConsumable = "Removal Scroll"
            };
        }

        return new GetEnchantmentCostResult
        {
            Success = true,
            Message = $"Removal will destroy the enchantment. The scroll is always consumed.",
            OperationType = EnchantmentOperationType.RemoveEnchantment,
            IsPossible = true,
            SuccessRate = 100.0,
            RequiredConsumable = "Removal Scroll"
        };
    }

    private GetEnchantmentCostResult GetUnlockSlotCost(Item item, int skill)
    {
        var maxPossible = _enchantingService.GetMaxSlotsForRarity(item.Rarity);

        if (item.MaxPlayerEnchantments >= 3 || item.MaxPlayerEnchantments >= maxPossible)
        {
            return new GetEnchantmentCostResult
            {
                Success = true,
                OperationType = EnchantmentOperationType.UnlockSlot,
                IsPossible = false,
                BlockedReason = $"{item.Name} already has the maximum slots for its rarity ({maxPossible}).",
                SuccessRate = 0,
                RequiredConsumable = "Socket Crystal"
            };
        }

        var nextSlot = item.MaxPlayerEnchantments + 1;
        var requiredSkill = _enchantingService.GetRequiredSkillForSlot(nextSlot);
        var hasSkill = skill >= requiredSkill;

        return new GetEnchantmentCostResult
        {
            Success = true,
            Message = hasSkill
                ? $"Slot {nextSlot} can be unlocked."
                : $"Slot {nextSlot} requires Enchanting skill {requiredSkill} (you have {skill}).",
            OperationType = EnchantmentOperationType.UnlockSlot,
            IsPossible = hasSkill,
            BlockedReason = hasSkill ? string.Empty : $"Requires Enchanting skill {requiredSkill}.",
            SuccessRate = 100.0,
            RequiredSkill = requiredSkill,
            RequiredConsumable = "Socket Crystal"
        };
    }
}
