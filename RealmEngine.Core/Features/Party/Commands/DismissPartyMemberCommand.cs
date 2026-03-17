using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Party.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Party.Commands;

/// <summary>
/// Command to dismiss a party member.
/// </summary>
public record DismissPartyMemberCommand : IRequest<DismissPartyMemberResult>
{
    /// <summary>
    /// Party member ID to dismiss.
    /// </summary>
    public string MemberId { get; init; } = string.Empty;
}

/// <summary>
/// Result of dismissing a party member.
/// </summary>
public record DismissPartyMemberResult
{
    /// <summary>
    /// Whether dismissal was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler for DismissPartyMemberCommand.
/// </summary>
public class DismissPartyMemberHandler : IRequestHandler<DismissPartyMemberCommand, DismissPartyMemberResult>
{
    private readonly SaveGameService _saveGameService;
    private readonly PartyService _partyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DismissPartyMemberHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public DismissPartyMemberHandler(SaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
        _partyService = new PartyService();
    }

    /// <summary>
    /// Handles the dismiss party member command.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<DismissPartyMemberResult> Handle(DismissPartyMemberCommand request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
        {
            return Task.FromResult(new DismissPartyMemberResult
            {
                Success = false,
                Message = "No active game!"
            });
        }

        // Check if party exists
        if (saveGame.Party == null)
        {
            _logger.LogWarning("Failed to dismiss party member: No party exists");
            return Task.FromResult(new DismissPartyMemberResult
            {
                Success = false,
                Message = "You don't have a party!"
            });
        }

        // Dismiss member
        var success = _partyService.DismissPartyMember(saveGame.Party, request.MemberId, out string errorMessage);

        if (!success)
        {
            _logger.LogWarning("Failed to dismiss party member: {Message}", errorMessage);
            return Task.FromResult(new DismissPartyMemberResult
            {
                Success = false,
                Message = errorMessage
            });
        }

        // Save
        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Successfully dismissed party member");

        return Task.FromResult(new DismissPartyMemberResult
        {
            Success = true,
            Message = "Party member dismissed."
        });
    }
}
