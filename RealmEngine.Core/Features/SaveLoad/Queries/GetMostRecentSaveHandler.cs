using MediatR;

namespace RealmEngine.Core.Features.SaveLoad.Queries;

/// <summary>
/// Handles the GetMostRecentSave query.
/// </summary>
public class GetMostRecentSaveHandler : IRequestHandler<GetMostRecentSaveQuery, GetMostRecentSaveResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMostRecentSaveHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetMostRecentSaveHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the GetMostRecentSaveQuery and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<GetMostRecentSaveResult> Handle(GetMostRecentSaveQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetMostRecentSave();

        return Task.FromResult(new GetMostRecentSaveResult
        {
            SaveGame = saveGame
        });
    }
}