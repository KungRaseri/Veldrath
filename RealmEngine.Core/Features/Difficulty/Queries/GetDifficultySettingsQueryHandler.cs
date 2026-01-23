using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Queries;

/// <summary>
/// Handler for GetDifficultySettingsQuery.
/// Returns the current difficulty settings for the active game.
/// </summary>
public class GetDifficultySettingsQueryHandler : IRequestHandler<GetDifficultySettingsQuery, DifficultySettings>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDifficultySettingsQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetDifficultySettingsQueryHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the get difficulty settings query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current difficulty settings.</returns>
    public Task<DifficultySettings> Handle(GetDifficultySettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = _saveGameService.GetDifficultySettings();
        return Task.FromResult(settings);
    }
}
