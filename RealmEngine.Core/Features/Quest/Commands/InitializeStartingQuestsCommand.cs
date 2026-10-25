using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Quests.Commands;

/// <summary>
/// Command to initialize the starting quests when creating a new game.
/// </summary>
public record InitializeStartingQuestsCommand(SaveGame SaveGame) : IRequest<InitializeStartingQuestsResult>;

/// <summary>
/// Result of initializing starting quests.
/// </summary>
public record InitializeStartingQuestsResult(bool Success, int QuestsInitialized);

/// <summary>
/// Handles initializing starting quests.
/// </summary>
public class InitializeStartingQuestsHandler : IRequestHandler<InitializeStartingQuestsCommand, InitializeStartingQuestsResult>
{
    private readonly Services.QuestInitializationService _initService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeStartingQuestsHandler"/> class.
    /// </summary>
    /// <param name="initService">The quest initialization service.</param>
    public InitializeStartingQuestsHandler(Services.QuestInitializationService initService)
    {
        _initService = initService;
    }

    /// <summary>
    /// Handles initializing starting quests.
    /// </summary>
    /// <param name="request">The initialize command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The initialization result.</returns>
    public async Task<InitializeStartingQuestsResult> Handle(InitializeStartingQuestsCommand request, CancellationToken cancellationToken)
    {
        await _initService.InitializeStartingQuests(request.SaveGame);

        return new InitializeStartingQuestsResult(
            true,
            request.SaveGame.AvailableQuests.Count);
    }
}
