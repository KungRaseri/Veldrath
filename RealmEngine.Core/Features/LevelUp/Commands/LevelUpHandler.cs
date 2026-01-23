using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Handler for explicitly leveling up a character.
/// </summary>
public class LevelUpHandler : IRequestHandler<LevelUpCommand, LevelUpResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ILogger<LevelUpHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LevelUpHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mediator">The mediator for publishing events.</param>
    public LevelUpHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ILogger<LevelUpHandler> logger,
        IMediator mediator)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
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

            // Get unlocked abilities (would need to query class progression)
            var unlockedAbilities = new List<string>(); // TODO: Query class abilities unlocked at this level

            _logger.LogInformation(
                "Character {CharacterName} leveled up from {OldLevel} to {NewLevel}. HP: +{HP}, Mana: +{Mana}",
                request.CharacterName,
                oldLevel,
                character.Level,
                healthGain,
                manaGain
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
}
