using RealmEngine.Core.Features.Progression.Services;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting learnable spells for a character.
/// </summary>
public class GetLearnableSpellsHandler : IRequestHandler<GetLearnableSpellsQuery, GetLearnableSpellsResult>
{
    private readonly SpellDataService _spellService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLearnableSpellsHandler"/> class.
    /// </summary>
    /// <param name="spellService">The spell catalog service.</param>
    public GetLearnableSpellsHandler(SpellDataService spellService)
    {
        _spellService = spellService ?? throw new ArgumentNullException(nameof(spellService));
    }

    /// <summary>
    /// Handles getting learnable spells.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learnable spells result.</returns>
    public Task<GetLearnableSpellsResult> Handle(GetLearnableSpellsQuery request, CancellationToken cancellationToken)
    {
        var spells = request.Tradition.HasValue
            ? _spellService.GetSpellsByTradition(request.Tradition.Value)
            : _spellService.GetLearnableSpells(request.Character);

        return Task.FromResult(new GetLearnableSpellsResult
        {
            Spells = spells,
            TotalCount = spells.Count
        });
    }
}
