using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Queries;

/// <summary>
/// Handler for <see cref="GetEnchantmentsQuery"/>.
/// Returns slot information and success rates for the item's enchantments.
/// </summary>
public class GetEnchantmentsHandler : IRequestHandler<GetEnchantmentsQuery, GetEnchantmentsResult>
{
    private readonly EnchantingService _enchantingService;

    /// <summary>Initializes a new instance of <see cref="GetEnchantmentsHandler"/>.</summary>
    public GetEnchantmentsHandler(EnchantingService enchantingService)
    {
        _enchantingService = enchantingService;
    }

    /// <summary>Handles the get enchantments request.</summary>
    public Task<GetEnchantmentsResult> Handle(GetEnchantmentsQuery request, CancellationToken cancellationToken)
    {
        var item = request.Item;

        var playerSlots = item.PlayerEnchantments
            .Select((e, i) => new EnchantmentSlotInfo
            {
                Index = i,
                Name = e.Name,
                Description = e.Description,
                Rarity = e.Rarity,
                Traits = e.Traits
            })
            .ToList();

        var inherentSlots = item.Enchantments
            .Select((e, i) => new EnchantmentSlotInfo
            {
                Index = i,
                Name = e.Name,
                Description = e.Description,
                Rarity = e.Rarity,
                Traits = e.Traits
            })
            .ToList();

        var maxPossibleSlots = _enchantingService.GetMaxSlotsForRarity(item.Rarity);
        var rateSummary = _enchantingService.GetRateSummary(item, request.EnchantingSkillRank);

        return Task.FromResult(new GetEnchantmentsResult
        {
            Success = true,
            Message = $"{item.Name} has {playerSlots.Count}/{item.MaxPlayerEnchantments} enchantment slots filled.",
            ItemName = item.Name,
            Rarity = item.Rarity,
            PlayerEnchantments = playerSlots,
            InherentEnchantments = inherentSlots,
            UnlockedSlots = item.MaxPlayerEnchantments,
            MaxPossibleSlots = maxPossibleSlots,
            RateSummary = rateSummary,
            CanEnchant = item.CanAddPlayerEnchantment()
        });
    }
}
