using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get all available character classes.
/// </summary>
public record GetCharacterClassesQuery : IRequest<GetCharacterClassesResult>
{
}

/// <summary>
/// Result containing available character classes.
/// </summary>
public record GetCharacterClassesResult
{
    /// <summary>
    /// Gets the list of available character classes.
    /// </summary>
    public required List<CharacterClass> Classes { get; init; }
}
