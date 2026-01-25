using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get all available character classes.
/// </summary>
public class GetAvailableClassesQuery : IRequest<GetAvailableClassesResult>
{
    /// <summary>
    /// If true, resolves all references and populates related properties (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;

    /// <summary>
    /// Optional category filter (e.g., "warriors", "rogues", "mages", "priests").
    /// If null, returns all classes from all categories.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Result containing available character classes.
/// </summary>
public class GetAvailableClassesResult
{
    /// <summary>
    /// True if query succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// List of available character classes.
    /// </summary>
    public List<CharacterClass> Classes { get; set; } = new List<CharacterClass>();

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
