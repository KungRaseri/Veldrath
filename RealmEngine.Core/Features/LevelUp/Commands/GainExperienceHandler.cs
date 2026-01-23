using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Handler for awarding experience points to a character.
/// </summary>
public class GainExperienceHandler : IRequestHandler<GainExperienceCommand, GainExperienceResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ILogger<GainExperienceHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GainExperienceHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mediator">The mediator for publishing events.</param>
    public GainExperienceHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ILogger<GainExperienceHandler> logger,
        IMediator mediator)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Handles the gain experience command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing experience and level information.</returns>
    public Task<GainExperienceResult> Handle(GainExperienceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new GainExperienceResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            if (request.ExperienceAmount <= 0)
            {
                return Task.FromResult(new GainExperienceResult
                {
                    Success = false,
                    ErrorMessage = "Experience amount must be positive"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new GainExperienceResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new GainExperienceResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Store old level
            var oldLevel = character.Level;

            // Award experience using character's built-in method
            character.GainExperience(request.ExperienceAmount);

            // Check if leveled up
            var leveledUp = character.Level > oldLevel;
            var newLevel = character.Level;

            // Calculate XP to next level
            var nextRequiredXP = character.Level * 100;
            var xpToNext = nextRequiredXP - character.Experience;

            _logger.LogInformation(
                "Character {CharacterName} gained {XP} experience from {Source}. Level: {Level}, Total XP: {TotalXP}",
                request.CharacterName,
                request.ExperienceAmount,
                request.Source ?? "Unknown",
                character.Level,
                character.Experience
            );

            // Publish level up event if applicable
            if (leveledUp)
            {
                _logger.LogInformation(
                    "Character {CharacterName} leveled up from {OldLevel} to {NewLevel}!",
                    request.CharacterName,
                    oldLevel,
                    newLevel
                );

                // Publish event (event class would need to be created)
                // await _mediator.Publish(new CharacterLeveledUpEvent(request.CharacterName, oldLevel, newLevel), cancellationToken);
            }

            return Task.FromResult(new GainExperienceResult
            {
                Success = true,
                NewExperience = character.Experience,
                CurrentLevel = character.Level,
                LeveledUp = leveledUp,
                NewLevel = leveledUp ? newLevel : null,
                ExperienceToNextLevel = xpToNext
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to award experience to character {CharacterName}", request.CharacterName);
            return Task.FromResult(new GainExperienceResult
            {
                Success = false,
                ErrorMessage = $"Failed to award experience: {ex.Message}"
            });
        }
    }
}
