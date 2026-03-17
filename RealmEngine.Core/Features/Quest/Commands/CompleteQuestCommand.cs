using MediatR;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Quests.Commands;

/// <summary>
/// Command to complete a quest.
/// </summary>
public record CompleteQuestCommand(string QuestId) : IRequest<CompleteQuestResult>;

/// <summary>
/// Result of completing a quest.
/// </summary>
public record CompleteQuestResult(bool Success, string Message, QuestRewards? Rewards = null);

/// <summary>
/// Quest rewards data.
/// </summary>
public record QuestRewards(int Xp, int Gold, int ApocalypseBonus, List<string> Items);

/// <summary>
/// Handles quest completion.
/// </summary>
public class CompleteQuestHandler : IRequestHandler<CompleteQuestCommand, CompleteQuestResult>
{
    private readonly Services.QuestService _questService;
    private readonly Services.QuestRewardService _rewardService;
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteQuestHandler"/> class.
    /// </summary>
    /// <param name="questService">The quest service.</param>
    /// <param name="rewardService">The quest reward service.</param>
    /// <param name="saveGameService">The save game service.</param>
    public CompleteQuestHandler(
        Services.QuestService questService, 
        Services.QuestRewardService rewardService,
        ISaveGameService saveGameService)
    {
        _questService = questService;
        _rewardService = rewardService;
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the complete quest command.
    /// </summary>
    /// <param name="request">The complete quest command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete quest result.</returns>
    public async Task<CompleteQuestResult> Handle(CompleteQuestCommand request, CancellationToken cancellationToken)
    {
        var result = await _questService.CompleteQuestAsync(request.QuestId);

        if (result.Success)
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame != null)
            {
                // Distribute rewards to the player
                await _rewardService.DistributeRewardsAsync(result.Quest!, saveGame.Character, saveGame);
            }

            var rewards = new QuestRewards(
                result.Quest!.XpReward,
                result.Quest.GoldReward,
                result.Quest.ApocalypseBonusMinutes,
                result.Quest.ItemRewardIds
            );

            return new CompleteQuestResult(true, $"Quest completed: {result.Quest.Title}", rewards);
        }

        return new CompleteQuestResult(false, result.ErrorMessage);
    }
}
