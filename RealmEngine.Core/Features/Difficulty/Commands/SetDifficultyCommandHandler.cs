using MediatR;
using Serilog;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Commands;

/// <summary>
/// Handler for SetDifficultyCommand.
/// Sets the difficulty level for the current game session.
/// </summary>
public class SetDifficultyCommandHandler : IRequestHandler<SetDifficultyCommand, SetDifficultyResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly IApocalypseTimer? _apocalypseTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetDifficultyCommandHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="apocalypseTimer">Optional apocalypse timer service.</param>
    public SetDifficultyCommandHandler(
        ISaveGameService saveGameService,
        IApocalypseTimer? apocalypseTimer = null)
    {
        _saveGameService = saveGameService;
        _apocalypseTimer = apocalypseTimer;
    }

    /// <summary>
    /// Handles the set difficulty command.
    /// </summary>
    /// <param name="request">The set difficulty command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of setting difficulty.</returns>
    public Task<SetDifficultyResult> Handle(SetDifficultyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new SetDifficultyResult
                {
                    Success = false,
                    ErrorMessage = "No active game session"
                });
            }

            // Get difficulty settings by name
            var difficulty = DifficultySettings.GetByName(request.DifficultyName);
            
            if (difficulty.Name != request.DifficultyName && request.DifficultyName != "Normal")
            {
                // GetByName returns Normal as default, check if that was intentional
                return Task.FromResult(new SetDifficultyResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown difficulty: {request.DifficultyName}"
                });
            }

            // Set difficulty in save game
            saveGame.DifficultyLevel = difficulty.Name;

            // Initialize apocalypse timer if apocalypse mode
            if (difficulty.IsApocalypse && _apocalypseTimer != null)
            {
                _apocalypseTimer.Start();
                Log.Information("Apocalypse mode started with {Minutes} minute time limit", 
                    difficulty.ApocalypseTimeLimitMinutes);
            }

            Log.Information("Difficulty set to {Difficulty} for player {Player}", 
                difficulty.Name, saveGame.PlayerName);

            return Task.FromResult(new SetDifficultyResult
            {
                Success = true,
                DifficultyName = difficulty.Name,
                ApocalypseModeEnabled = difficulty.IsApocalypse,
                ApocalypseTimeLimitMinutes = difficulty.IsApocalypse 
                    ? difficulty.ApocalypseTimeLimitMinutes 
                    : null
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting difficulty to {Difficulty}", request.DifficultyName);
            return Task.FromResult(new SetDifficultyResult
            {
                Success = false,
                ErrorMessage = $"Failed to set difficulty: {ex.Message}"
            });
        }
    }
}
