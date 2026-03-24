using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles learning a new ability.
/// </summary>
public class LearnAbilityHandler : IRequestHandler<LearnAbilityCommand, LearnAbilityResult>
{
    private readonly PowerDataService _powerCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnAbilityHandler"/> class.
    /// </summary>
    /// <param name="powerCatalog">The power catalog service.</param>
    public LearnAbilityHandler(PowerDataService powerCatalog)
    {
        _powerCatalog = powerCatalog ?? throw new ArgumentNullException(nameof(powerCatalog));
    }

    /// <summary>
    /// Handles learning a new ability.
    /// </summary>
    /// <param name="request">The learn ability command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learn ability result.</returns>
    public Task<LearnAbilityResult> Handle(LearnAbilityCommand request, CancellationToken cancellationToken)
    {
        var power = _powerCatalog.GetPower(request.AbilityId);
        if (power == null)
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"Unknown ability: {request.AbilityId}"
            });
        }

        // Check if already learned
        if (request.Character.LearnedAbilities.ContainsKey(request.AbilityId))
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"You already know {power.DisplayName}!"
            });
        }

        // Check level requirement
        if (request.Character.Level < power.RequiredLevel)
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"You must be level {power.RequiredLevel} to learn {power.DisplayName}."
            });
        }

        // Check class restrictions
        if (power.AllowedClasses.Count > 0 &&
            !power.AllowedClasses.Contains(request.Character.ClassName))
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"{power.DisplayName} is not available to your class."
            });
        }

        // Learn the ability
        request.Character.LearnedAbilities[request.AbilityId] = new CharacterAbility
        {
            AbilityId = request.AbilityId,
            LearnedDate = DateTime.UtcNow,
            TimesUsed = 0
        };

        return Task.FromResult(new LearnAbilityResult
        {
            Success = true,
            Message = $"You have learned {power.DisplayName}!",
            AbilityLearned = power
        });
    }
}
