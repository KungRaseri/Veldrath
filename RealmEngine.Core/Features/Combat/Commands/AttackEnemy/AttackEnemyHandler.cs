using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using MediatR;
using Serilog;
using RealmEngine.Core.Features.Quests.Commands;

namespace RealmEngine.Core.Features.Combat.Commands.AttackEnemy;

/// <summary>
/// Handles the AttackEnemy command.
/// </summary>
public class AttackEnemyHandler : IRequestHandler<AttackEnemyCommand, AttackEnemyResult>
{
    private readonly CombatService _combatService;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttackEnemyHandler"/> class.
    /// </summary>
    /// <param name="combatService">The combat service.</param>
    /// <param name="mediator">The mediator for publishing events.</param>
    /// <param name="saveGameService">The save game service.</param>
    public AttackEnemyHandler(CombatService combatService, IMediator mediator, SaveGameService saveGameService)
    {
        _combatService = combatService;
        _mediator = mediator;
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the attack enemy command and returns the result of the attack.
    /// </summary>
    /// <param name="request">The attack enemy command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the attack result.</returns>
    public async Task<AttackEnemyResult> Handle(AttackEnemyCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var enemy = request.Enemy;
        var combatLog = request.CombatLog;

        // Calculate damage using combat service (now async)
        var combatResult = await _combatService.ExecutePlayerAttack(player, enemy);
        var damage = combatResult.Damage;
        var isCritical = combatResult.IsCritical;

        // Apply damage to enemy (clamped to 0)
        enemy.Health = Math.Max(0, enemy.Health - damage);
        
        // Log to combat log
        combatLog?.AddEntry($"{player.Name} attacks for {damage} damage" + 
            (isCritical ? " (CRITICAL!)" : ""));

        // Publish attack event
        await _mediator.Publish(new AttackPerformed(player.Name, enemy.Name, damage), cancellationToken);

        // Check if enemy defeated
        var isDefeated = enemy.Health <= 0;
        int xpGained = 0;
        int goldGained = 0;

        if (isDefeated)
        {
            // Award gold and experience with difficulty multiplier
            var difficulty = _saveGameService.GetDifficultySettings();
            xpGained = (int)(enemy.XP * difficulty.GoldXPMultiplier);
            goldGained = (int)(enemy.GoldReward * difficulty.GoldXPMultiplier);
            
            player.Experience += xpGained;
            player.Gold += goldGained;

            combatLog?.AddEntry($"{enemy.Name} defeated! Gained {xpGained} XP and {goldGained} gold!");

            await _mediator.Publish(new EnemyDefeated(player.Name, enemy.Name), cancellationToken);
            await _mediator.Publish(new GoldGained(player.Name, goldGained), cancellationToken);

            // Update quest progress for kill objectives
            await UpdateQuestProgressForEnemyKill(enemy, cancellationToken);

            // Check for auto-completable quests after enemy defeat
            await CheckAndCompleteReadyQuests(cancellationToken);
        }

        Log.Information("Player {PlayerName} attacked {EnemyName} for {Damage} damage (critical: {IsCritical})",
            player.Name, enemy.Name, damage, isCritical);

        return new AttackEnemyResult
        {
            Damage = damage,
            IsCritical = isCritical,
            IsEnemyDefeated = isDefeated,
            ExperienceGained = xpGained,
            GoldGained = goldGained
        };
    }

    /// <summary>
    /// Updates quest progress when an enemy is defeated.
    /// Checks all active quests and updates matching objectives.
    /// </summary>
    private async Task UpdateQuestProgressForEnemyKill(Enemy enemy, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null) return;

        // Update total enemies defeated counter
        saveGame.TotalEnemiesDefeated++;

        // Update enemies defeated by type (convert enum to string)
        var enemyType = enemy.Type.ToString().ToLowerInvariant();
        if (!saveGame.EnemiesDefeatedByType.ContainsKey(enemyType))
        {
            saveGame.EnemiesDefeatedByType[enemyType] = 0;
        }
        saveGame.EnemiesDefeatedByType[enemyType]++;

        // Check all active quests for matching objectives
        foreach (var quest in saveGame.ActiveQuests)
        {
            foreach (var objective in quest.Objectives)
            {
                var objectiveId = objective.Key;

                // Match objective ID patterns:
                // Pattern 1: "defeat_<enemy_name>" (e.g., "defeat_shrine_guardian")
                // Pattern 2: "defeat_<enemy_type>" (e.g., "defeat_abyssal_demons")
                // Pattern 3: "kill_<enemy_type>" (e.g., "kill_goblins")

                var enemyNameMatch = $"defeat_{enemy.Name.ToLowerInvariant().Replace(" ", "_")}";
                var enemyTypeMatch = $"defeat_{enemyType}";
                var killTypeMatch = $"kill_{enemyType}";

                if (objectiveId == enemyNameMatch || 
                    objectiveId == enemyTypeMatch || 
                    objectiveId == killTypeMatch ||
                    objectiveId.EndsWith($"_{enemyType}") ||
                    objectiveId.Contains(enemyType))
                {
                    // Update quest progress
                    var progressResult = await _mediator.Send(
                        new UpdateQuestProgressCommand(quest.Id, objectiveId, 1), 
                        cancellationToken);

                    if (progressResult.ObjectiveCompleted)
                    {
                        Log.Information("Quest objective completed: {QuestId}/{ObjectiveId}", quest.Id, objectiveId);
                    }

                    if (progressResult.QuestCompleted)
                    {
                        Log.Information("Quest ready for completion: {QuestId}", quest.Id);
                    }
                }
            }
        }

        // Save updated counters
        _saveGameService.SaveGame(saveGame);
    }

    /// <summary>
    /// Checks all active quests and auto-completes those with all objectives met.
    /// </summary>
    private async Task CheckAndCompleteReadyQuests(CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null) return;

        // Find quests that have all objectives completed
        var readyQuests = saveGame.ActiveQuests
            .Where(q => q.Objectives.All(kvp =>
                q.ObjectiveProgress.ContainsKey(kvp.Key) && 
                q.ObjectiveProgress[kvp.Key] >= kvp.Value))
            .ToList();

        // Auto-complete each ready quest
        foreach (var quest in readyQuests)
        {
            var result = await _mediator.Send(
                new CompleteQuestCommand(quest.Id), 
                cancellationToken);

            if (result.Success)
            {
                Log.Information("Quest auto-completed: {QuestTitle} - Rewards: {XP} XP, {Gold} gold",
                    quest.Title, result.Rewards?.Xp ?? 0, result.Rewards?.Gold ?? 0);
            }
        }
    }
}