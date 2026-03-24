using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles learning a new power.
/// </summary>
public class LearnPowerHandler : IRequestHandler<LearnPowerCommand, LearnPowerResult>
{
    private readonly PowerDataService _powerCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnPowerHandler"/> class.
    /// </summary>
    /// <param name="powerCatalog">The power catalog service.</param>
    public LearnPowerHandler(PowerDataService powerCatalog)
    {
        _powerCatalog = powerCatalog ?? throw new ArgumentNullException(nameof(powerCatalog));
    }

    /// <summary>
    /// Handles learning a new power.
    /// </summary>
    /// <param name="request">The learn power command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learn power result.</returns>
    public Task<LearnPowerResult> Handle(LearnPowerCommand request, CancellationToken cancellationToken)
    {
        var power = _powerCatalog.GetPower(request.PowerId);
        if (power == null)
        {
            return Task.FromResult(new LearnPowerResult
            {
                Success = false,
                Message = $"Unknown ability: {request.PowerId}"
            });
        }

        // Check if already learned
        if (request.Character.LearnedAbilities.ContainsKey(request.PowerId))
        {
            return Task.FromResult(new LearnPowerResult
            {
                Success = false,
                Message = $"You already know {power.DisplayName}!"
            });
        }

        // Check level requirement
        if (request.Character.Level < power.RequiredLevel)
        {
            return Task.FromResult(new LearnPowerResult
            {
                Success = false,
                Message = $"You must be level {power.RequiredLevel} to learn {power.DisplayName}."
            });
        }

        // Check class restrictions
        if (power.AllowedClasses.Count > 0 &&
            !power.AllowedClasses.Contains(request.Character.ClassName))
        {
            return Task.FromResult(new LearnPowerResult
            {
                Success = false,
                Message = $"{power.DisplayName} is not available to your class."
            });
        }

        // Learn the ability
        request.Character.LearnedAbilities[request.PowerId] = new CharacterAbility
        {
            AbilityId = request.PowerId,
            LearnedDate = DateTime.UtcNow,
            TimesUsed = 0
        };

        return Task.FromResult(new LearnPowerResult
        {
            Success = true,
            Message = $"You have learned {power.DisplayName}!",
            PowerLearned = power
        });
    }
}
