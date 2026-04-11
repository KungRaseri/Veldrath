using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that looks up a dungeon zone by its slug and returns the dungeon ID so the client
/// can initiate gameplay within that dungeon. The zone must exist and have
/// <see cref="ZoneType.Dungeon"/> type.
/// </summary>
/// <param name="CharacterId">The ID of the character entering the dungeon.</param>
/// <param name="DungeonSlug">The slug / zone ID of the dungeon (e.g. <c>"dungeon-grotto"</c>).</param>
public record EnterDungeonHubCommand(Guid CharacterId, string DungeonSlug) : IRequest<EnterDungeonHubResult>;

/// <summary>Result returned by <see cref="EnterDungeonHubCommandHandler"/>.</summary>
public record EnterDungeonHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the dungeon zone ID on success, or <see langword="null"/> on failure.</summary>
    public string? DungeonId { get; init; }
}

/// <summary>
/// Handles <see cref="EnterDungeonHubCommand"/> by looking up the requested dungeon zone via
/// <see cref="IZoneRepository"/> and returning its ID. Returns an error result when the slug is
/// empty, the zone is not found, or the zone is not a dungeon.
/// </summary>
public class EnterDungeonHubCommandHandler : IRequestHandler<EnterDungeonHubCommand, EnterDungeonHubResult>
{
    private readonly IZoneRepository _zoneRepo;
    private readonly ILogger<EnterDungeonHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EnterDungeonHubCommandHandler"/>.</summary>
    /// <param name="zoneRepo">Repository used to look up zone catalog entries.</param>
    /// <param name="logger">Logger instance.</param>
    public EnterDungeonHubCommandHandler(
        IZoneRepository zoneRepo,
        ILogger<EnterDungeonHubCommandHandler> logger)
    {
        _zoneRepo = zoneRepo;
        _logger   = logger;
    }

    /// <summary>Handles the command and returns the dungeon entry outcome.</summary>
    /// <param name="request">The command containing the character ID and dungeon slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EnterDungeonHubResult"/> describing the outcome.</returns>
    public async Task<EnterDungeonHubResult> Handle(
        EnterDungeonHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DungeonSlug))
            return new EnterDungeonHubResult { Success = false, ErrorMessage = "Dungeon slug cannot be empty" };

        var zone = await _zoneRepo.GetByIdAsync(request.DungeonSlug);
        if (zone is null)
            return new EnterDungeonHubResult { Success = false, ErrorMessage = $"Dungeon '{request.DungeonSlug}' not found" };

        if (zone.Type != ZoneType.Dungeon)
            return new EnterDungeonHubResult { Success = false, ErrorMessage = $"'{zone.Name}' is not a dungeon" };

        _logger.LogInformation(
            "Character {CharacterIdPrefix} entered dungeon {DungeonId} ({Name})",
            request.CharacterId.ToString()[..8], zone.Id, zone.Name);

        return new EnterDungeonHubResult
        {
            Success   = true,
            DungeonId = zone.Id,
        };
    }
}
