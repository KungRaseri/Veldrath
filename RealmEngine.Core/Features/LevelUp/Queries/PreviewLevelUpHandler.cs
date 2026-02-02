using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Handler for previewing next level's stat gains.
/// </summary>
public class PreviewLevelUpHandler : IRequestHandler<PreviewLevelUpQuery, PreviewLevelUpResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly GameDataCache _dataCache;
    private readonly ILogger<PreviewLevelUpHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewLevelUpHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="dataCache">The game data cache for loading class data.</param>
    /// <param name="logger">The logger instance.</param>
    public PreviewLevelUpHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        GameDataCache dataCache,
        ILogger<PreviewLevelUpHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
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

    /// <summary>
    /// Gets abilities unlocked at a specific level for a character class.
    /// </summary>
    /// <param name="className">The character's class name.</param>
    /// <param name="level">The level to check for ability unlocks.</param>
    /// <returns>List of ability references unlocked at this level.</returns>
    private List<string> GetAbilitiesForLevel(string className, int level)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            _logger.LogWarning("Cannot get abilities for empty class name");
            return new List<string>();
        }

        try
        {
            var catalogFile = _dataCache.GetFile("classes/catalog.json");
            if (catalogFile == null)
            {
                _logger.LogWarning("Classes catalog not found");
                return new List<string>();
            }

            var catalog = JObject.Parse(catalogFile.JsonData.ToString());
            var classTypes = catalog["class_types"] as JObject;

            if (classTypes == null)
            {
                _logger.LogWarning("No class_types found in classes catalog");
                return new List<string>();
            }

            // Search through all class types for this class
            foreach (var classType in classTypes.Properties())
            {
                var typeData = classType.Value as JObject;
                var items = typeData?["items"] as JArray;

                if (items == null)
                    continue;

                foreach (var classItem in items)
                {
                    var name = classItem["name"]?.ToString();
                    if (name != className)
                        continue;

                    // Found the class, now get progression data
                    var progression = classItem["progression"] as JObject;
                    var abilityUnlocks = progression?["abilityUnlocks"] as JObject;

                    if (abilityUnlocks == null)
                    {
                        _logger.LogDebug("No ability unlocks found for class {ClassName}", className);
                        return new List<string>();
                    }

                    // Check if this level has unlocks
                    var levelKey = level.ToString();
                    var abilitiesAtLevel = abilityUnlocks[levelKey] as JArray;

                    if (abilitiesAtLevel == null)
                    {
                        // No abilities unlock at this specific level
                        return new List<string>();
                    }

                    var abilities = abilitiesAtLevel.Select(a => a.ToString()).ToList();
                    _logger.LogInformation("Found {Count} abilities unlocking at level {Level} for {ClassName}: {Abilities}", 
                        abilities.Count, level, className, string.Join(", ", abilities));

                    return abilities;
                }
            }

            _logger.LogWarning("Class {ClassName} not found in catalog", className);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading abilities for class {ClassName} at level {Level}", className, level);
            return new List<string>();
        }
    }
}
