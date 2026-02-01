using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get all character classes of a specific type/category (e.g., "warrior", "mage", "cleric").
/// </summary>
public record GetClassesByTypeQuery : IRequest<GetClassesByTypeResult>
{
    /// <summary>
    /// The class type/category to filter by (e.g., "warrior", "mage", "cleric", "rogue").
    /// </summary>
    public string ClassType { get; init; } = string.Empty;
}

/// <summary>
/// Result containing all classes of the specified type.
/// </summary>
public record GetClassesByTypeResult
{
    /// <summary>
    /// The class type that was queried.
    /// </summary>
    public string ClassType { get; init; } = string.Empty;

    /// <summary>
    /// List of character classes of the specified type.
    /// </summary>
    public List<CharacterClass> Classes { get; init; } = new();

    /// <summary>
    /// Whether any classes were found for this type.
    /// </summary>
    public bool Found => Classes.Any();
}
