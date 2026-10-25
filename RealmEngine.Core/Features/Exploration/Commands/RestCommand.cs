using MediatR;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Command to rest and recover health/mana.
/// </summary>
public record RestCommand : IRequest<RestResult>;

/// <summary>
/// Result of resting.
/// </summary>
public record RestResult(
    bool Success,
    int? HealthRecovered = null,
    int? ManaRecovered = null,
    string? ErrorMessage = null
);
