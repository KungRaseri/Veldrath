using MediatR;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to get all known locations.
/// </summary>
public record GetKnownLocationsQuery : IRequest<GetKnownLocationsResult>;

/// <summary>
/// Result containing list of known locations.
/// </summary>
public record GetKnownLocationsResult(
    bool Success,
    IReadOnlyList<string>? Locations = null,
    string? ErrorMessage = null
);
