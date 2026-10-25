using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Commands;

/// <summary>
/// Handler for removing enchantments from items using removal scrolls.
/// The removed enchantment is destroyed (not recovered).
/// </summary>
public class RemoveEnchantmentHandler : IRequestHandler<RemoveEnchantmentCommand, RemoveEnchantmentResult>
{
    /// <summary>
    /// Handles the enchantment removal request.
    /// </summary>
    public Task<RemoveEnchantmentResult> Handle(RemoveEnchantmentCommand request, CancellationToken cancellationToken)
    {
        var item = request.Item;
        var index = request.EnchantmentIndex;

        // Validate item has player enchantments
        if (!item.PlayerEnchantments.Any())
        {
            return Task.FromResult(new RemoveEnchantmentResult
            {
                Success = false,
                Message = $"{item.Name} has no player-applied enchantments to remove.",
                ScrollConsumed = false
            });
        }

        // Validate index
        if (index < 0 || index >= item.PlayerEnchantments.Count)
        {
            return Task.FromResult(new RemoveEnchantmentResult
            {
                Success = false,
                Message = $"Invalid enchantment index {index}. {item.Name} has {item.PlayerEnchantments.Count} enchantment(s).",
                ScrollConsumed = false
            });
        }

        // Remove the enchantment
        var removedEnchantment = item.PlayerEnchantments[index];
        item.PlayerEnchantments.RemoveAt(index);

        return Task.FromResult(new RemoveEnchantmentResult
        {
            Success = true,
            Message = $"Removed {removedEnchantment.Name} from {item.Name}. The enchantment is destroyed.",
            RemovedEnchantment = removedEnchantment,
            ScrollConsumed = true
        });
    }
}
