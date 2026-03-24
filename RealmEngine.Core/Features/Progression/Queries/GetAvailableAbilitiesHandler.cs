using RealmEngine.Core.Features.Progression.Services;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting available powers for a character class and level.
/// </summary>
public class GetAvailableAbilitiesHandler : IRequestHandler<GetAvailableAbilitiesQuery, GetAvailableAbilitiesResult>
{
    private readonly PowerDataService _powerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableAbilitiesHandler"/> class.
    /// </summary>
    /// <param name="powerService">The power catalog service.</param>
    public GetAvailableAbilitiesHandler(PowerDataService powerService)
    {
        _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
    }

    /// <summary>
    /// Handles getting available powers.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The available powers result.</returns>
    public Task<GetAvailableAbilitiesResult> Handle(GetAvailableAbilitiesQuery request, CancellationToken cancellationToken)
    {
        var powers = request.Tier.HasValue
            ? _powerService.GetPowersByTier(request.Tier.Value)
            : _powerService.GetUnlockablePowers(request.ClassName, request.Level);

        return Task.FromResult(new GetAvailableAbilitiesResult
        {
            Abilities = powers,
            TotalCount = powers.Count
        });
    }
}
