using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Services;

/// <summary>
/// Orchestrates the initial stat/ability/spell setup for a newly created character.
/// Power IDs are sourced from <see cref="ICharacterClassRepository"/> —
/// no hardcoded class mappings; all data is driven by <c>ClassPowerUnlock</c>
/// rows in the content database.
/// </summary>
public class CharacterInitializationService
{
    private readonly PowerDataService _powerCatalogService;
    private readonly SpellCastingService _spellCastingService;
    private readonly ICharacterClassRepository _classRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CharacterInitializationService> _logger;

    /// <param name="powerCatalogService">Catalog service for power lookup.</param>
    /// <param name="spellCastingService">Service for spell-casting rules.</param>
    /// <param name="classRepository">Repository that exposes <c>StartingPowerIds</c> from the DB.</param>
    /// <param name="mediator">MediatR dispatcher used to send <see cref="LearnAbilityCommand"/> and <see cref="LearnSpellCommand"/>.</param>
    /// <param name="logger">Logger.</param>
    public CharacterInitializationService(
        PowerDataService powerCatalogService,
        SpellCastingService spellCastingService,
        ICharacterClassRepository classRepository,
        IMediator mediator,
        ILogger<CharacterInitializationService> logger)
    {
        _powerCatalogService = powerCatalogService ?? throw new ArgumentNullException(nameof(powerCatalogService));
        _spellCastingService = spellCastingService ?? throw new ArgumentNullException(nameof(spellCastingService));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger;
    }

    /// <summary>
    /// Grants each level-1 power from the class definition to the character
    /// by dispatching a <see cref="LearnAbilityCommand"/> per power. Power IDs
    /// are sourced from <see cref="ICharacterClassRepository"/> (<c>StartingPowerIds</c>),
    /// which reflect <c>ClassPowerUnlock</c> rows with <c>LevelRequired == 1</c>.
    /// </summary>
    /// <returns>Number of powers successfully learned.</returns>
    public async Task<int> InitializeStartingAbilitiesAsync(Character character, CharacterClass characterClass)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (characterClass == null) throw new ArgumentNullException(nameof(characterClass));

        var startingAbilityIds = _classRepository.GetByName(character.ClassName)?.StartingPowerIds ?? [];

        if (startingAbilityIds.Count == 0)
        {
            _logger.LogWarning("No starting abilities found for class {ClassName}", character.ClassName);
            return 0;
        }

        int abilitiesLearned = 0;
        foreach (var abilityId in startingAbilityIds)
        {
            try
            {
                var result = await _mediator.Send(new LearnAbilityCommand { Character = character, AbilityId = abilityId });
                if (result.Success)
                {
                    abilitiesLearned++;
                    _logger.LogInformation("Character {CharacterName} learned starting ability: {AbilityId}", character.Name, abilityId);
                }
                else
                {
                    _logger.LogWarning("Failed to teach ability {AbilityId} to {CharacterName}: {Message}", abilityId, character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error teaching starting ability {AbilityId} to {CharacterName}", abilityId, character.Name);
            }
        }

        return abilitiesLearned;
    }

    /// <summary>
    /// Grants each level-1 spell from the class definition to the character
    /// by dispatching a <see cref="LearnSpellCommand"/> per spell. Spell IDs
    /// are sourced from <see cref="ICharacterClassRepository"/> (<c>StartingPowerIds</c>),
    /// which reflect <c>ClassPowerUnlock</c> rows with <c>LevelRequired == 1</c>.
    /// Returns 0 for non-spellcaster classes (empty power unlock list).
    /// </summary>
    /// <returns>Number of spells successfully learned.</returns>
    public async Task<int> InitializeStartingSpellsAsync(Character character, CharacterClass characterClass)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (characterClass == null) throw new ArgumentNullException(nameof(characterClass));

        var startingSpellIds = _classRepository.GetByName(character.ClassName)?.StartingPowerIds ?? [];

        if (startingSpellIds.Count == 0)
        {
            _logger.LogDebug("No starting spells for class {ClassName} (non-spellcaster or none configured)", character.ClassName);
            return 0;
        }

        int spellsLearned = 0;
        foreach (var spellId in startingSpellIds)
        {
            try
            {
                var result = await _mediator.Send(new LearnSpellCommand { Character = character, SpellId = spellId });
                if (result.Success)
                {
                    spellsLearned++;
                    _logger.LogInformation("Character {CharacterName} learned starting spell: {SpellId}", character.Name, spellId);
                }
                else
                {
                    _logger.LogWarning("Failed to teach spell {SpellId} to {CharacterName}: {Message}", spellId, character.Name, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error teaching starting spell {SpellId} to {CharacterName}", spellId, character.Name);
            }
        }

        return spellsLearned;
    }
}
