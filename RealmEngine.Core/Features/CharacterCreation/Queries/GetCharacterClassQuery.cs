using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get a character class by name.
/// </summary>
public record GetCharacterClassQuery : IRequest<GetCharacterClassResult>
{
    /// <summary>
    /// Gets the name of the character class to retrieve.
    /// </summary>
    public required string ClassName { get; init; }
}

/// <summary>
/// Result containing character class details.
/// </summary>
public record GetCharacterClassResult
{
    /// <summary>
    /// Gets a value indicating whether the character class was found.
    /// </summary>
    public required bool Found { get; init; }

    /// <summary>
    /// Gets the character class, or null if not found.
    /// </summary>
    public CharacterClass? CharacterClass { get; init; }
}
