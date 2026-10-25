using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Quests.Queries;

/// <summary>
/// Query to get all available quests (not yet accepted).
/// </summary>
public record GetAvailableQuestsQuery : IRequest<List<Quest>>;

/// <summary>
/// Handles getting available quests.
/// </summary>
public class GetAvailableQuestsHandler : IRequestHandler<GetAvailableQuestsQuery, List<Quest>>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableQuestsHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetAvailableQuestsHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles getting available quests.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of available quests.</returns>
    public async Task<List<Quest>> Handle(GetAvailableQuestsQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        return await Task.FromResult(saveGame?.AvailableQuests ?? new List<Quest>());
    }
}
