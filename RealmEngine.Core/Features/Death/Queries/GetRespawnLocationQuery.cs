using MediatR;

namespace RealmEngine.Core.Features.Death.Queries;

/// <summary>
/// Query to get available respawn locations.
/// </summary>
public record GetRespawnLocationQuery : IRequest<GetRespawnLocationResult>
{
}

/// <summary>
/// Result containing respawn location information.
/// </summary>
public record GetRespawnLocationResult
{
    /// <summary>Gets the default respawn location.</summary>
    public required string DefaultLocation { get; init; }
    
    /// <summary>Gets the list of all available respawn points.</summary>
    public List<string> AvailableLocations { get; init; } = new();
    
    /// <summary>Gets a value indicating whether the player has unlocked custom respawn points.</summary>
    public bool HasCustomRespawnPoints { get; init; }
}
