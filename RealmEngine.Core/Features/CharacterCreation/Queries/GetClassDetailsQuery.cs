using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Query to get detailed information about a specific character class.
/// </summary>
public class GetClassDetailsQuery : IRequest<GetClassDetailsResult>
{
    /// <summary>
    /// The class slug or name to lookup.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Optional category hint for faster lookup (e.g., "warriors", "mages").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// If true, resolves all references and populates related properties (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;
}

/// <summary>
/// Result containing character class details.
/// </summary>
public class GetClassDetailsResult
{
    /// <summary>
    /// True if query succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The character class details.
    /// </summary>
    public CharacterClass? Class { get; set; }

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
