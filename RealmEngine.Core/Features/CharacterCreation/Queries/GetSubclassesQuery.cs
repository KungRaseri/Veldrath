using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get subclasses, optionally filtered by parent class.
/// </summary>
public record GetSubclassesQuery : IRequest<GetSubclassesResult>
{
    /// <summary>
    /// Optional parent class ID to filter subclasses (e.g., "cleric:Priest").
    /// If null, returns all subclasses.
    /// </summary>
    public string? ParentClassId { get; init; }
}

/// <summary>
/// Result containing subclasses.
/// </summary>
public record GetSubclassesResult
{
    /// <summary>
    /// The parent class ID filter used (if any).
    /// </summary>
    public string? ParentClassId { get; init; }

    /// <summary>
    /// List of subclasses.
    /// </summary>
    public List<CharacterClass> Subclasses { get; init; } = new();
}
