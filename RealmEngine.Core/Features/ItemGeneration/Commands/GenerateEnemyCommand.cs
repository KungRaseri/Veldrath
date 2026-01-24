using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate an enemy using the modern enemy generator.
/// </summary>
public record GenerateEnemyCommand : IRequest<GenerateEnemyResult>
{
    /// <summary>Gets the enemy category (e.g., "beasts", "undead", "dragons").</summary>
    public required string Category { get; init; }
    
    /// <summary>Gets a value indicating whether to hydrate references (default: true).</summary>
    public bool Hydrate { get; init; } = true;
    
    /// <summary>Gets the optional level for the enemy.</summary>
    public int? Level { get; init; }
}

/// <summary>
/// Result of enemy generation.
/// </summary>
public record GenerateEnemyResult
{
    /// <summary>Gets a value indicating whether generation was successful.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Gets the generated enemy.</summary>
    public Enemy? Enemy { get; init; }
    
    /// <summary>Gets the error message if generation failed.</summary>
    public string? ErrorMessage { get; init; }
}
