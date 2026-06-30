using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Services;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that initiates dialogue between a character and an NPC at their current location.
/// </summary>
/// <param name="CharacterId">The character initiating dialogue.</param>
/// <param name="TargetEntityId">The unique instance ID of the NPC to talk to.</param>
/// <param name="ZoneId">The zone identifier where the interaction is taking place.</param>
public record TalkToNpcHubCommand(Guid CharacterId, Guid TargetEntityId, string ZoneId)
    : IRequest<TalkToNpcHubResult>;

/// <summary>Result returned by <see cref="TalkToNpcHubCommandHandler"/>.</summary>
public record TalkToNpcHubResult
{
    /// <summary>Gets a value indicating whether the dialogue initiation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the display name of the NPC spoken to.</summary>
    public string NpcName { get; init; } = string.Empty;

    /// <summary>Gets the NPC's response dialogue text.</summary>
    public string DialogueText { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="TalkToNpcHubCommand"/> by looking up the target NPC entity
/// and returning a greeting response based on the NPC's sprite key.
/// </summary>
public class TalkToNpcHubCommandHandler
    : IRequestHandler<TalkToNpcHubCommand, TalkToNpcHubResult>
{
    private readonly IZoneEntityTracker _entityTracker;
    private readonly ILogger<TalkToNpcHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="TalkToNpcHubCommandHandler"/>.</summary>
    /// <param name="entityTracker">The zone entity tracker for looking up live entities.</param>
    /// <param name="logger">Logger instance.</param>
    public TalkToNpcHubCommandHandler(
        IZoneEntityTracker entityTracker,
        ILogger<TalkToNpcHubCommandHandler> logger)
    {
        _entityTracker = entityTracker;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the dialogue outcome.</summary>
    /// <param name="request">The command containing character, target entity, and zone identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TalkToNpcHubResult"/> describing the outcome.</returns>
    public Task<TalkToNpcHubResult> Handle(
        TalkToNpcHubCommand request,
        CancellationToken cancellationToken)
    {
        var entities = _entityTracker.GetEntities(request.ZoneId);
        var entity = entities.FirstOrDefault(e => e.EntityId == request.TargetEntityId);

        if (entity is null)
            return Task.FromResult(new TalkToNpcHubResult
            {
                Success = false,
                ErrorMessage = "NPC not found at this location.",
            });

        if (entity.EntityType != "npc")
            return Task.FromResult(new TalkToNpcHubResult
            {
                Success = false,
                ErrorMessage = "That entity is not an NPC and cannot be spoken to.",
            });

        // Generate a context-appropriate greeting based on the NPC's sprite key
        var npcName = entity.SpriteKey switch
        {
            "merchant" => "Merchant",
            "guard" => "Town Guard",
            "innkeeper" => "Innkeeper",
            "blacksmith" => "Blacksmith",
            "elder" => "Village Elder",
            _ => entity.SpriteKey.Length > 0
                ? char.ToUpper(entity.SpriteKey[0]) + entity.SpriteKey[1..]
                : "Stranger",
        };

        var dialogue = entity.SpriteKey switch
        {
            "merchant" => $"\"Welcome, traveler! Have a look at my wares.\"",
            "guard" => $"\"Stay out of trouble, and we won't have any problems.\"",
            "innkeeper" => $"\"Need a room? It's 10 gold for the night.\"",
            "blacksmith" => $"\"Fine steel for fine adventurers. What'll it be?\"",
            "elder" => $"\"Ah, another traveler. The roads have been dangerous lately.\"",
            _ => $"\"Hello there, adventurer. Safe travels to you.\"",
        };

        _logger.LogInformation(
            "Character {CharacterIdPrefix} talked to NPC {NpcName} ({EntityId})",
            request.CharacterId.ToString()[..8], npcName, request.TargetEntityId);

        return Task.FromResult(new TalkToNpcHubResult
        {
            Success = true,
            NpcName = npcName,
            DialogueText = dialogue,
        });
    }
}
