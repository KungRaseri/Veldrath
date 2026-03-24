using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Command to initialize starting powers for a new character based on their class.
/// </summary>
public record InitializeStartingPowersCommand : IRequest<InitializeStartingPowersResult>
{
    /// <summary>
    /// Gets the character to initialize powers for.
    /// </summary>
    public required Character Character { get; init; }

    /// <summary>
    /// Gets the name of the character class.
    /// </summary>
    public required string ClassName { get; init; }
}

/// <summary>
/// Result of initializing starting powers.
/// </summary>
public record InitializeStartingPowersResult
{
    /// <summary>
    /// Gets the number of powers learned.
    /// </summary>
    public int PowersLearned { get; init; }

    /// <summary>
    /// Gets the list of power IDs that were learned.
    /// </summary>
    public List<string> PowerIds { get; init; } = new();
    /// <summary>
    /// Gets a value indicating whether the initialization was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Gets a message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
