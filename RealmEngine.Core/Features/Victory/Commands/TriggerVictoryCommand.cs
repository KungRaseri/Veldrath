using MediatR;

namespace RealmEngine.Core.Features.Victory.Commands;

/// <summary>
/// Command to trigger victory state.
/// </summary>
public record TriggerVictoryCommand : IRequest<TriggerVictoryResult>;

/// <summary>
/// Result of trigger victory operation.
/// </summary>
/// <param name="Success">Whether the operation was successful.</param>
/// <param name="Statistics">Victory statistics if successful.</param>
public record TriggerVictoryResult(bool Success, VictoryStatistics? Statistics = null);

/// <summary>
/// Victory statistics record.
/// </summary>
/// <param name="PlayerName">Player name.</param>
/// <param name="ClassName">Class name.</param>
/// <param name="FinalLevel">Final level achieved.</param>
/// <param name="Difficulty">Difficulty level.</param>
/// <param name="PlayTimeMinutes">Total play time in minutes.</param>
/// <param name="QuestsCompleted">Number of quests completed.</param>
/// <param name="EnemiesDefeated">Number of enemies defeated.</param>
/// <param name="DeathCount">Number of deaths.</param>
/// <param name="AchievementsUnlocked">Number of achievements unlocked.</param>
/// <param name="TotalGoldEarned">Total gold earned.</param>
public record VictoryStatistics(
    string PlayerName,
    string ClassName,
    int FinalLevel,
    string Difficulty,
    int PlayTimeMinutes,
    int QuestsCompleted,
    int EnemiesDefeated,
    int DeathCount,
    int AchievementsUnlocked,
    int TotalGoldEarned
);

/// <summary>
/// Handles the TriggerVictory command.
/// </summary>
public class TriggerVictoryHandler : IRequestHandler<TriggerVictoryCommand, TriggerVictoryResult>
{
    private readonly Services.VictoryService _victoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerVictoryHandler"/> class.
    /// </summary>
    /// <param name="victoryService">The victory service.</param>
    public TriggerVictoryHandler(Services.VictoryService victoryService)
    {
        _victoryService = victoryService;
    }

    /// <summary>
    /// Handles the TriggerVictoryCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<TriggerVictoryResult> Handle(TriggerVictoryCommand request, CancellationToken cancellationToken)
    {
        var statistics = await _victoryService.CalculateVictoryStatisticsAsync();

        if (statistics != null)
        {
            await _victoryService.MarkGameCompleteAsync();
            return new TriggerVictoryResult(true, statistics);
        }

        return new TriggerVictoryResult(false);
    }
}
