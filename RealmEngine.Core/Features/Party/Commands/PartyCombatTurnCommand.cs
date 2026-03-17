using MediatR;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Party.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Party.Commands;

/// <summary>
/// Command for party-based combat (player + allies vs enemy).
/// </summary>
public record PartyCombatTurnCommand : IRequest<PartyCombatTurnResult>
{
    /// <summary>
    /// Player action (Attack, Defend, UseAbility, etc.).
    /// </summary>
    public string PlayerAction { get; init; } = "Attack";

    /// <summary>
    /// Enemy being fought.
    /// </summary>
    public Enemy Enemy { get; init; } = null!;
}

/// <summary>
/// Result of party combat turn.
/// </summary>
public record PartyCombatTurnResult
{
    /// <summary>
    /// Whether combat continues.
    /// </summary>
    public bool CombatContinues { get; init; }

    /// <summary>
    /// Player action result.
    /// </summary>
    public string PlayerMessage { get; init; } = string.Empty;

    /// <summary>
    /// Player damage dealt.
    /// </summary>
    public int PlayerDamage { get; init; }

    /// <summary>
    /// Party member actions.
    /// </summary>
    public List<PartyMemberActionResult> AllyActions { get; init; } = new();

    /// <summary>
    /// Enemy action result.
    /// </summary>
    public string EnemyMessage { get; init; } = string.Empty;

    /// <summary>
    /// Enemy damage dealt.
    /// </summary>
    public int EnemyDamage { get; init; }

    /// <summary>
    /// Enemy target (player or ally name).
    /// </summary>
    public string? EnemyTarget { get; init; }

    /// <summary>
    /// Whether enemy was defeated.
    /// </summary>
    public bool EnemyDefeated { get; init; }

    /// <summary>
    /// Whether party was defeated.
    /// </summary>
    public bool PartyDefeated { get; init; }

    /// <summary>
    /// XP gained (if victory).
    /// </summary>
    public int XPGained { get; init; }

    /// <summary>
    /// Gold gained (if victory).
    /// </summary>
    public int GoldGained { get; init; }

    /// <summary>
    /// Full combat log.
    /// </summary>
    public List<string> CombatLog { get; init; } = new();
}

/// <summary>
/// Party member action result.
/// </summary>
public record PartyMemberActionResult
{
    /// <summary>
    /// Member name.
    /// </summary>
    public string MemberName { get; init; } = string.Empty;

    /// <summary>
    /// Action message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Damage dealt (if attack).
    /// </summary>
    public int Damage { get; init; }

    /// <summary>
    /// Heal amount (if heal).
    /// </summary>
    public int HealAmount { get; init; }
}

/// <summary>
/// Handler for party combat turns.
/// </summary>
public class PartyCombatTurnHandler : IRequestHandler<PartyCombatTurnCommand, PartyCombatTurnResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly CombatService _combatService;
    private readonly PartyService _partyService;
    private readonly PartyAIService _partyAI;
    private readonly ILogger<PartyCombatTurnHandler> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyCombatTurnHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="combatService">The combat service.</param>
    /// <param name="partyService">The party service.</param>
    /// <param name="partyAI">The party AI service.</param>
    /// <param name="logger">The logger.</param>
    public PartyCombatTurnHandler(ISaveGameService saveGameService, CombatService combatService, PartyService partyService, PartyAIService partyAI, ILogger<PartyCombatTurnHandler> logger)
    {
        _saveGameService = saveGameService;
        _combatService = combatService;
        _partyService = partyService;
        _partyAI = partyAI;
        _logger = logger;
    }

    /// <summary>
    /// Handles the party combat turn command.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<PartyCombatTurnResult> Handle(PartyCombatTurnCommand request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
        {
            return new PartyCombatTurnResult { CombatContinues = false };
        }

        var player = saveGame.Character;
        var party = saveGame.Party;
        var enemy = request.Enemy;
        var combatLog = new List<string>();

        // --- PLAYER TURN ---
        var playerResult = await _combatService.ExecutePlayerAttack(player, enemy);
        enemy.Health = Math.Max(0, enemy.Health - playerResult.Damage);
        
        combatLog.Add(playerResult.Message ?? $"Player dealt {playerResult.Damage} damage.");

        // Check if enemy defeated
        if (enemy.Health <= 0)
        {
            var difficulty = _saveGameService.GetDifficultySettings();
            int xpGained = (int)(enemy.XP * difficulty.GoldXPMultiplier);
            int goldGained = (int)(enemy.GoldReward * difficulty.GoldXPMultiplier);

            // Distribute XP/gold to party
            if (party != null)
            {
                _partyService.DistributeExperience(party, xpGained);
                _partyService.DistributeGold(party, goldGained);
            }
            else
            {
                player.GainExperience(xpGained);
                player.Gold += goldGained;
            }

            combatLog.Add($"{enemy.Name} defeated! Gained {xpGained} XP and {goldGained} gold!");

            _saveGameService.SaveGame(saveGame);

            return new PartyCombatTurnResult
            {
                CombatContinues = false,
                PlayerMessage = playerResult.Message ?? string.Empty,
                PlayerDamage = playerResult.Damage,
                EnemyDefeated = true,
                XPGained = xpGained,
                GoldGained = goldGained,
                CombatLog = combatLog
            };
        }

        // --- PARTY MEMBER TURNS ---
        var allyActions = new List<PartyMemberActionResult>();

        if (party != null)
        {
            var aliveAllies = party.AliveMembers;

            foreach (var ally in aliveAllies)
            {
                // Determine AI action
                var action = _partyAI.DetermineAction(ally, enemy, player, aliveAllies);

                combatLog.Add(action.Message);

                if (action.Type == ActionType.Attack || action.Type == ActionType.AttackFallback)
                {
                    // Apply damage to enemy
                    enemy.Health = Math.Max(0, enemy.Health - action.Damage);

                    allyActions.Add(new PartyMemberActionResult
                    {
                        MemberName = ally.Name,
                        Message = action.Message,
                        Damage = action.Damage
                    });

                    // Check if enemy defeated by ally
                    if (enemy.Health <= 0)
                    {
                        var difficulty = _saveGameService.GetDifficultySettings();
                        int xpGained = (int)(enemy.XP * difficulty.GoldXPMultiplier);
                        int goldGained = (int)(enemy.GoldReward * difficulty.GoldXPMultiplier);

                        _partyService.DistributeExperience(party, xpGained);
                        _partyService.DistributeGold(party, goldGained);

                        combatLog.Add($"{enemy.Name} defeated! Gained {xpGained} XP and {goldGained} gold!");

                        _saveGameService.SaveGame(saveGame);

                        return new PartyCombatTurnResult
                        {
                            CombatContinues = false,
                            PlayerMessage = playerResult.Message ?? string.Empty,
                            PlayerDamage = playerResult.Damage,
                            AllyActions = allyActions,
                            EnemyDefeated = true,
                            XPGained = xpGained,
                            GoldGained = goldGained,
                            CombatLog = combatLog
                        };
                    }
                }
                else if (action.Type == ActionType.Heal)
                {
                    // Apply heal
                    _partyAI.ApplyHeal(action, player, aliveAllies);

                    allyActions.Add(new PartyMemberActionResult
                    {
                        MemberName = ally.Name,
                        Message = action.Message,
                        HealAmount = action.HealAmount
                    });
                }
            }
        }

        // --- ENEMY TURN ---
        string enemyTarget;
        int enemyDamage = 0;
        string enemyMessage = string.Empty;

        // Enemy chooses target (player or random ally)
        if (party != null && party.AliveMembers.Count > 0)
        {
            // 60% chance to attack player, 40% to attack random ally
            if (_random.NextDouble() < 0.6)
            {
                enemyTarget = player.Name;
                var enemyResult = await _combatService.ExecuteEnemyAttack(enemy, player);
                player.Health = Math.Max(0, player.Health - enemyResult.Damage);
                enemyDamage = enemyResult.Damage;
                enemyMessage = enemyResult.Message ?? $"{enemy.Name} dealt {enemyDamage} damage to {player.Name}.";
            }
            else
            {
                var targetAlly = party.AliveMembers[_random.Next(party.AliveMembers.Count)];
                enemyTarget = targetAlly.Name;
                
                // Calculate enemy damage to ally (simplified)
                int baseDamage = enemy.BasePhysicalDamage + enemy.Strength + (enemy.Level * 2);
                enemyDamage = Math.Max(1, baseDamage - targetAlly.GetDefense());
                targetAlly.TakeDamage(enemyDamage);
                
                enemyMessage = $"{enemy.Name} attacks {targetAlly.Name} for {enemyDamage} damage!";
            }
        }
        else
        {
            // No party, attack player
            enemyTarget = player.Name;
            var enemyResult = await _combatService.ExecuteEnemyAttack(enemy, player);
            player.Health = Math.Max(0, player.Health - enemyResult.Damage);
            enemyDamage = enemyResult.Damage;
            enemyMessage = enemyResult.Message ?? $"{enemy.Name} dealt {enemyDamage} damage to {player.Name}.";
        }

        combatLog.Add(enemyMessage);

        // Check if party defeated
        bool partyDefeated = player.Health <= 0 && (party == null || party.AliveMembers.Count == 0);

        if (partyDefeated)
        {
            combatLog.Add("Your party has been defeated!");
        }

        _saveGameService.SaveGame(saveGame);

        return new PartyCombatTurnResult
        {
            CombatContinues = !partyDefeated,
            PlayerMessage = playerResult.Message ?? string.Empty,
            PlayerDamage = playerResult.Damage,
            AllyActions = allyActions,
            EnemyMessage = enemyMessage,
            EnemyDamage = enemyDamage,
            EnemyTarget = enemyTarget,
            PartyDefeated = partyDefeated,
            CombatLog = combatLog
        };
    }
}
