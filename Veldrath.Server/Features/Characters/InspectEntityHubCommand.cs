using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Services;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that inspects a visible entity (player, enemy, or NPC) at the character's current location,
/// returning descriptive information about it.
/// </summary>
/// <param name="CharacterId">The character performing the inspection.</param>
/// <param name="TargetEntityId">The unique instance ID of the entity to inspect.</param>
/// <param name="ZoneId">The zone identifier where the interaction is taking place.</param>
public record InspectEntityHubCommand(Guid CharacterId, Guid TargetEntityId, string ZoneId)
    : IRequest<InspectEntityHubResult>;

/// <summary>Result returned by <see cref="InspectEntityHubCommandHandler"/>.</summary>
public record InspectEntityHubResult
{
    /// <summary>Gets a value indicating whether the inspection succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the display name of the inspected entity.</summary>
    public string EntityName { get; init; } = string.Empty;

    /// <summary>Gets the type category of the inspected entity: <c>"player"</c>, <c>"enemy"</c>, or <c>"npc"</c>.</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>Gets a human-readable description of the inspected entity.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="InspectEntityHubCommand"/> by looking up the target entity
/// and returning descriptive information based on its type and sprite key.
/// </summary>
public class InspectEntityHubCommandHandler
    : IRequestHandler<InspectEntityHubCommand, InspectEntityHubResult>
{
    private readonly IZoneEntityTracker _entityTracker;
    private readonly ILogger<InspectEntityHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="InspectEntityHubCommandHandler"/>.</summary>
    /// <param name="entityTracker">The zone entity tracker for looking up live entities.</param>
    /// <param name="logger">Logger instance.</param>
    public InspectEntityHubCommandHandler(
        IZoneEntityTracker entityTracker,
        ILogger<InspectEntityHubCommandHandler> logger)
    {
        _entityTracker = entityTracker;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the inspection outcome.</summary>
    /// <param name="request">The command containing the character and target entity identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="InspectEntityHubResult"/> describing the outcome.</returns>
    public Task<InspectEntityHubResult> Handle(
        InspectEntityHubCommand request,
        CancellationToken cancellationToken)
    {
        var entities = _entityTracker.GetEntities(request.ZoneId);
        var entity = entities.FirstOrDefault(e => e.EntityId == request.TargetEntityId);

        if (entity is null)
            return Task.FromResult(new InspectEntityHubResult
            {
                Success = false,
                ErrorMessage = "Entity not found at this location.",
            });

        var displayName = entity.SpriteKey.Length > 0
            ? char.ToUpper(entity.SpriteKey[0]) + entity.SpriteKey[1..]
            : "Unknown";

        var description = entity.EntityType switch
        {
            "npc" => entity.SpriteKey switch
            {
                "merchant" => "A merchant peddling wares to passing adventurers.",
                "guard" => "A stoic guard keeping watch over the area.",
                "innkeeper" => "A welcoming innkeeper, ready to offer a room for the night.",
                "blacksmith" => "A skilled blacksmith, hammering steel at the forge.",
                "elder" => "A wise village elder with many tales to tell.",
                _ => $"A {displayName.ToLowerInvariant()} going about their business.",
            },
            "enemy" => entity.SpriteKey switch
            {
                "wolf" => "A lean, hungry wolf with sharp fangs and glowing eyes.",
                "skeleton" => "An animated skeleton, rattling with each step.",
                "goblin" => "A mischievous goblin, clutching a crude weapon.",
                "bandit" => "A rough-looking bandit, watching for easy prey.",
                "spider" => "A giant spider, its many eyes glinting in the dark.",
                "slime" => "A gelatinous slime, pulsing with an unnatural glow.",
                _ => $"A {displayName.ToLowerInvariant()} — hostile and alert.",
            },
            "player" => entity.EntityId == request.CharacterId
                ? "That's you — an adventurer seeking fortune and glory."
                : $"A fellow adventurer named {displayName}.",
            _ => $"A {displayName.ToLowerInvariant()}. You're not sure what to make of it.",
        };

        _logger.LogInformation(
            "Character {CharacterIdPrefix} inspected entity {EntityName} ({EntityType}, {EntityId})",
            request.CharacterId.ToString()[..8], displayName, entity.EntityType, request.TargetEntityId);

        return Task.FromResult(new InspectEntityHubResult
        {
            Success = true,
            EntityName = displayName,
            EntityType = entity.EntityType,
            Description = description,
        });
    }
}
