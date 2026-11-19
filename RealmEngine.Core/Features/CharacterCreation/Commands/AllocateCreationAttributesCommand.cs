using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Allocates point-buy attribute values for an in-progress creation session.
/// </summary>
public record AllocateCreationAttributesCommand(
    Guid SessionId,
    Dictionary<string, int> Allocations) : IRequest<AllocateCreationAttributesResult>;

/// <summary>
/// Result of <see cref="AllocateCreationAttributesCommand"/>.
/// </summary>
public record AllocateCreationAttributesResult
{
    /// <summary>Gets a value indicating whether the allocation was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets a message describing the result.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the remaining point budget after this allocation (only meaningful when Success is true).</summary>
    public int RemainingPoints { get; init; }
}

/// <summary>
/// Handles <see cref="AllocateCreationAttributesCommand"/>.
/// </summary>
public class AllocateCreationAttributesHandler(
    ICharacterCreationSessionStore sessionStore,
    ILogger<AllocateCreationAttributesHandler> logger)
    : IRequestHandler<AllocateCreationAttributesCommand, AllocateCreationAttributesResult>
{
    private static readonly PointBuyConfig Config = new();

    /// <inheritdoc />
    public async Task<AllocateCreationAttributesResult> Handle(AllocateCreationAttributesCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new AllocateCreationAttributesResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new AllocateCreationAttributesResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        string[] requiredStats = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];
        var missing = requiredStats.Where(s => !request.Allocations.ContainsKey(s)).ToList();
        if (missing.Count > 0)
            return new AllocateCreationAttributesResult { Success = false, Message = $"Allocations must include all 6 stats. Missing: {string.Join(", ", missing)}." };

        if (!Config.IsValid(request.Allocations))
        {
            int cost = Config.CalculateTotalCost(request.Allocations);
            string reason = cost > Config.TotalPoints
                ? $"Total cost {cost} exceeds budget of {Config.TotalPoints}."
                : "One or more stat values are outside the allowed range (8–15).";

            logger.LogDebug("Session {SessionId}: invalid attribute allocation — {Reason}", request.SessionId, reason);
            return new AllocateCreationAttributesResult { Success = false, Message = reason };
        }

        // The budget is a constraint, not a suggestion: a partial allocation was accepted here, stored,
        // and then handed to CreateCharacterCommand unchanged, so a client that skipped the step created a
        // character at the minimum stat line with its points unused. "All stat points must be allocated"
        // is the rule, and it has to hold for the API and not only for the wizard.
        int spent = Config.CalculateTotalCost(request.Allocations);
        int remaining = Config.TotalPoints - spent;
        if (remaining > 0)
        {
            string reason = $"All attribute points must be spent before continuing; {remaining} of {Config.TotalPoints} remain unspent.";
            logger.LogDebug("Session {SessionId}: partial attribute allocation refused — {Reason}", request.SessionId, reason);
            return new AllocateCreationAttributesResult { Success = false, Message = reason, RemainingPoints = remaining };
        }

        session.AttributeAllocations = new Dictionary<string, int>(request.Allocations);
        await sessionStore.UpdateSessionAsync(session);

        logger.LogDebug("Session {SessionId}: attributes allocated, {Remaining} points remaining", request.SessionId, remaining);
        return new AllocateCreationAttributesResult { Success = true, Message = "Attributes allocated.", RemainingPoints = remaining };
    }
}
