using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Upgrading.Commands;

/// <summary>
/// Handler for upgrading items using essences.
/// Implements hybrid safety model: +1 to +5 safe, +6 to +10 risky.
/// </summary>
public class UpgradeItemHandler : IRequestHandler<UpgradeItemCommand, UpgradeItemResult>
{
    /// <summary>
    /// Handles the upgrade item request.
    /// </summary>
    public Task<UpgradeItemResult> Handle(UpgradeItemCommand request, CancellationToken cancellationToken)
    {
        var item = request.Item;
        var essences = request.Essences;
        var oldLevel = item.UpgradeLevel;

        // Validate item can be upgraded
        var maxLevel = item.GetMaxUpgradeLevel();
        if (item.UpgradeLevel >= maxLevel)
        {
            return Task.FromResult(new UpgradeItemResult
            {
                Success = false,
                Message = $"{item.Name} is already at maximum upgrade level (+{maxLevel}) for its rarity.",
                NewUpgradeLevel = item.UpgradeLevel,
                OldUpgradeLevel = oldLevel,
                SuccessRate = 0,
                EssencesConsumed = false,
                StatMultiplier = CalculateStatMultiplier(item.UpgradeLevel)
            });
        }

        var targetLevel = item.UpgradeLevel + 1;

        // Validate essence type matches item type
        var requiredEssenceType = GetRequiredEssenceType(item.Type);
        if (!essences.All(e => e.Name.Contains(requiredEssenceType)))
        {
            return Task.FromResult(new UpgradeItemResult
            {
                Success = false,
                Message = $"Cannot upgrade {item.Type} with these essences. Requires {requiredEssenceType} Essence.",
                NewUpgradeLevel = item.UpgradeLevel,
                OldUpgradeLevel = oldLevel,
                SuccessRate = 0,
                EssencesConsumed = false,
                StatMultiplier = CalculateStatMultiplier(item.UpgradeLevel)
            });
        }

        // Validate essence count matches requirement
        var requiredEssences = GetRequiredEssenceCount(targetLevel);
        if (essences.Count < requiredEssences.Count)
        {
            return Task.FromResult(new UpgradeItemResult
            {
                Success = false,
                Message = $"Insufficient essences for +{targetLevel}. Required: {string.Join(", ", requiredEssences)}.",
                NewUpgradeLevel = item.UpgradeLevel,
                OldUpgradeLevel = oldLevel,
                SuccessRate = 0,
                EssencesConsumed = false,
                StatMultiplier = CalculateStatMultiplier(item.UpgradeLevel)
            });
        }

        // Calculate success rate
        var successRate = CalculateSuccessRate(targetLevel);

        // Attempt upgrade
        var random = new Random();
        var roll = random.NextDouble() * 100.0;
        var succeeded = roll <= successRate;

        if (succeeded)
        {
            // Success: increment level
            item.UpgradeLevel = targetLevel;
            var newMultiplier = CalculateStatMultiplier(targetLevel);

            return Task.FromResult(new UpgradeItemResult
            {
                Success = true,
                Message = $"Successfully upgraded {item.Name} to +{targetLevel}! Stats increased by {(newMultiplier - 1) * 100:F0}%.",
                NewUpgradeLevel = targetLevel,
                OldUpgradeLevel = oldLevel,
                SuccessRate = successRate,
                EssencesConsumed = true,
                StatMultiplier = newMultiplier
            });
        }
        else
        {
            // Failure: drop to previous level (or stay at 0)
            var newLevel = Math.Max(0, item.UpgradeLevel - 1);
            item.UpgradeLevel = newLevel;

            return Task.FromResult(new UpgradeItemResult
            {
                Success = false,
                Message = $"Upgrade failed! {item.Name} dropped from +{oldLevel} to +{newLevel}. (Success rate was {successRate:F0}%)",
                NewUpgradeLevel = newLevel,
                OldUpgradeLevel = oldLevel,
                SuccessRate = successRate,
                EssencesConsumed = true,
                StatMultiplier = CalculateStatMultiplier(newLevel)
            });
        }
    }

    /// <summary>
    /// Calculate success rate for upgrade based on target level.
    /// +1 to +5: 100% (safe)
    /// +6: 95%, +7: 85%, +8: 75%, +9: 60%, +10: 50%
    /// </summary>
    private double CalculateSuccessRate(int targetLevel) => targetLevel switch
    {
        <= 5 => 100.0,  // Safe zone
        6 => 95.0,
        7 => 85.0,
        8 => 75.0,
        9 => 60.0,
        10 => 50.0,
        _ => 25.0  // Shouldn't happen
    };

    /// <summary>
    /// Calculate stat multiplier for a given upgrade level.
    /// Formula: 1 + (level * 0.10) + (level² * 0.01)
    /// Examples: +1=1.11x, +5=1.75x, +8=2.12x, +10=2.5x
    /// </summary>
    private double CalculateStatMultiplier(int level)
    {
        return 1.0 + (level * 0.10) + (level * level * 0.01);
    }

    /// <summary>
    /// Get required essence type based on item type.
    /// Weapons use Weapon Essence, Armor pieces use Armor Essence, Jewelry uses Accessory Essence.
    /// </summary>
    private string GetRequiredEssenceType(ItemType itemType) => itemType switch
    {
        ItemType.Weapon => "Weapon",
        ItemType.Shield => "Armor",
        ItemType.OffHand => "Weapon",
        ItemType.Helmet => "Armor",
        ItemType.Shoulders => "Armor",
        ItemType.Chest => "Armor",
        ItemType.Bracers => "Armor",
        ItemType.Gloves => "Armor",
        ItemType.Belt => "Armor",
        ItemType.Legs => "Armor",
        ItemType.Boots => "Armor",
        ItemType.Necklace => "Accessory",
        ItemType.Ring => "Accessory",
        _ => "Unknown"
    };

    /// <summary>
    /// Get required essence count and tiers for each upgrade level.
    /// Returns list of essence tier names needed.
    /// </summary>
    private List<string> GetRequiredEssenceCount(int targetLevel) => targetLevel switch
    {
        1 => new List<string> { "Minor" },
        2 => new List<string> { "Minor", "Minor" },
        3 => new List<string> { "Minor", "Minor", "Minor" },
        4 => new List<string> { "Greater", "Minor", "Minor" },
        5 => new List<string> { "Greater", "Greater" },
        6 => new List<string> { "Greater", "Greater", "Greater" },
        7 => new List<string> { "Superior", "Greater", "Greater", "Greater" },
        8 => new List<string> { "Superior", "Superior" },
        9 => new List<string> { "Superior", "Superior", "Superior" },
        10 => new List<string> { "Perfect", "Superior", "Superior", "Superior" },
        _ => new List<string>()
    };
}
