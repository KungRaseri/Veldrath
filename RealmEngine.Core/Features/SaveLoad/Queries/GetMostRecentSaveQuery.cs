using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SaveLoad.Queries;

/// <summary>
/// Query to get the most recent saved game.
/// </summary>
public record GetMostRecentSaveQuery : IRequest<GetMostRecentSaveResult>
{
}

/// <summary>
/// Result containing the most recent save game.
/// </summary>
public record GetMostRecentSaveResult
{
    /// <summary>Gets the most recent save game.</summary>
    public SaveGame? SaveGame { get; init; }
}
