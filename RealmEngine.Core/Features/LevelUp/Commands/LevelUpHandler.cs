using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Handler for explicitly leveling up a character.
/// </summary>
public class LevelUpHandler : IRequestHandler<LevelUpCommand, LevelUpResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly GameDataCache _dataCache;
    private readonly ILogger<LevelUpHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LevelUpHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="dataCache">The game data cache for loading class data.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mediator">The mediator for publishing events.</param>
    public LevelUpHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        GameDataCache dataCache,
        ILogger<LevelUpHandler> logger,
        IMediator mediator)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
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
            var unlockedAbilities = GetAbilitiesForLevel(character.CharacterClass, character.Level);

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
