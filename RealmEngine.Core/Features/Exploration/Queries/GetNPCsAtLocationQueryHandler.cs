using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for GetNPCsAtLocationQuery.
/// Returns all NPCs at the player's current location with their traits.
/// </summary>
public class GetNPCsAtLocationQueryHandler : IRequestHandler<GetNPCsAtLocationQuery, GetNPCsAtLocationResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetNPCsAtLocationQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetNPCsAtLocationQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetNPCsAtLocationQueryHandler(
        ISaveGameService saveGameService,
        ILogger<GetNPCsAtLocationQueryHandler> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query to get NPCs at a location.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The NPCs at location result.</returns>
    public Task<GetNPCsAtLocationResult> Handle(GetNPCsAtLocationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new GetNPCsAtLocationResult(false, ErrorMessage: "No active game session"));
            }

            var locationName = request.LocationName ?? "Current Location"; // Note: SaveGame.DiscoveredLocations exists for tracking

            // Filter NPCs by location when a location is specified
            var npcsAtLocation = saveGame.KnownNPCs
                .Where(npc => request.LocationName == null
                    || npc.Location.Equals(request.LocationName, StringComparison.OrdinalIgnoreCase))
                .Select(npc => new NPCInfo(
                    Id: npc.Id,
                    Name: npc.Name,
                    Occupation: npc.Occupation,
                    IsFriendly: npc.IsFriendly,
                    IsMerchant: npc.Traits.ContainsKey("isMerchant") && npc.Traits["isMerchant"].AsBool(),
                    IsQuestGiver: npc.Traits.ContainsKey("isQuestGiver") && npc.Traits["isQuestGiver"].AsBool(),
                    IsTrainer: npc.Traits.ContainsKey("isTrainer") && npc.Traits["isTrainer"].AsBool(),
                    Dialogue: npc.Dialogue,
                    RelationshipValue: saveGame.NPCRelationships.TryGetValue(npc.Id, out var relationship) 
                        ? relationship 
                        : null
                ))
                .ToList();

            _logger.LogInformation("Retrieved {NpcCount} NPCs at location {Location}",
                npcsAtLocation.Count, locationName);

            return Task.FromResult(new GetNPCsAtLocationResult(
                Success: true,
                LocationName: locationName,
                NPCs: npcsAtLocation
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPCs at location {Location}", request.LocationName);
            return Task.FromResult(new GetNPCsAtLocationResult(
                false,
                ErrorMessage: $"Failed to get NPCs: {ex.Message}"
            ));
        }
    }
}
