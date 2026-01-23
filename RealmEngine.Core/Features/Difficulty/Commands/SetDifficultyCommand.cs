using MediatR;

namespace RealmEngine.Core.Features.Difficulty.Commands;

/// <summary>
/// Command to set the difficulty level for the current game.
/// </summary>
public record SetDifficultyCommand : IRequest<SetDifficultyResult>
{
    /// <summary>Gets the difficulty name to set.</summary>
    public required string DifficultyName { get; init; }
}

/// <summary>
/// Result of setting difficulty.
/// </summary>
public record SetDifficultyResult
{
    /// <summary>Gets a value indicating whether the operation was successful.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Gets the error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Gets the difficulty name that was set.</summary>
    public string? DifficultyName { get; init; }
    
    /// <summary>Gets a value indicating whether apocalypse mode was enabled.</summary>
    public bool ApocalypseModeEnabled { get; init; }
    
    /// <summary>Gets the time limit in minutes if apocalypse mode was enabled.</summary>
    public int? ApocalypseTimeLimitMinutes { get; init; }
}
