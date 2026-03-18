using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Upgrading.Queries;

/// <summary>
/// Handler for <see cref="GetUpgradePreviewQuery"/>.
/// Builds a full upgrade preview without modifying the item.
/// </summary>
public class GetUpgradePreviewHandler : IRequestHandler<GetUpgradePreviewQuery, GetUpgradePreviewResult>
{
    private readonly UpgradeService _upgradeService;

    /// <summary>Initializes a new instance of <see cref="GetUpgradePreviewHandler"/>.</summary>
    public GetUpgradePreviewHandler(UpgradeService upgradeService)
    {
        _upgradeService = upgradeService;
    }

    /// <summary>Handles the upgrade preview request.</summary>
    public Task<GetUpgradePreviewResult> Handle(GetUpgradePreviewQuery request, CancellationToken cancellationToken)
    {
        var item = request.Item;
        var maxLevel = item.GetMaxUpgradeLevel();
        var canUpgrade = item.UpgradeLevel < maxLevel;

        var nextPreview = _upgradeService.BuildPreview(item);

        var remainingLevels = new List<UpgradeLevelSummary>();
        for (var level = item.UpgradeLevel + 1; level <= maxLevel; level++)
        {
            remainingLevels.Add(new UpgradeLevelSummary
            {
                Level = level,
                SuccessRate = _upgradeService.CalculateSuccessRate(level),
                StatMultiplier = _upgradeService.CalculateStatMultiplier(level),
                RequiredEssences = _upgradeService.GetRequiredEssences(level),
                IsSafeZone = level <= 5
            });
        }

        var message = canUpgrade
            ? $"{item.Name} is +{item.UpgradeLevel}. Next: +{item.UpgradeLevel + 1} ({nextPreview!.SuccessRate:F0}% success)."
            : $"{item.Name} is at maximum upgrade level (+{maxLevel}).";

        return Task.FromResult(new GetUpgradePreviewResult
        {
            Success = true,
            Message = message,
            ItemName = item.Name,
            CanUpgrade = canUpgrade,
            NextLevelPreview = nextPreview,
            RemainingLevels = remainingLevels
        });
    }
}
