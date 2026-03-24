using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles <see cref="InitializeStartingPowersCommand"/>.
/// Reads level-1 <see cref="ClassPowerUnlock"/> rows via <see cref="ICharacterClassRepository"/>
/// and dispatches a <see cref="LearnSpellCommand"/> for each tradition-based power, or
/// <see cref="LearnPowerCommand"/> for non-spell powers.
/// Non-spellcaster classes have no level-1 power unlocks and return success with zero powers learned.
/// </summary>
public class InitializeStartingPowersHandler : IRequestHandler<InitializeStartingPowersCommand, InitializeStartingPowersResult>
{
    private readonly IMediator _mediator;
    private readonly ICharacterClassRepository _classRepository;
    private readonly ILogger<InitializeStartingPowersHandler> _logger;

    /// <param name="mediator">MediatR dispatcher used to send learn commands.</param>
    /// <param name="classRepository">Repository that exposes <c>StartingPowerIds</c> from the DB.</param>
    /// <param name="logger">Logger.</param>
    public InitializeStartingPowersHandler(
        IMediator mediator,
        ICharacterClassRepository classRepository,
        ILogger<InitializeStartingPowersHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InitializeStartingPowersResult> Handle(InitializeStartingPowersCommand request, CancellationToken cancellationToken)
    {
        var powersLearned = 0;
        var powerIds = new List<string>();

        var characterClass = _classRepository.GetByName(request.ClassName);
        var startingPowers = characterClass?.StartingPowerIds ?? [];

        if (startingPowers.Count == 0)
        {
            _logger.LogDebug("No starting powers for class {ClassName} (none configured)", request.ClassName);
            return new InitializeStartingPowersResult
            {
                Success = true,
                PowersLearned = 0,
                Message = $"No starting powers for {request.ClassName}"
            };
        }

        foreach (var powerId in startingPowers)
        {
            try
            {
                var result = await _mediator.Send(new LearnSpellCommand
                {
                    Character = request.Character,
                    SpellId = powerId
                }, cancellationToken);

                if (result.Success)
                {
                    powersLearned++;
                    powerIds.Add(powerId);
                    _logger.LogInformation("Character {CharacterName} learned starting power: {PowerId}",
                        request.Character.Name, powerId);
                }
                else
                {
                    _logger.LogWarning("Failed to teach starting power {PowerId} to {CharacterName}: {Message}",
                        powerId, request.Character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error teaching starting power {PowerId} to {CharacterName}",
                    powerId, request.Character.Name);
            }
        }

        return new InitializeStartingPowersResult
        {
            Success = true,
            PowersLearned = powersLearned,
            PowerIds = powerIds,
            Message = $"Learned {powersLearned} starting powers"
        };
    }
}

