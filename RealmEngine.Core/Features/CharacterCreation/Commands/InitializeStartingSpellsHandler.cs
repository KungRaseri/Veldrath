using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles initializing starting spells for a new character.
/// </summary>
public class InitializeStartingSpellsHandler : IRequestHandler<InitializeStartingSpellsCommand, InitializeStartingSpellsResult>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeStartingSpellsHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator for sending commands.</param>
    public InitializeStartingSpellsHandler(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Handles the initialize starting spells command and returns the result.
    /// </summary>
    /// <param name="request">The initialize starting spells command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the initialization result.</returns>
    public async Task<InitializeStartingSpellsResult> Handle(InitializeStartingSpellsCommand request, CancellationToken cancellationToken)
    {
        var spellsLearned = 0;
        var spellIds = new List<string>();
        
        // Get starting spells for class
        var startingSpells = GetStartingSpellsForClass(request.ClassName);
        
        if (startingSpells.Count == 0)
        {
            // Not all classes are spellcasters
            _logger.LogDebug("No starting spells for class {ClassName} (non-spellcaster)", request.ClassName);
            return new InitializeStartingSpellsResult
            {
                Success = true,
                SpellsLearned = 0,
                Message = $"No starting spells for {request.ClassName}"
            };
        }

        // Learn each spell
        foreach (var spellId in startingSpells)
        {
            try
            {
                var command = new LearnSpellCommand
                {
                    Character = request.Character,
                    SpellId = spellId
                };

                var result = await _mediator.Send(command, cancellationToken);
                
                if (result.Success)
                {
                    spellsLearned++;
                    spellIds.Add(spellId);
                    _logger.LogInformation("Character {CharacterName} learned starting spell: {SpellId}", 
                        request.Character.Name, spellId);
                }
                else
                {
                    _logger.LogWarning("Failed to learn starting spell {SpellId} for {CharacterName}: {Message}",
                        spellId, request.Character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning starting spell {SpellId} for {CharacterName}", 
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

    /// <summary>
    /// Get starting spells for a class based on class name.
    /// Only spellcasting classes have starting spells.
    /// </summary>
    private List<string> GetStartingSpellsForClass(string className)
    {
        return className.ToLower() switch
        {
            "mage" => new List<string>
            {
                "magic-missile",
                "ray-of-frost",
                "shield"
            },
            "cleric" => new List<string>
            {
                "sacred-flame",
                "cure-wounds",
                "bless"
            },
            "paladin" => new List<string>
            {
                "sacred-flame",
                "cure-wounds"
            },
            _ => new List<string>() // Warrior, Rogue, Ranger are not primary spellcasters
        };
    }
}
