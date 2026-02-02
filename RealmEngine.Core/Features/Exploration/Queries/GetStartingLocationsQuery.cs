using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to retrieve starting locations, optionally filtered by background recommendations
/// </summary>
public record GetStartingLocationsQuery(
    string? BackgroundId = null,
    bool FilterByRecommended = true
) : IRequest<List<Location>>;
