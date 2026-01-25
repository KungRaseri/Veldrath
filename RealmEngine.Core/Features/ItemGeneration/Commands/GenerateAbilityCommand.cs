using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate abilities using the AbilityGenerator.
/// Supports category/subcategory selection and hydration options.
/// </summary>
public class GenerateAbilityCommand : IRequest<GenerateAbilityResult>
{
    /// <summary>
    /// The ability category (e.g., "active", "passive", "reactive", "ultimate").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The ability subcategory (e.g., "offensive", "defensive", "support", "utility").
    /// </summary>
    public string Subcategory { get; set; } = string.Empty;

    /// <summary>
    /// Specific ability name to generate (optional).
    /// If provided, generates this specific ability instead of random selection.
    /// </summary>
    public string? AbilityName { get; set; }

    /// <summary>
    /// Number of abilities to generate (default: 1).
    /// Ignored if AbilityName is specified.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// If true, resolves all references and populates related properties (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;
}

/// <summary>
/// Result of ability generation command.
/// </summary>
public class GenerateAbilityResult
{
    /// <summary>
    /// True if generation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Generated ability (for single generation or AbilityName requests).
    /// </summary>
    public Ability? Ability { get; set; }

    /// <summary>
    /// Generated abilities (for multiple generation requests).
    /// </summary>
    public List<Ability> Abilities { get; set; } = new List<Ability>();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
