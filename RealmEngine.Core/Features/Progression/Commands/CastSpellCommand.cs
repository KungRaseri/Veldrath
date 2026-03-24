using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to cast a spell.
/// </summary>
public record CastSpellCommand : IRequest<CastSpellResult>
{
    /// <summary>Gets the character casting the spell.</summary>
    public required Character Caster { get; init; }
    /// <summary>Gets the spell ID to cast.</summary>
    public required string SpellId { get; init; }
    /// <summary>Gets the target character, if any.</summary>
    public Character? Target { get; init; }
}

/// <summary>
/// Result of casting a spell.
/// </summary>
public record CastSpellResult
{
    /// <summary>Gets a value indicating whether the spell cast succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the mana cost paid.</summary>
    public int ManaCostPaid { get; init; }
    /// <summary>Gets the effect value as a string.</summary>
    public string EffectValue { get; init; } = string.Empty;
    /// <summary>Gets a value indicating whether the spell fizzled.</summary>
    public bool WasFizzle { get; init; }
    /// <summary>Gets the spell (power) that was cast.</summary>
    public Power? SpellCast { get; init; }
}
