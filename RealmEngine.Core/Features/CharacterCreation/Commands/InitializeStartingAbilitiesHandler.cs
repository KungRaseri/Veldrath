using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles <see cref="InitializeStartingAbilitiesCommand"/>.
/// Reads level-1 <see cref="ClassAbilityUnlock"/> rows via <see cref="ICharacterClassRepository"/>
/// and dispatches a <see cref="LearnPowerCommand"/> for each one.
/// </summary>
public class InitializeStartingAbilitiesHandler : IRequestHandler<InitializeStartingAbilitiesCommand, InitializeStartingAbilitiesResult>
{
    private readonly IMediator _mediator;
    private readonly ICharacterClassRepository _classRepository;
    private readonly ILogger<InitializeStartingAbilitiesHandler> _logger;

    /// <param name="mediator">MediatR dispatcher used to send <see cref="LearnPowerCommand"/>.</param>
    /// <param name="classRepository">Repository that exposes <c>StartingPowerIds</c> from the DB.</param>
    /// <param name="logger">Logger.</param>
    public InitializeStartingAbilitiesHandler(
        IMediator mediator,
        ICharacterClassRepository classRepository,
        ILogger<InitializeStartingAbilitiesHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InitializeStartingAbilitiesResult> Handle(InitializeStartingAbilitiesCommand request, CancellationToken cancellationToken)
    {
        var abilitiesLearned = 0;
        var abilityIds = new List<string>();

        var characterClass = _classRepository.GetByName(request.ClassName);
        var startingAbilities = characterClass?.StartingPowerIds ?? [];

        if (startingAbilities.Count == 0)
        {
            _logger.LogWarning("No starting abilities found for class {ClassName}", request.ClassName);
            return new InitializeStartingAbilitiesResult
            {
                Success = true,
                AbilitiesLearned = 0,
                Message = $"No starting abilities for {request.ClassName}"
            };
        }

        foreach (var abilityId in startingAbilities)
        {
            try
            {
                var result = await _mediator.Send(new LearnPowerCommand
                {
                    Character = request.Character,
                    PowerId = abilityId
                }, cancellationToken);

                if (result.Success)
                {
                    abilitiesLearned++;
                    abilityIds.Add(abilityId);
                    _logger.LogInformation("Character {CharacterName} learned starting ability: {AbilityId}",
                        request.Character.Name, abilityId);
                }
                else
                {
                    _logger.LogWarning("Failed to teach starting ability {AbilityId} to {CharacterName}: {Message}",
                        abilityId, request.Character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error teaching starting ability {AbilityId} to {CharacterName}",
                    abilityId, request.Character.Name);
            }
        }

        return new InitializeStartingAbilitiesResult
        {
            Success = true,
            AbilitiesLearned = abilitiesLearned,
            AbilityIds = abilityIds,
            Message = $"Learned {abilitiesLearned} starting abilities"
        };
    }
}
