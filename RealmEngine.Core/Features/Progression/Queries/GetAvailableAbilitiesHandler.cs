using RealmEngine.Core.Features.Progression.Services;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting available abilities for a character.
/// </summary>
public class GetAvailableAbilitiesHandler : IRequestHandler<GetAvailableAbilitiesQuery, GetAvailableAbilitiesResult>
{
    private readonly AbilityDataService _abilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableAbilitiesHandler"/> class.
    /// </summary>
    /// <param name="abilityService">The ability catalog service.</param>
    public GetAvailableAbilitiesHandler(AbilityDataService abilityService)
    {
        _abilityService = abilityService ?? throw new ArgumentNullException(nameof(abilityService));
    }

    /// <summary>
    /// Handles getting available abilities.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The available abilities result.</returns>
    public Task<GetAvailableAbilitiesResult> Handle(GetAvailableAbilitiesQuery request, CancellationToken cancellationToken)
    {
        var abilities = request.Tier.HasValue
            ? _abilityService.GetAbilitiesByTier(request.Tier.Value)
            : _abilityService.GetUnlockableAbilities(request.ClassName, request.Level);

        return Task.FromResult(new GetAvailableAbilitiesResult
        {
            Abilities = abilities,
            TotalCount = abilities.Count
        });
    }
}
