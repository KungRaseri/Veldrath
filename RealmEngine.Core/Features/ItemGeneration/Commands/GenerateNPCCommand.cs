using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate an NPC using the modern NPC generator.
/// </summary>
public record GenerateNPCCommand : IRequest<GenerateNPCResult>
{
    /// <summary>Gets the NPC category (e.g., "merchants", "guards", "quest-givers").</summary>
    public required string Category { get; init; }
    
    /// <summary>Gets a value indicating whether to hydrate references (default: true).</summary>
    public bool Hydrate { get; init; } = true;
}

/// <summary>
/// Result of NPC generation.
/// </summary>
public record GenerateNPCResult
{
    /// <summary>Gets a value indicating whether generation was successful.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Gets the generated NPC.</summary>
    public NPC? NPC { get; init; }
    
    /// <summary>Gets the error message if generation failed.</summary>
    public string? ErrorMessage { get; init; }
}
