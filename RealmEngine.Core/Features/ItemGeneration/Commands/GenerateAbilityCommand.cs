using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate powers using the PowerGenerator.
/// Supports category/subcategory selection and hydration options.
/// </summary>
public class GeneratePowerCommand : IRequest<GeneratePowerResult>
{
    /// <summary>
    /// The power category (e.g., "active", "passive", "reactive", "ultimate").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The power subcategory (e.g., "offensive", "defensive", "support", "utility").
    /// </summary>
    public string Subcategory { get; set; } = string.Empty;

    /// <summary>
    /// Specific power name to generate (optional).
    /// If provided, generates this specific power instead of random selection.
    /// </summary>
    public string? PowerName { get; set; }

    /// <summary>
    /// Number of powers to generate (default: 1).
    /// Ignored if PowerName is specified.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// If true, resolves all references and populates related properties (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;
}

/// <summary>
/// Result of power generation command.
/// </summary>
public class GeneratePowerResult
{
    /// <summary>
    /// True if generation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Generated power (for single generation or PowerName requests).
    /// </summary>
    public Power? Power { get; set; }

    /// <summary>
    /// Generated powers (for multiple generation requests).
    /// </summary>
    public List<Power> Powers { get; set; } = new List<Power>();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
