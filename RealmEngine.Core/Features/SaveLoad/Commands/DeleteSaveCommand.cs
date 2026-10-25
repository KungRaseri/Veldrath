using MediatR;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Command to delete a saved game.
/// </summary>
public record DeleteSaveCommand : IRequest<DeleteSaveResult>
{
    /// <summary>Gets the save ID.</summary>
    public required string SaveId { get; init; }
}

/// <summary>
/// Result of delete save operation.
/// </summary>
public record DeleteSaveResult
{
    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public required bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
}
