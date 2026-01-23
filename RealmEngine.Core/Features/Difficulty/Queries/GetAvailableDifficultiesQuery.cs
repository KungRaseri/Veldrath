using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Queries;

/// <summary>
/// Query to get all available difficulty options.
/// </summary>
public record GetAvailableDifficultiesQuery : IRequest<GetAvailableDifficultiesResult>
{
}

/// <summary>
/// Result containing all available difficulty options.
/// </summary>
public record GetAvailableDifficultiesResult
{
    /// <summary>Gets the list of available difficulty settings.</summary>
    public required List<DifficultySettings> Difficulties { get; init; }
    
    /// <summary>Gets the name of the currently active difficulty.</summary>
    public string? CurrentDifficulty { get; init; }
}
