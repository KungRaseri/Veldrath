using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Handles the LoadGame command.
/// </summary>
public class LoadGameHandler(ISaveGameService saveGameService, ILogger<LoadGameHandler> logger)
    : IRequestHandler<LoadGameCommand, LoadGameResult>
{

    /// <summary>
    /// Handles the LoadGameCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<LoadGameResult> Handle(LoadGameCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = saveGameService.LoadGame(request.SaveId);

            if (saveGame == null)
            {
                return Task.FromResult(new LoadGameResult
                {
                    Success = false,
                    Message = "Save game not found",
                    SaveGame = null
                });
            }

            logger.LogInformation("Game loaded for player {PlayerName}", saveGame.Character.Name);

            return Task.FromResult(new LoadGameResult
            {
                Success = true,
                Message = $"Loaded game: {saveGame.Character.Name}",
                SaveGame = saveGame
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load game {SaveId}", request.SaveId);

            return Task.FromResult(new LoadGameResult
            {
                Success = false,
                Message = $"Failed to load game: {ex.Message}",
                SaveGame = null
            });
        }
    }
}