using MediatR;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Command to explore the current location.
/// </summary>
public record ExploreLocationCommand : IRequest<ExploreLocationResult>;

/// <summary>
/// Result of exploration.
/// </summary>
public record ExploreLocationResult(
    bool Success,
    bool CombatTriggered,
    int? ExperienceGained = null,
    int? GoldGained = null,
    string? ItemFound = null,
    string? ErrorMessage = null
);
