using MediatR;

namespace RealmEngine.Core.Features.SaveLoad.Queries;

/// <summary>
/// Handles the GetAllSaves query.
/// </summary>
public class GetAllSavesHandler : IRequestHandler<GetAllSavesQuery, GetAllSavesResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllSavesHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetAllSavesHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the GetAllSavesQuery and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<GetAllSavesResult> Handle(GetAllSavesQuery request, CancellationToken cancellationToken)
    {
        var saveGames = _saveGameService.GetAllSaves();

        return Task.FromResult(new GetAllSavesResult
        {
            SaveGames = saveGames
        });
    }
}