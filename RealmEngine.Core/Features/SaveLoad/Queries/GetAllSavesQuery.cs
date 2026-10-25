using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SaveLoad.Queries;

/// <summary>
/// Query to get all saved games.
/// </summary>
public record GetAllSavesQuery : IRequest<GetAllSavesResult>
{
}

/// <summary>
/// Result containing all saved games.
/// </summary>
public record GetAllSavesResult
{
    /// <summary>Gets the list of all saved games.</summary>
    public required List<SaveGame> SaveGames { get; init; }
}
