using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Handler for previewing next level's stat gains.
/// </summary>
public class PreviewLevelUpHandler : IRequestHandler<PreviewLevelUpQuery, PreviewLevelUpResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ICharacterClassRepository _classRepository;
    private readonly ILogger<PreviewLevelUpHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewLevelUpHandler"/> class.
    /// </summary>
    public PreviewLevelUpHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ICharacterClassRepository classRepository,
        ILogger<PreviewLevelUpHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the preview level up query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing level up preview.</returns>
    public Task<PreviewLevelUpResult> Handle(PreviewLevelUpQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new PreviewLevelUpResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new PreviewLevelUpResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new PreviewLevelUpResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Calculate next level requirements (100 XP per level)
            var nextLevel = character.Level + 1;
            var requiredXP = character.Level * 100;
            var canLevelUp = character.Experience >= requiredXP;

            // Calculate stat gains (10 HP, 5 mana per level)
            var healthGain = 10;
            var manaGain = 5;
            
            var statGains = new Dictionary<string, int>
            {
                ["MaxHealth"] = healthGain,
                ["MaxMana"] = manaGain
            };

            // Points per level (typically 1 attribute + 1 skill, but could vary by class)
            var attributePointsGain = 1;
            var skillPointsGain = 1;

            // Get unlocked abilities (query class progression data)
            var unlockedAbilities = GetAbilitiesForLevel(character.ClassName, nextLevel);

            _logger.LogDebug(
                "Previewing level up for {CharacterName}: {CurrentLevel} -> {NextLevel}. HP: +{HP}, Mana: +{Mana}, Abilities: {Abilities}",
                request.CharacterName,
                character.Level,
                nextLevel,
                healthGain,
                manaGain,
                string.Join(", ", unlockedAbilities)
            );

            return Task.FromResult(new PreviewLevelUpResult
            {
                Success = true,
                CurrentLevel = character.Level,
                NextLevel = nextLevel,
                AttributePointsGain = attributePointsGain,
                SkillPointsGain = skillPointsGain,
                StatGains = statGains,
                UnlockedAbilities = unlockedAbilities,
                CanLevelUp = canLevelUp,
                RequiredExperience = requiredXP
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview level up for character {CharacterName}", request.CharacterName);
            return Task.FromResult(new PreviewLevelUpResult
            {
                Success = false,
                ErrorMessage = $"Failed to preview level up: {ex.Message}"
            });
        }
    }

    private List<string> GetAbilitiesForLevel(string className, int level)
    {
        if (string.IsNullOrWhiteSpace(className))
            return [];

        try
        {
            var characterClass = _classRepository.GetByName(className);
            if (characterClass == null)
            {
                _logger.LogWarning("Class {ClassName} not found in repository", className);
                return [];
            }

            if (characterClass.Progression?.AbilityUnlocks.TryGetValue(level, out var abilities) != true || abilities == null)
                return [];

            return abilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading abilities for class {ClassName} at level {Level}", className, level);
            return [];
        }
    }
}
