using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles learning a new ability.
/// </summary>
public class LearnAbilityHandler : IRequestHandler<LearnAbilityCommand, LearnAbilityResult>
{
    private readonly AbilityDataService _abilityCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnAbilityHandler"/> class.
    /// </summary>
    /// <param name="abilityCatalog">The ability catalog service.</param>
    public LearnAbilityHandler(AbilityDataService abilityCatalog)
    {
        _abilityCatalog = abilityCatalog ?? throw new ArgumentNullException(nameof(abilityCatalog));
    }

    /// <summary>
    /// Handles learning a new ability.
    /// </summary>
    /// <param name="request">The learn ability command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learn ability result.</returns>
    public Task<LearnAbilityResult> Handle(LearnAbilityCommand request, CancellationToken cancellationToken)
    {
        var ability = _abilityCatalog.GetAbility(request.AbilityId);
        if (ability == null)
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
                Message = $"You already know {ability.DisplayName}!"
            });
        }

        // Check level requirement
        if (request.Character.Level < ability.RequiredLevel)
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"You must be level {ability.RequiredLevel} to learn {ability.DisplayName}."
            });
        }

        // Check class restrictions
        if (ability.AllowedClasses.Count > 0 && 
            !ability.AllowedClasses.Contains(request.Character.ClassName))
        {
            return Task.FromResult(new LearnAbilityResult
            {
                Success = false,
                Message = $"{ability.DisplayName} is not available to your class."
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
            Message = $"You have learned {ability.DisplayName}!",
            AbilityLearned = ability
        });
    }
}
