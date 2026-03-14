using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Handler for explicitly leveling up a character.
/// </summary>
public class LevelUpHandler : IRequestHandler<LevelUpCommand, LevelUpResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ICharacterClassRepository _classRepository;
    private readonly ILogger<LevelUpHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LevelUpHandler"/> class.
    /// </summary>
    public LevelUpHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ICharacterClassRepository classRepository,
        ILogger<LevelUpHandler> logger,
        IMediator mediator)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Handles the level up command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing level up details.</returns>
    public Task<LevelUpResult> Handle(LevelUpCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new LevelUpResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new LevelUpResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new LevelUpResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Check if character can level up
            var requiredXP = character.Level * 100;
            if (character.Experience < requiredXP)
            {
                return Task.FromResult(new LevelUpResult
                {
                    Success = false,
                    ErrorMessage = $"Insufficient experience. Need {requiredXP - character.Experience} more XP to level up."
                });
            }

            // Store old stats
            var oldLevel = character.Level;
            var oldMaxHealth = character.MaxHealth;
            var oldMaxMana = character.MaxMana;

            // Perform level up - deduct XP and call character's level up logic
            character.Experience -= requiredXP;
            // Force one level up (private method, so replicate logic)
            character.Level++;
            character.MaxHealth = character.GetMaxHealth();
            character.Health = character.MaxHealth;
            character.MaxMana = character.GetMaxMana();
            character.Mana = character.MaxMana;
            
            var attributePointsGained = 3;
            var skillPointsGained = 1;
            if (character.Level % 5 == 0)
            {
                attributePointsGained += 2;
                skillPointsGained += 1;
            }
            character.UnspentAttributePoints += attributePointsGained;
            character.UnspentSkillPoints += skillPointsGained;

            // Calculate stat increases
            var healthGain = character.MaxHealth - oldMaxHealth;
            var manaGain = character.MaxMana - oldMaxMana;

            var statIncreases = new Dictionary<string, int>
            {
                ["MaxHealth"] = healthGain,
                ["MaxMana"] = manaGain
            };

            // Get unlocked abilities (query class progression)
            var unlockedAbilities = GetAbilitiesForLevel(character.ClassName, character.Level);

            _logger.LogInformation(
                "Character {CharacterName} leveled up from {OldLevel} to {NewLevel}. HP: +{HP}, Mana: +{Mana}, Abilities: {Abilities}",
                request.CharacterName,
                oldLevel,
                character.Level,
                healthGain,
                manaGain,
                string.Join(", ", unlockedAbilities)
            );

            return Task.FromResult(new LevelUpResult
            {
                Success = true,
                OldLevel = oldLevel,
                NewLevel = character.Level,
                AttributePointsGained = attributePointsGained,
                SkillPointsGained = skillPointsGained,
                StatIncreases = statIncreases,
                UnlockedAbilities = unlockedAbilities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to level up character {CharacterName}", request.CharacterName);
            return Task.FromResult(new LevelUpResult
            {
                Success = false,
                ErrorMessage = $"Failed to level up: {ex.Message}"
            });
        }
    }

    private List<string> GetAbilitiesForLevel(string className, int level)
    {
        if (string.IsNullOrWhiteSpace(className))
            return [];

        try
        {
            var characterClass = _classRepository.GetClassByName(className);
            if (characterClass == null)
            {
                _logger.LogWarning("Class {ClassName} not found in repository", className);
                return [];
            }

            if (!characterClass.AbilityUnlocks.TryGetValue(level, out var abilities))
                return [];

            _logger.LogInformation("Found {Count} abilities unlocking at level {Level} for {ClassName}: {Abilities}",
                abilities.Count, level, className, string.Join(", ", abilities));
            return abilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading abilities for class {ClassName} at level {Level}", className, level);
            return [];
        }
    }
}
