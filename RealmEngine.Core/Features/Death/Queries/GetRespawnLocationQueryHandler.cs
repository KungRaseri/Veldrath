using MediatR;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Death.Queries;

/// <summary>
/// Handler for GetRespawnLocationQuery.
/// Returns available respawn locations for the player.
/// </summary>
public class GetRespawnLocationQueryHandler : IRequestHandler<GetRespawnLocationQuery, GetRespawnLocationResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRespawnLocationQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetRespawnLocationQueryHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the get respawn location query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Available respawn locations.</returns>
    public Task<GetRespawnLocationResult> Handle(GetRespawnLocationQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        
        var availableLocations = new List<string> { "Hub Town" };
        
        // Add discovered towns/safe zones as respawn points
        if (saveGame != null)
        {
            var safeTowns = saveGame.DiscoveredLocations
                .Where(loc => loc.Contains("Town") || loc.Contains("Village") || loc.Contains("Sanctuary"))
                .ToList();
            
            availableLocations.AddRange(safeTowns);
        }

        return Task.FromResult(new GetRespawnLocationResult
        {
            DefaultLocation = "Hub Town",
            AvailableLocations = availableLocations.Distinct().ToList(),
            HasCustomRespawnPoints = availableLocations.Count > 1
        });
    }
}
