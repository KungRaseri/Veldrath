using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Handles the SaveGame command.
/// </summary>
public class SaveGameHandler(ISaveGameService saveGameService, ILogger<SaveGameHandler> logger)
    : IRequestHandler<SaveGameCommand, SaveGameResult>
{
    /// <summary>
    /// Handles the SaveGameCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<SaveGameResult> Handle(SaveGameCommand request, CancellationToken cancellationToken)
    {
        try
        {
            saveGameService.SaveGame(request.Player, request.Inventory, request.SaveId);

            logger.LogInformation("Game saved for player {PlayerName}", request.Player.Name);

            return Task.FromResult(new SaveGameResult
            {
                Success = true,
                Message = "Game saved successfully!",
                SaveId = request.SaveId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save game for player {PlayerName}", request.Player.Name);

            return Task.FromResult(new SaveGameResult
            {
                Success = false,
                Message = $"Failed to save game: {ex.Message}",
                SaveId = null
            });
        }
    }
}