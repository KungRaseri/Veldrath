using RealmEngine.Core.Features.SaveLoad;
using MediatR;

namespace RealmEngine.Core.Features.Death.Queries;

/// <summary>
/// Handles retrieval of dropped items at a location.
/// </summary>
public class GetDroppedItemsHandler : IRequestHandler<GetDroppedItemsQuery, GetDroppedItemsResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDroppedItemsHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetDroppedItemsHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the query to retrieve dropped items at a location.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing dropped items.</returns>
    public Task<GetDroppedItemsResult> Handle(GetDroppedItemsQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
        {
            return Task.FromResult(new GetDroppedItemsResult());
        }

        if (saveGame.DroppedItemsAtLocations.TryGetValue(request.Location, out var items))
        {
            return Task.FromResult(new GetDroppedItemsResult { Items = items });
        }

        return Task.FromResult(new GetDroppedItemsResult());
    }
}