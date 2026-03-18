using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Reputation.Queries;

/// <summary>
/// Query to get all faction reputations.
/// </summary>
public record GetReputationQuery : IRequest<GetReputationResult>
{
    /// <summary>
    /// Optional faction ID to get specific faction reputation.
    /// If null, returns all reputations.
    /// </summary>
    public string? FactionId { get; init; }
}

/// <summary>
/// Result of getting reputation.
/// </summary>
public record GetReputationResult
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
    /// All faction reputations.
    /// </summary>
    public List<FactionReputationInfo> Reputations { get; init; } = new();
}

/// <summary>
/// Information about a faction reputation.
/// </summary>
public record FactionReputationInfo
{
    /// <summary>
    /// Faction ID.
    /// </summary>
    public string FactionId { get; init; } = string.Empty;

    /// <summary>
    /// Reputation level.
    /// </summary>
    public ReputationLevel Level { get; init; }

    /// <summary>
    /// Current reputation points.
    /// </summary>
    public int Points { get; init; }

    /// <summary>
    /// Whether the player can trade with this faction.
    /// </summary>
    public bool CanTrade { get; init; }

    /// <summary>
    /// Whether the player can accept quests from this faction.
    /// </summary>
    public bool CanAcceptQuests { get; init; }

    /// <summary>
    /// Whether this faction is hostile.
    /// </summary>
    public bool IsHostile { get; init; }

    /// <summary>
    /// Price discount percentage (0.0 to 0.30).
    /// </summary>
    public double PriceDiscount { get; init; }
}

/// <summary>
/// Handler for GetReputationQuery.
/// </summary>
public class GetReputationHandler : IRequestHandler<GetReputationQuery, GetReputationResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ReputationService _reputationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetReputationHandler"/> class.
    /// </summary>
    public GetReputationHandler(ISaveGameService saveGameService, ReputationService reputationService)
    {
        _saveGameService = saveGameService;
        _reputationService = reputationService;
    }

    /// <summary>
    /// Handles the get reputation query.
    /// </summary>
    public Task<GetReputationResult> Handle(GetReputationQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();

        if (saveGame == null)
        {
            return Task.FromResult(new GetReputationResult
            {
                Success = false,
                Message = "No active game found."
            });
        }

        List<FactionReputationInfo> reputations;

        if (!string.IsNullOrWhiteSpace(request.FactionId))
        {
            // Get specific faction
            var standing = _reputationService.GetOrCreateReputation(saveGame, request.FactionId);
            reputations = new List<FactionReputationInfo>
            {
                MapToInfo(standing)
            };
        }
        else
        {
            // Get all factions
            var allReputations = _reputationService.GetAllReputations(saveGame);
            reputations = allReputations.Values.Select(MapToInfo).ToList();
        }

        return Task.FromResult(new GetReputationResult
        {
            Success = true,
            Message = reputations.Count > 0 
                ? $"Retrieved {reputations.Count} faction reputation(s)." 
                : "No faction reputations found.",
            Reputations = reputations
        });
    }

    private static FactionReputationInfo MapToInfo(ReputationStanding standing)
    {
        return new FactionReputationInfo
        {
            FactionId = standing.FactionId,
            Level = standing.Level,
            Points = standing.ReputationPoints,
            CanTrade = standing.CanTrade,
            CanAcceptQuests = standing.CanAcceptQuests,
            IsHostile = standing.IsHostile,
            PriceDiscount = standing.GetPriceDiscount()
        };
    }
}
