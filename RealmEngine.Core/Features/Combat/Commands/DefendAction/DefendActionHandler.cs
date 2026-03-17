using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Combat.Commands.DefendAction;

/// <summary>
/// Handles the DefendAction command.
/// </summary>
public class DefendActionHandler : IRequestHandler<DefendActionCommand, DefendActionResult>
{
    /// <summary>
    /// Handles the defend action command and returns the defense bonus.
    /// </summary>
    /// <param name="request">The defend action command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the defend action result.</returns>
    public Task<DefendActionResult> Handle(DefendActionCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var combatLog = request.CombatLog;

        // Apply temporary defense bonus (based on Constitution)
        var defenseBonus = player.Constitution / 2;

        combatLog?.AddEntry($"{player.Name} takes a defensive stance! (Defense +{defenseBonus})");
        
        _logger.LogInformation("Player {PlayerName} defended (bonus: {DefenseBonus})", 
            player.Name, defenseBonus);

        return Task.FromResult(new DefendActionResult
        {
            DefenseBonus = defenseBonus,
            Message = $"You brace yourself for impact! Defense +{defenseBonus}"
        });
    }
}