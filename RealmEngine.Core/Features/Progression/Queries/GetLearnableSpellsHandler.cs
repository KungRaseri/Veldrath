using RealmEngine.Core.Features.Progression.Services;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting learnable spells for a character.
/// </summary>
public class GetLearnableSpellsHandler : IRequestHandler<GetLearnableSpellsQuery, GetLearnableSpellsResult>
{
    private readonly PowerDataService _powerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLearnableSpellsHandler"/> class.
    /// </summary>
    /// <param name="powerService">The power catalog service.</param>
    public GetLearnableSpellsHandler(PowerDataService powerService)
    {
        _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
    }

    /// <summary>
    /// Handles getting learnable spells.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learnable powers result.</returns>
    public Task<GetLearnableSpellsResult> Handle(GetLearnableSpellsQuery request, CancellationToken cancellationToken)
    {
        var powers = request.Tradition.HasValue
            ? _powerService.GetPowersByTradition(request.Tradition.Value)
            : _powerService.GetLearnablePowers(request.Character);

        return Task.FromResult(new GetLearnableSpellsResult
        {
            Spells = powers,
            TotalCount = powers.Count
        });
    }
}
