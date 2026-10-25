using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Command to initialize starting abilities for a new character based on their class.
/// </summary>
public record InitializeStartingAbilitiesCommand : IRequest<InitializeStartingAbilitiesResult>
{
    /// <summary>
    /// Gets the character to initialize abilities for.
    /// </summary>
    public required Character Character { get; init; }

    /// <summary>
    /// Gets the name of the character class.
    /// </summary>
    public required string ClassName { get; init; }
}

/// <summary>
/// Result of initializing starting abilities.
/// </summary>
public record InitializeStartingAbilitiesResult
{
    /// <summary>
    /// Gets the number of abilities learned.
    /// </summary>
    public int AbilitiesLearned { get; init; }

    /// <summary>
    /// Gets the list of ability IDs that were learned.
    /// </summary>
    public List<string> AbilityIds { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether the initialization was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
