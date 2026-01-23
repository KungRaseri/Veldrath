using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Death.Commands;

/// <summary>
/// Command to respawn the player after death.
/// </summary>
public record RespawnCommand : IRequest<RespawnResult>
{
    /// <summary>Gets the player character to respawn.</summary>
    public required Character Player { get; init; }
    
    /// <summary>Gets the optional respawn location (defaults to hub town).</summary>
    public string? RespawnLocation { get; init; }
}

/// <summary>
/// Result of respawning the player.
/// </summary>
public record RespawnResult
{
    /// <summary>Gets a value indicating whether the respawn was successful.</summary>
    public required bool Success { get; init; }
    
    /// <summary>Gets the error message if respawn failed.</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Gets the location where the player respawned.</summary>
    public string? RespawnLocation { get; init; }
    
    /// <summary>Gets the player's health after respawn.</summary>
    public int Health { get; init; }
    
    /// <summary>Gets the player's mana after respawn.</summary>
    public int Mana { get; init; }
}
