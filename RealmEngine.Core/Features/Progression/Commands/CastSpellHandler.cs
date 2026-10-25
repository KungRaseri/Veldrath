using MediatR;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles casting a spell.
/// </summary>
public class CastSpellHandler : IRequestHandler<CastSpellCommand, CastSpellResult>
{
    private readonly SpellCastingService _spellService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastSpellHandler"/> class.
    /// </summary>
    /// <param name="spellService">The spell casting service.</param>
    public CastSpellHandler(SpellCastingService spellService)
    {
        _spellService = spellService ?? throw new ArgumentNullException(nameof(spellService));
    }

    /// <summary>
    /// Handles casting a spell.
    /// </summary>
    /// <param name="request">The cast spell command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cast spell result.</returns>
    public Task<CastSpellResult> Handle(CastSpellCommand request, CancellationToken cancellationToken)
    {
        var result = _spellService.CastSpell(
            request.Caster,
            request.SpellId,
            request.Target);

        return Task.FromResult(new CastSpellResult
        {
            Success = result.Success,
            Message = result.Message,
            ManaCostPaid = result.ManaCostPaid,
            EffectValue = result.EffectValue,
            WasFizzle = result.WasFizzle,
            SpellCast = result.SpellCast
        });
    }
}
