using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Command to load a saved game.
/// </summary>
public record LoadGameCommand : IRequest<LoadGameResult>
{
    /// <summary>Gets the save ID.</summary>
    public required string SaveId { get; init; }
}

/// <summary>
/// Result of load game operation.
/// </summary>
public record LoadGameResult
{
    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public required bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the loaded save game.</summary>
    public SaveGame? SaveGame { get; init; }
}
