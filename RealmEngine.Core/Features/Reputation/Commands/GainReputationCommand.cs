using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Reputation.Commands;

/// <summary>
/// Command to gain reputation with a faction.
/// </summary>
public record GainReputationCommand : IRequest<GainReputationResult>
{
    /// <summary>
    /// Faction ID to gain reputation with.
    /// </summary>
    public string FactionId { get; init; } = string.Empty;

    /// <summary>
    /// Amount of reputation to gain.
    /// </summary>
    public int Amount { get; init; }

    /// <summary>
    /// Reason for gaining reputation (optional for logging).
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Result of gaining reputation.
/// </summary>
public record GainReputationResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// New reputation level.
    /// </summary>
    public ReputationLevel NewLevel { get; init; }

    /// <summary>
    /// Previous reputation level.
    /// </summary>
    public ReputationLevel OldLevel { get; init; }

    /// <summary>
    /// Current reputation points.
    /// </summary>
    public int CurrentPoints { get; init; }

    /// <summary>
    /// Whether the reputation level changed.
    /// </summary>
    public bool LevelChanged => NewLevel != OldLevel;
}

/// <summary>
/// Handler for GainReputationCommand.
/// </summary>
public class GainReputationHandler : IRequestHandler<GainReputationCommand, GainReputationResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ReputationService _reputationService;
    private readonly ILogger<GainReputationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GainReputationHandler"/> class.
    /// </summary>
    public GainReputationHandler(ISaveGameService saveGameService, ReputationService reputationService, ILogger<GainReputationHandler> logger)
    {
        _saveGameService = saveGameService;
        _reputationService = reputationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the gain reputation command.
    /// </summary>
    public Task<GainReputationResult> Handle(GainReputationCommand request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();

        if (saveGame == null)
        {
            return Task.FromResult(new GainReputationResult
            {
                Success = false,
                Message = "No active game found."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FactionId))
        {
            return Task.FromResult(new GainReputationResult
            {
                Success = false,
                Message = "Faction ID is required."
            });
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(new GainReputationResult
            {
                Success = false,
                Message = "Amount must be greater than zero."
            });
        }

        var oldLevel = _reputationService.GetReputationLevel(saveGame, request.FactionId);
        var standing = _reputationService.GainReputation(saveGame, request.FactionId, request.Amount);
        var newLevel = standing.Level;

        _saveGameService.SaveGame(saveGame);

        var reasonText = !string.IsNullOrWhiteSpace(request.Reason) ? $" ({request.Reason})" : "";
        var message = newLevel != oldLevel
            ? $"Your reputation with the faction has increased to {newLevel}{reasonText}!"
            : $"You gained {request.Amount} reputation with the faction{reasonText}.";

        _logger.LogInformation("Player gained {Amount} reputation with {FactionId}: {OldLevel} -> {NewLevel}",
            request.Amount, request.FactionId, oldLevel, newLevel);

        return Task.FromResult(new GainReputationResult
        {
            Success = true,
            Message = message,
            OldLevel = oldLevel,
            NewLevel = newLevel,
            CurrentPoints = standing.ReputationPoints
        });
    }
}
