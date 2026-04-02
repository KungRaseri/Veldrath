using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.FleeFromCombat;

/// <summary>
/// Handler for FleeFromCombatCommand. Resolves a flee attempt based on the player's Dexterity.
/// </summary>
public class FleeFromCombatHandler : IRequestHandler<FleeFromCombatCommand, FleeFromCombatResult>
{
    private readonly ILogger<FleeFromCombatHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleeFromCombatHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FleeFromCombatHandler(ILogger<FleeFromCombatHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the flee from combat command.
    /// </summary>
    /// <param name="request">The flee command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the flee attempt.</returns>
    public Task<FleeFromCombatResult> Handle(FleeFromCombatCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var enemy = request.Enemy;

        // Base flee chance: 30% + 2% per point of Dexterity, capped at 90%
        double fleeChance = Math.Min(0.90, 0.30 + (player.Dexterity * 0.02));
        bool fled = Random.Shared.NextDouble() < fleeChance;

        if (fled)
        {
            _logger.LogInformation("Player {Name} successfully fled from {Enemy}", player.Name, enemy.Name);

            request.CombatLog?.AddEntry($"{player.Name} fled from {enemy.Name}!", CombatLogType.Info);

            return Task.FromResult(new FleeFromCombatResult
            {
                Success = true,
                Message = $"You successfully fled from {enemy.Name}!"
            });
        }

        // Failed flee — enemy gets a free attack (half damage)
        int counterDamage = Math.Max(1, enemy.BasePhysicalDamage / 2);
        player.Health = Math.Max(0, player.Health - counterDamage);

        _logger.LogInformation("Player {Name} failed to flee from {Enemy}, took {Damage} damage",
            player.Name, enemy.Name, counterDamage);

        request.CombatLog?.AddEntry($"{player.Name} failed to flee! {enemy.Name} counterattacks for {counterDamage} damage!", CombatLogType.EnemyAttack);

        return Task.FromResult(new FleeFromCombatResult
        {
            Success = false,
            Message = $"Failed to flee! {enemy.Name} counterattacks for {counterDamage} damage!"
        });
    }
}
