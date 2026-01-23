using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for EncounterNPCCommand.
/// Determines available actions based on NPC traits (merchant, quest giver, etc.)
/// </summary>
public class EncounterNPCCommandHandler : IRequestHandler<EncounterNPCCommand, EncounterNPCResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<EncounterNPCCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncounterNPCCommandHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public EncounterNPCCommandHandler(
        ISaveGameService saveGameService,
        ILogger<EncounterNPCCommandHandler> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the NPC encounter command.
    /// </summary>
    /// <param name="request">The encounter command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The encounter result.</returns>
    public Task<EncounterNPCResult> Handle(EncounterNPCCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                return Task.FromResult(new EncounterNPCResult(false, ErrorMessage: "No active game session"));
            }

            // Find NPC in known NPCs (already encountered) or generate new encounter
            var npc = saveGame.KnownNPCs.FirstOrDefault(n => n.Id == request.NpcId);
            
            if (npc == null)
            {
                _logger.LogWarning("NPC {NpcId} not found in KnownNPCs", request.NpcId);
                return Task.FromResult(new EncounterNPCResult(false, ErrorMessage: $"NPC {request.NpcId} not found"));
            }

            // Add to known NPCs if first encounter
            _saveGameService.MeetNPC(npc);

            // Determine available actions based on NPC traits
            var availableActions = new List<string> { "Talk", "Leave" };

            // Check for merchant trait
            if (npc.Traits.ContainsKey("isMerchant") && npc.Traits["isMerchant"].AsBool())
            {
                availableActions.Insert(1, "Trade");
            }

            // Check for quest giver trait (future)
            if (npc.Traits.ContainsKey("isQuestGiver"))
            {
                availableActions.Insert(1, "Quest");
            }

            // Check for trainer trait (future)
            if (npc.Traits.ContainsKey("isTrainer"))
            {
                availableActions.Insert(1, "Train");
            }

            _logger.LogInformation("Player encountered NPC {NpcName} ({NpcId}) with {ActionCount} actions available",
                npc.Name, npc.Id, availableActions.Count);

            return Task.FromResult(new EncounterNPCResult(
                Success: true,
                Npc: npc,
                AvailableActions: availableActions
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NPC encounter {NpcId}", request.NpcId);
            return Task.FromResult(new EncounterNPCResult(false, ErrorMessage: $"Failed to encounter NPC: {ex.Message}"));
        }
    }
}
