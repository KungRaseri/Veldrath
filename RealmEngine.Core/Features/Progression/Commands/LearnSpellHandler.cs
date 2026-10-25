using MediatR;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles learning a spell from a spellbook.
/// </summary>
public class LearnSpellHandler : IRequestHandler<LearnSpellCommand, LearnSpellResult>
{
    private readonly SpellCastingService _spellService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnSpellHandler"/> class.
    /// </summary>
    /// <param name="spellService">The spell casting service.</param>
    public LearnSpellHandler(SpellCastingService spellService)
    {
        _spellService = spellService ?? throw new ArgumentNullException(nameof(spellService));
    }

    /// <summary>
    /// Handles learning a spell from a spellbook.
    /// </summary>
    /// <param name="request">The learn spell command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The learn spell result.</returns>
    public Task<LearnSpellResult> Handle(LearnSpellCommand request, CancellationToken cancellationToken)
    {
        var result = _spellService.LearnSpell(request.Character, request.SpellId);

        return Task.FromResult(new LearnSpellResult
        {
            Success = result.Success,
            Message = result.Message,
            SpellLearned = result.SpellLearned
        });
    }
}
