using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Quests.Queries;

/// <summary>
/// Query to get all completed quests.
/// </summary>
public record GetCompletedQuestsQuery : IRequest<List<Quest>>;

/// <summary>
/// Handles getting completed quests.
/// </summary>
public class GetCompletedQuestsHandler : IRequestHandler<GetCompletedQuestsQuery, List<Quest>>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCompletedQuestsHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetCompletedQuestsHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles getting completed quests.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of completed quests.</returns>
    public async Task<List<Quest>> Handle(GetCompletedQuestsQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        return await Task.FromResult(saveGame?.CompletedQuests ?? new List<Quest>());
    }
}
