using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Handler for querying complete character progression information.
/// </summary>
public class GetCharacterProgressionHandler : IRequestHandler<GetCharacterProgressionQuery, GetCharacterProgressionResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly LevelUpService _levelUpService;
    private readonly ILogger<GetCharacterProgressionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCharacterProgressionHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="levelUpService">The level up domain service.</param>
    /// <param name="logger">The logger instance.</param>
    public GetCharacterProgressionHandler(
        ISaveGameService saveGameService,
        LevelUpService levelUpService,
        ILogger<GetCharacterProgressionHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _levelUpService = levelUpService ?? throw new ArgumentNullException(nameof(levelUpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the get character progression query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing complete progression details.</returns>
    public Task<GetCharacterProgressionResult> Handle(GetCharacterProgressionQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new GetCharacterProgressionResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new GetCharacterProgressionResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new GetCharacterProgressionResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Calculate XP to next level (100 XP per level)
            var nextLevelXP = character.Level * 100;
            var xpToNext = Math.Max(0, nextLevelXP - character.Experience);

            // Gather progression data
            var attributes = new Dictionary<string, int>
            {
                ["Strength"] = character.Strength,
                ["Dexterity"] = character.Dexterity,
                ["Constitution"] = character.Constitution,
                ["Intelligence"] = character.Intelligence,
                ["Wisdom"] = character.Wisdom,
                ["Charisma"] = character.Charisma
            };

            var result = new GetCharacterProgressionResult
            {
                Success = true,
                Level = character.Level,
                Experience = character.Experience,
                ExperienceToNextLevel = xpToNext,
                UnallocatedAttributePoints = character.UnspentAttributePoints,
                UnallocatedSkillPoints = character.UnspentSkillPoints,
                Attributes = attributes,
                Skills = character.Skills.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.CurrentRank),
                LearnedAbilities = character.LearnedAbilities.Keys.ToList(),
                LearnedSpells = character.LearnedSpells.Keys.ToList(),
                PlaytimeSeconds = 0, // Not tracked in Character model
                EnemiesDefeated = 0, // Not tracked in Character model
                QuestsCompleted = saveGame.CompletedQuests?.Count ?? 0
            };

            _logger.LogDebug(
                "Retrieved progression for {CharacterName}: Level {Level}, {XP} XP, {AbilityCount} abilities, {SpellCount} spells",
                request.CharacterName,
                character.Level,
                character.Experience,
                character.LearnedAbilities.Count,
                character.LearnedSpells.Count
            );

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get character progression for {CharacterName}", request.CharacterName);
            return Task.FromResult(new GetCharacterProgressionResult
            {
                Success = false,
                ErrorMessage = $"Failed to get character progression: {ex.Message}"
            });
        }
    }
}
