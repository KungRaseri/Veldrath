using MediatR;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Command to rest at an inn in the current location.
/// </summary>
public record RestAtInnCommand(string LocationId, string CharacterName, int Cost = 10) : IRequest<RestAtInnResult>;

/// <summary>
/// Result of resting at an inn.
/// </summary>
public record RestAtInnResult
{
    /// <summary>Gets whether the rest was successful.</summary>
    public bool Success { get; init; }
    
    /// <summary>Gets the amount of health recovered.</summary>
    public int HealthRecovered { get; init; }
    
    /// <summary>Gets the amount of mana recovered.</summary>
    public int ManaRecovered { get; init; }
    
    /// <summary>Gets the gold cost paid.</summary>
    public int GoldPaid { get; init; }
    
    /// <summary>Gets whether the game was saved.</summary>
    public bool GameSaved { get; init; }
    
    /// <summary>Gets any buffs applied.</summary>
    public List<string> BuffsApplied { get; init; } = new();
    
    /// <summary>Gets an error message if the rest failed.</summary>
    public string? ErrorMessage { get; init; }
}
