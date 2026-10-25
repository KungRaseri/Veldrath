using MediatR;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to get the current location.
/// </summary>
public record GetCurrentLocationQuery : IRequest<GetCurrentLocationResult>;

/// <summary>
/// Result containing current location.
/// </summary>
public record GetCurrentLocationResult(
    bool Success,
    string? CurrentLocation = null,
    string? ErrorMessage = null
);
