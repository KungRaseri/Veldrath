using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles initializing starting abilities for a new character.
/// </summary>
public class InitializeStartingAbilitiesHandler : IRequestHandler<InitializeStartingAbilitiesCommand, InitializeStartingAbilitiesResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<InitializeStartingAbilitiesHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeStartingAbilitiesHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator for sending commands.</param>
    /// <param name="logger">The logger.</param>
    public InitializeStartingAbilitiesHandler(IMediator mediator, ILogger<InitializeStartingAbilitiesHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger;
    }

    /// <summary>
    /// Handles the initialize starting abilities command and returns the result.
    /// </summary>
    /// <param name="request">The initialize starting abilities command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the initialization result.</returns>
    public async Task<InitializeStartingAbilitiesResult> Handle(InitializeStartingAbilitiesCommand request, CancellationToken cancellationToken)
    {
        var abilitiesLearned = 0;
        var abilityIds = new List<string>();
        
        // Get starting abilities for class
        var startingAbilities = GetStartingAbilitiesForClass(request.ClassName);
        
        if (startingAbilities.Count == 0)
        {
            _logger.LogWarning("No starting abilities defined for class {ClassName}", request.ClassName);
            return new InitializeStartingAbilitiesResult
            {
                Success = true,
                AbilitiesLearned = 0,
                Message = $"No starting abilities for {request.ClassName}"
            };
        }

        // Learn each ability
        foreach (var abilityId in startingAbilities)
        {
            try
            {
                var command = new LearnAbilityCommand
                {
                    Character = request.Character,
                    AbilityId = abilityId
                };

                var result = await _mediator.Send(command, cancellationToken);
                
                if (result.Success)
                {
                    abilitiesLearned++;
                    abilityIds.Add(abilityId);
                    _logger.LogInformation("Character {CharacterName} learned starting ability: {AbilityId}", 
                        request.Character.Name, abilityId);
                }
                else
                {
                    _logger.LogWarning("Failed to learn starting ability {AbilityId} for {CharacterName}: {Message}",
                        abilityId, request.Character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning starting ability {AbilityId} for {CharacterName}", 
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

    /// <summary>
    /// Get starting abilities for a class based on class name.
    /// Maps class names to their starting ability IDs.
    /// </summary>
    private List<string> GetStartingAbilitiesForClass(string className)
    {
        return className.ToLower() switch
        {
            "warrior" => new List<string>
            {
                "active/offensive:shield-bash",
                "active/support:second-wind",
                "active/support:battle-cry"
            },
            "rogue" => new List<string>
            {
                "active/offensive:backstab",
                "active/defensive:evasion",
                "active/mobility:shadow-step"
            },
            "mage" => new List<string>
            {
                "active/offensive:magic-missile",
                "active/offensive:fireball",
                "active/defensive:arcane-shield"
            },
            "cleric" => new List<string>
            {
                "active/offensive:smite",
                "active/support:heal",
                "active/defensive:divine-shield"
            },
            "ranger" => new List<string>
            {
                "active/support:hunters-mark",
                "active/utility:trap"
            },
            "paladin" => new List<string>
            {
                "active/support:lay-on-hands",
                "active/defensive:divine-shield"
            },
            _ => new List<string>()
        };
    }
}
