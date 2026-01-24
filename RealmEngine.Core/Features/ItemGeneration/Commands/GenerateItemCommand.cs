using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate an item using the modern item generator.
/// </summary>
public record GenerateItemCommand : IRequest<GenerateItemResult>
{
    /// <summary>Gets the item category (e.g., "weapons/swords", "armor/chest").</summary>
    public required string Category { get; init; }
    
    /// <summary>Gets the optional budget request for budget-based generation.</summary>
    public Services.Budget.BudgetItemRequest? BudgetRequest { get; init; }
    
    /// <summary>Gets a value indicating whether to hydrate references (default: true).</summary>
    public bool Hydrate { get; init; } = true;
}

/// <summary>
/// Result of item generation.
/// </summary>
public record GenerateItemResult
{
    /// <summary>Gets a value indicating whether generation was successful.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Gets the generated item.</summary>
    public Item? Item { get; init; }
    
    /// <summary>Gets the error message if generation failed.</summary>
    public string? ErrorMessage { get; init; }
}
