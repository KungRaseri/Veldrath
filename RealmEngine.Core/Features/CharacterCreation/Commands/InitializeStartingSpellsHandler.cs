using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles <see cref="InitializeStartingSpellsCommand"/>.
/// Reads level-1 <see cref="ClassSpellUnlock"/> rows via <see cref="ICharacterClassRepository"/>
/// and dispatches a <see cref="LearnSpellCommand"/> for each one. Non-spellcaster
/// classes have no level-1 spell unlocks and return success with zero spells learned.
/// </summary>
public class InitializeStartingSpellsHandler : IRequestHandler<InitializeStartingSpellsCommand, InitializeStartingSpellsResult>
{
    private readonly IMediator _mediator;
    private readonly ICharacterClassRepository _classRepository;
    private readonly ILogger<InitializeStartingSpellsHandler> _logger;

    /// <param name="mediator">MediatR dispatcher used to send <see cref="LearnSpellCommand"/>.</param>
    /// <param name="classRepository">Repository that exposes <c>StartingPowerIds</c> from the DB.</param>
    /// <param name="logger">Logger.</param>
    public InitializeStartingSpellsHandler(
        IMediator mediator,
        ICharacterClassRepository classRepository,
        ILogger<InitializeStartingSpellsHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InitializeStartingSpellsResult> Handle(InitializeStartingSpellsCommand request, CancellationToken cancellationToken)
    {
        var spellsLearned = 0;
        var spellIds = new List<string>();

        var characterClass = _classRepository.GetByName(request.ClassName);
        var startingSpells = characterClass?.StartingPowerIds ?? [];

        if (startingSpells.Count == 0)
        {
            _logger.LogDebug("No starting spells for class {ClassName} (non-spellcaster or none configured)", request.ClassName);
            return new InitializeStartingSpellsResult
            {
                Success = true,
                SpellsLearned = 0,
                Message = $"No starting spells for {request.ClassName}"
            };
        }

        foreach (var spellId in startingSpells)
        {
            try
            {
                var result = await _mediator.Send(new LearnSpellCommand
                {
                    Character = request.Character,
                    SpellId = spellId
                }, cancellationToken);

                if (result.Success)
                {
                    spellsLearned++;
                    spellIds.Add(spellId);
                    _logger.LogInformation("Character {CharacterName} learned starting spell: {SpellId}",
                        request.Character.Name, spellId);
                }
                else
                {
                    _logger.LogWarning("Failed to teach starting spell {SpellId} to {CharacterName}: {Message}",
                        spellId, request.Character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error teaching starting spell {SpellId} to {CharacterName}",
                    spellId, request.Character.Name);
            }
        }

        return new InitializeStartingSpellsResult
        {
            Success = true,
            SpellsLearned = spellsLearned,
            SpellIds = spellIds,
            Message = $"Learned {spellsLearned} starting spells"
        };
    }
}

