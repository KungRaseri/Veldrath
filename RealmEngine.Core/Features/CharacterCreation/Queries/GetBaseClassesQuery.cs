using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get all base character classes (excluding subclasses).
/// </summary>
public record GetBaseClassesQuery : IRequest<GetBaseClassesResult>
{
}

/// <summary>
/// Result containing all base classes.
/// </summary>
public record GetBaseClassesResult
{
    /// <summary>
    /// List of base character classes (non-subclasses).
    /// </summary>
    public List<CharacterClass> BaseClasses { get; init; } = new();
}
