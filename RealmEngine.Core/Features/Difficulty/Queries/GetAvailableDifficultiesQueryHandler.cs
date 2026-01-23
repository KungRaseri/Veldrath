using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Queries;

/// <summary>
/// Handler for GetAvailableDifficultiesQuery.
/// Returns all available difficulty options.
/// </summary>
public class GetAvailableDifficultiesQueryHandler : IRequestHandler<GetAvailableDifficultiesQuery, GetAvailableDifficultiesResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableDifficultiesQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetAvailableDifficultiesQueryHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the get available difficulties query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All available difficulty options with current selection.</returns>
    public Task<GetAvailableDifficultiesResult> Handle(GetAvailableDifficultiesQuery request, CancellationToken cancellationToken)
    {
        var allDifficulties = DifficultySettings.GetAll();
        
        // Get current difficulty if there's an active game
        var saveGame = _saveGameService.GetCurrentSave();
        var currentDifficulty = saveGame?.DifficultyLevel ?? "Normal";

        return Task.FromResult(new GetAvailableDifficultiesResult
        {
            Difficulties = allDifficulties,
            CurrentDifficulty = currentDifficulty
        });
    }
}
