using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Command to save the current game state.
/// </summary>
public record SaveGameCommand : IRequest<SaveGameResult>
{
    /// <summary>Gets the player character.</summary>
    public required Character Player { get; init; }
    /// <summary>Gets the inventory items.</summary>
    public required List<Item> Inventory { get; init; }
    /// <summary>Gets the save ID.</summary>
    public string? SaveId { get; init; }
}

/// <summary>
/// Result of save game operation.
/// </summary>
public record SaveGameResult
{
    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public required bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the save ID.</summary>
    public string? SaveId { get; init; }
}
