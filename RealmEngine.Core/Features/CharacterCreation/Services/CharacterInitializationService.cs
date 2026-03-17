using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Services;

/// <summary>
/// Service for initializing new characters with starting abilities, spells, and equipment.
/// </summary>
public class CharacterInitializationService
{
    private readonly AbilityDataService _abilityCatalogService;
    private readonly SpellCastingService _spellCastingService;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterInitializationService"/> class.
    /// </summary>
    /// <param name="abilityCatalogService">The ability catalog service.</param>
    /// <param name="spellCastingService">The spell casting service.</param>
    /// <param name="mediator">The mediator for sending commands.</param>
    public CharacterInitializationService(
        AbilityDataService abilityCatalogService,
        SpellCastingService spellCastingService,
        IMediator mediator)
    {
        _abilityCatalogService = abilityCatalogService ?? throw new ArgumentNullException(nameof(abilityCatalogService));
        _spellCastingService = spellCastingService ?? throw new ArgumentNullException(nameof(spellCastingService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Initialize starting abilities for a character based on their class.
    /// </summary>
    public async Task<int> InitializeStartingAbilitiesAsync(Character character, CharacterClass characterClass)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (characterClass == null) throw new ArgumentNullException(nameof(characterClass));

        int abilitiesLearned = 0;

        // Get starting abilities from class definition
        var startingAbilityIds = GetStartingAbilitiesForClass(character.ClassName);
        
        if (startingAbilityIds.Count == 0)
        {
            _logger.LogWarning("No starting abilities defined for class {ClassName}", character.ClassName);
            return 0;
        }

        // Learn each starting ability
        foreach (var abilityId in startingAbilityIds)
        {
            try
            {
                var command = new LearnAbilityCommand
                {
                    Character = character,
                    AbilityId = abilityId
                };

                var result = await _mediator.Send(command);
                
                if (result.Success)
                {
                    abilitiesLearned++;
                    _logger.LogInformation("Character {CharacterName} learned starting ability: {AbilityId}", 
                        character.Name, abilityId);
                }
                else
                {
                    _logger.LogWarning("Failed to learn starting ability {AbilityId} for {CharacterName}: {Message}",
                        abilityId, character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning starting ability {AbilityId} for {CharacterName}", 
                    abilityId, character.Name);
            }
        }

        return abilitiesLearned;
    }

    /// <summary>
    /// Initialize starting spells for a character based on their class.
    /// </summary>
    public async Task<int> InitializeStartingSpellsAsync(Character character, CharacterClass characterClass)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (characterClass == null) throw new ArgumentNullException(nameof(characterClass));

        int spellsLearned = 0;

        // Get starting spells from class definition
        var startingSpellIds = GetStartingSpellsForClass(character.ClassName);
        
        if (startingSpellIds.Count == 0)
        {
            // Not all classes are spellcasters
            _logger.LogDebug("No starting spells for class {ClassName} (non-spellcaster)", character.ClassName);
            return 0;
        }

        // Learn each starting spell
        foreach (var spellId in startingSpellIds)
        {
            try
            {
                var command = new LearnSpellCommand
                {
                    Character = character,
                    SpellId = spellId
                };

                var result = await _mediator.Send(command);
                
                if (result.Success)
                {
                    spellsLearned++;
                    _logger.LogInformation("Character {CharacterName} learned starting spell: {SpellId}", 
                        character.Name, spellId);
                }
                else
                {
                    _logger.LogWarning("Failed to learn starting spell {SpellId} for {CharacterName}: {Message}",
                        spellId, character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning starting spell {SpellId} for {CharacterName}", 
                    spellId, character.Name);
            }
        }

        return spellsLearned;
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
                "active/offensive:power-shot",
                "active/support:hunters-mark",
                "active/utility:trap"
            },
            "paladin" => new List<string>
            {
                "active/offensive:holy-strike",
                "active/support:lay-on-hands",
                "active/support:protective-aura"
            },
            _ => new List<string>()
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
                "arcane/offensive:magic-missile",
                "arcane/offensive:fire-bolt",
                "arcane/defensive:shield"
            },
            "cleric" => new List<string>
            {
                "divine/offensive:sacred-flame",
                "divine/support:cure-wounds",
                "divine/support:bless"
            },
            "paladin" => new List<string>
            {
                "divine/offensive:smite",
                "divine/support:healing-word"
            },
            "ranger" => new List<string>
            {
                "primal/utility:hunters-mark",
                "primal/support:cure-wounds"
            },
            _ => new List<string>() // Warrior and Rogue are not spellcasters
        };
    }
}
