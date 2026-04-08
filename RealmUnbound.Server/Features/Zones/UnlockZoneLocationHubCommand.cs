using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that explicitly unlocks a hidden <see cref="RealmEngine.Data.Entities.ZoneLocation"/>
/// for a character. Called directly for manual, quest, and item-triggered unlocks.
/// </summary>
/// <param name="CharacterId">The character receiving the unlock.</param>
/// <param name="LocationSlug">Slug of the hidden location to unlock.</param>
/// <param name="UnlockSource">How the location was unlocked (e.g. "quest", "item", "manual").</param>
public record UnlockZoneLocationHubCommand(Guid CharacterId, string LocationSlug, string UnlockSource)
    : IRequest<UnlockZoneLocationHubResult>;

/// <summary>Result returned by <see cref="UnlockZoneLocationHubCommandHandler"/>.</summary>
public record UnlockZoneLocationHubResult
{
    /// <summary>Gets a value indicating whether the unlock succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the slug of the location that was unlocked, or <see langword="null"/> on failure.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of the unlocked location, or <see langword="null"/> on failure.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets the type key of the unlocked location, or <see langword="null"/> on failure.</summary>
    public string? TypeKey { get; init; }

    /// <summary>True when the location was already unlocked before this call (idempotent).</summary>
    public bool WasAlreadyUnlocked { get; init; }
}

/// <summary>
/// Handles <see cref="UnlockZoneLocationHubCommand"/> by verifying the location is hidden,
/// then persisting an unlock row in <see cref="ICharacterUnlockedLocationRepository"/>.
/// </summary>
public class UnlockZoneLocationHubCommandHandler
    : IRequestHandler<UnlockZoneLocationHubCommand, UnlockZoneLocationHubResult>
{
    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly ILogger<UnlockZoneLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="UnlockZoneLocationHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository used to look up zone location catalog entries.</param>
    /// <param name="unlockedRepo">Repository used to persist character unlock records.</param>
    /// <param name="logger">Logger instance.</param>
    public UnlockZoneLocationHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        ILogger<UnlockZoneLocationHubCommandHandler> logger)
    {
        _locationRepo  = locationRepo;
        _unlockedRepo  = unlockedRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the unlock outcome.</summary>
    /// <param name="request">The command containing the character ID, location slug, and unlock source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="UnlockZoneLocationHubResult"/> describing the outcome.</returns>
    public async Task<UnlockZoneLocationHubResult> Handle(
        UnlockZoneLocationHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LocationSlug))
            return new UnlockZoneLocationHubResult { Success = false, ErrorMessage = "Location slug cannot be empty" };

        var location = await _locationRepo.GetBySlugAsync(request.LocationSlug);

        if (location is null)
            return new UnlockZoneLocationHubResult
            {
                Success      = false,
                ErrorMessage = $"Location '{request.LocationSlug}' does not exist",
            };

        if (!location.IsHidden)
            return new UnlockZoneLocationHubResult
            {
                Success      = false,
                ErrorMessage = $"Location '{request.LocationSlug}' is not a hidden location",
            };

        var alreadyUnlocked = await _unlockedRepo.IsUnlockedAsync(
            request.CharacterId, request.LocationSlug, cancellationToken);

        if (!alreadyUnlocked)
        {
            await _unlockedRepo.AddUnlockAsync(
                request.CharacterId, request.LocationSlug, request.UnlockSource, cancellationToken);

            _logger.LogInformation(
                "Character {CharacterIdPrefix} unlocked hidden location {LocationSlug} via {UnlockSource}",
                request.CharacterId.ToString()[..8], location.Slug, request.UnlockSource);
        }

        return new UnlockZoneLocationHubResult
        {
            Success             = true,
            LocationSlug        = location.Slug,
            LocationDisplayName = location.DisplayName,
            TypeKey             = location.TypeKey,
            WasAlreadyUnlocked  = alreadyUnlocked,
        };
    }
}
