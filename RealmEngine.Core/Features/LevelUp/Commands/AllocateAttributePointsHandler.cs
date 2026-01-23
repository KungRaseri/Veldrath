using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Handler for allocating attribute points to character attributes.
/// </summary>
public class AllocateAttributePointsHandler : IRequestHandler<AllocateAttributePointsCommand, AllocateAttributePointsResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<AllocateAttributePointsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocateAttributePointsHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger instance.</param>
    public AllocateAttributePointsHandler(
        ISaveGameService saveGameService,
        ILogger<AllocateAttributePointsHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the allocate attribute points command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing allocation details.</returns>
    public Task<AllocateAttributePointsResult> Handle(AllocateAttributePointsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = "Character name is required"
                });
            }

            if (request.AttributeAllocations == null || request.AttributeAllocations.Count == 0)
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = "Attribute allocations are required"
                });
            }

            // Get current save
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            var character = saveGame.Character;
            if (character.Name != request.CharacterName)
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = $"Character '{request.CharacterName}' not found"
                });
            }

            // Calculate total points to spend
            var totalPointsToSpend = request.AttributeAllocations.Values.Sum();
            
            // Validate non-negative allocations
            if (request.AttributeAllocations.Any(kvp => kvp.Value < 0))
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = "Cannot allocate negative attribute points"
                });
            }

            // Check if character has enough unspent points
            if (character.UnspentAttributePoints < totalPointsToSpend)
            {
                return Task.FromResult(new AllocateAttributePointsResult
                {
                    Success = false,
                    ErrorMessage = $"Insufficient attribute points. Have: {character.UnspentAttributePoints}, Need: {totalPointsToSpend}"
                });
            }

            // Allocate points to attributes
            foreach (var (attributeName, points) in request.AttributeAllocations)
            {
                switch (attributeName.ToLower())
                {
                    case "strength":
                        character.Strength += points;
                        break;
                    case "dexterity":
                        character.Dexterity += points;
                        break;
                    case "constitution":
                        character.Constitution += points;
                        break;
                    case "intelligence":
                        character.Intelligence += points;
                        break;
                    case "wisdom":
                        character.Wisdom += points;
                        break;
                    case "charisma":
                        character.Charisma += points;
                        break;
                    default:
                        _logger.LogWarning(
                            "Unknown attribute '{AttributeName}' for character {CharacterName}",
                            attributeName,
                            request.CharacterName
                        );
                        break;
                }
            }

            // Deduct spent points
            character.UnspentAttributePoints -= totalPointsToSpend;

            _logger.LogInformation(
                "Character {CharacterName} allocated {Points} attribute points. Remaining: {Remaining}",
                request.CharacterName,
                totalPointsToSpend,
                character.UnspentAttributePoints
            );

            var currentAttributes = new Dictionary<string, int>
            {
                ["Strength"] = character.Strength,
                ["Dexterity"] = character.Dexterity,
                ["Constitution"] = character.Constitution,
                ["Intelligence"] = character.Intelligence,
                ["Wisdom"] = character.Wisdom,
                ["Charisma"] = character.Charisma
            };

            return Task.FromResult(new AllocateAttributePointsResult
            {
                Success = true,
                PointsSpent = totalPointsToSpend,
                RemainingPoints = character.UnspentAttributePoints,
                NewAttributeValues = currentAttributes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate attribute points for character {CharacterName}", request.CharacterName);
            return Task.FromResult(new AllocateAttributePointsResult
            {
                Success = false,
                ErrorMessage = $"Failed to allocate attribute points: {ex.Message}"
            });
        }
    }
}
