using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Party.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Party.Commands;

/// <summary>
/// Command to recruit an NPC to the player's party.
/// </summary>
public record RecruitNPCCommand : IRequest<RecruitNPCResult>
{
    /// <summary>
    /// NPC ID to recruit.
    /// </summary>
    public string NpcId { get; init; } = string.Empty;
}

/// <summary>
/// Result of recruiting an NPC.
/// </summary>
public record RecruitNPCResult
{
    /// <summary>
    /// Whether recruitment was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The recruited party member (if successful).
    /// </summary>
    public Shared.Models.PartyMember? Member { get; init; }
}

/// <summary>
/// Handler for RecruitNPCCommand.
/// </summary>
public class RecruitNPCHandler : IRequestHandler<RecruitNPCCommand, RecruitNPCResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly PartyService _partyService;
    private readonly ILogger<RecruitNPCHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecruitNPCHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="partyService">The party service.</param>
    /// <param name="logger">The logger.</param>
    public RecruitNPCHandler(ISaveGameService saveGameService, PartyService partyService, ILogger<RecruitNPCHandler> logger)
    {
        _saveGameService = saveGameService;
        _partyService = partyService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the recruit NPC command.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<RecruitNPCResult> Handle(RecruitNPCCommand request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
        {
            return Task.FromResult(new RecruitNPCResult
            {
                Success = false,
                Message = "No active game!"
            });
        }

        // Find NPC
        var npc = saveGame.KnownNPCs.FirstOrDefault(n => n.Id == request.NpcId);
        if (npc == null)
        {
            _logger.LogWarning("Failed to recruit NPC: NPC {NpcId} not found", request.NpcId);
            return Task.FromResult(new RecruitNPCResult
            {
                Success = false,
                Message = "NPC not found!"
            });
        }

        // Check if NPC is friendly
        if (!npc.IsFriendly)
        {
            _logger.LogWarning("Failed to recruit NPC: {Name} is not friendly", npc.Name);
            return Task.FromResult(new RecruitNPCResult
            {
                Success = false,
                Message = $"{npc.Name} is hostile and cannot be recruited!"
            });
        }

        // Initialize party if not exists
        if (saveGame.Party == null)
        {
            saveGame.Party = _partyService.CreateParty(saveGame.Character);
            _logger.LogInformation("Created new party for player {Name}", saveGame.Character.Name);
        }

        // Recruit NPC
        var success = _partyService.RecruitNPC(saveGame.Party, npc, out string errorMessage);

        if (!success)
        {
            _logger.LogWarning("Failed to recruit NPC: {Message}", errorMessage);
            return Task.FromResult(new RecruitNPCResult
            {
                Success = false,
                Message = errorMessage
            });
        }

        // Get recruited member
        var member = saveGame.Party.Members.Last();

        // Save
        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Successfully recruited {Name} to party (Role: {Role})", npc.Name, member.Role);

        return Task.FromResult(new RecruitNPCResult
        {
            Success = true,
            Message = $"{npc.Name} has joined your party as a {member.Role}!",
            Member = member
        });
    }
}
