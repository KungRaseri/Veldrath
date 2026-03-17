using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.SaveLoad.Commands;

/// <summary>
/// Handles the DeleteSave command.
/// </summary>
public class DeleteSaveHandler(ISaveGameService saveGameService, ILogger<DeleteSaveHandler> logger)
    : IRequestHandler<DeleteSaveCommand, DeleteSaveResult>
{

    /// <summary>
    /// Handles the DeleteSaveCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<DeleteSaveResult> Handle(DeleteSaveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var success = saveGameService.DeleteSave(request.SaveId);

            if (!success)
            {
                return Task.FromResult(new DeleteSaveResult
                {
                    Success = false,
                    Message = "Save game not found or could not be deleted"
                });
            }

            logger.LogInformation("Save game deleted: {SaveId}", request.SaveId);

            return Task.FromResult(new DeleteSaveResult
            {
                Success = true,
                Message = "Save game deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete save game {SaveId}", request.SaveId);

            return Task.FromResult(new DeleteSaveResult
            {
                Success = false,
                Message = $"Failed to delete save: {ex.Message}"
            });
        }
    }
}