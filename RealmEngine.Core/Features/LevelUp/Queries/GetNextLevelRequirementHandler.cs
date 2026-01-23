using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Handler for querying next level experience requirements.
/// </summary>
public class GetNextLevelRequirementHandler : IRequestHandler<GetNextLevelRequirementQuery, GetNextLevelRequirementResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ILogger<GetNextLevelRequirementHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetNextLevelRequirementHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="logger">The logger instance.</param>
    public GetNextLevelRequirementHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ILogger<GetNextLevelRequirementHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the get next level requirement query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing next level requirements.</returns>
    public Task<GetNextLevelRequirementResult> Handle(GetNextLevelRequirementQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new GetNextLevelRequirementResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new GetNextLevelRequirementResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new GetNextLevelRequirementResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Calculate requirements directly (100 XP per level)
            var nextLevel = character.Level + 1;
            var requiredXP = character.Level * 100;
            var remaining = Math.Max(0, requiredXP - character.Experience);
            var progress = requiredXP > 0 
                ? ((double)character.Experience / requiredXP) * 100.0 
                : 100.0;

            _logger.LogDebug(
                "Character {CharacterName} level {Level}: {CurrentXP}/{RequiredXP} XP ({Progress:F1}%)",
                request.CharacterName,
                character.Level,
                character.Experience,
                requiredXP,
                progress
            );

            return Task.FromResult(new GetNextLevelRequirementResult
            {
                Success = true,
                CurrentLevel = character.Level,
                CurrentExperience = character.Experience,
                RequiredExperience = requiredXP,
                RemainingExperience = remaining,
                ProgressPercentage = Math.Min(100.0, progress)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next level requirement for character {CharacterName}", request.CharacterName);
            return Task.FromResult(new GetNextLevelRequirementResult
            {
                Success = false,
                ErrorMessage = $"Failed to get level requirements: {ex.Message}"
            });
        }
    }
}
