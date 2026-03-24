using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to learn a spell from a spellbook.
/// </summary>
public record LearnSpellCommand : IRequest<LearnSpellResult>
{
    /// <summary>Gets the character learning the spell.</summary>
    public required Character Character { get; init; }
    /// <summary>Gets the spell ID to learn.</summary>
    public required string SpellId { get; init; }
}

/// <summary>
/// Result of learning a spell.
/// </summary>
public record LearnSpellResult
{
    /// <summary>Gets a value indicating whether the spell was learned successfully.</summary>
    public bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the spell (power) that was learned.</summary>
    public Power? SpellLearned { get; init; }
}
