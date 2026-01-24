using MediatR;
using Serilog;
using RealmEngine.Core.Generators.Modern;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for GenerateNPCCommand.
/// Delegates to NpcGenerator to create NPCs.
/// </summary>
public class GenerateNPCCommandHandler : IRequestHandler<GenerateNPCCommand, GenerateNPCResult>
{
    private readonly NpcGenerator _npcGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateNPCCommandHandler"/> class.
    /// </summary>
    /// <param name="npcGenerator">The NPC generator.</param>
    public GenerateNPCCommandHandler(NpcGenerator npcGenerator)
    {
        _npcGenerator = npcGenerator;
    }

    /// <summary>
    /// Handles the generate NPC command.
    /// </summary>
    /// <param name="request">The generate NPC command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated NPC result.</returns>
    public async Task<GenerateNPCResult> Handle(GenerateNPCCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new GenerateNPCResult
                {
                    Success = false,
                    ErrorMessage = "Category cannot be empty"
                };
            }

            var npcs = await _npcGenerator.GenerateNpcsAsync(request.Category, 1, request.Hydrate);
            
            if (npcs == null || npcs.Count == 0)
            {
                return new GenerateNPCResult
                {
                    Success = false,
                    ErrorMessage = $"No NPCs found in category: {request.Category}"
                };
            }

            var npc = npcs[0];

            Log.Debug("Generated NPC: {NPCName} from category {Category}", 
                npc.Name, request.Category);

            return new GenerateNPCResult
            {
                Success = true,
                NPC = npc
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating NPC from category {Category}", request.Category);
            return new GenerateNPCResult
            {
                Success = false,
                ErrorMessage = $"Failed to generate NPC: {ex.Message}"
            };
        }
    }
}
