using MediatR;
using Microsoft.Extensions.Logging;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.LevelUp;

/// <summary>
/// Hub command that awards experience to a server-side character and persists
/// the updated <see cref="Data.Entities.Character.Level"/> and
/// <see cref="Data.Entities.Character.Experience"/> to the database.
/// Uses the same rolling XP formula as <c>RealmEngine.Shared.Models.Character.GainExperience()</c>:
/// <c>Level × 100</c> XP is required to advance each level; surplus carries over.
/// </summary>
public record GainExperienceHubCommand : IRequest<GainExperienceHubResult>
{
    /// <summary>Gets the ID of the character receiving experience.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>Gets the amount of experience to award. Must be a positive value.</summary>
    public required int Amount { get; init; }

    /// <summary>Gets an optional label describing the experience source (e.g. <c>"Combat"</c>, <c>"Quest"</c>).</summary>
    public string? Source { get; init; }
}

/// <summary>Result returned by <see cref="GainExperienceHubCommandHandler"/>.</summary>
public record GainExperienceHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's level after the experience award.</summary>
    public int NewLevel { get; init; }

    /// <summary>Gets the character's accumulated experience toward the next level after the award.</summary>
    public long NewExperience { get; init; }

    /// <summary>Gets a value indicating whether the character levelled up during this award.</summary>
    public bool LeveledUp { get; init; }

    /// <summary>Gets the level the character reached when <see cref="LeveledUp"/> is <see langword="true"/>.</summary>
    public int? LeveledUpTo { get; init; }
}

/// <summary>
/// Handles <see cref="GainExperienceHubCommand"/> by loading the character, applying
/// the rolling XP formula, persisting the change, and returning <see cref="GainExperienceHubResult"/>.
/// </summary>
public class GainExperienceHubCommandHandler : IRequestHandler<GainExperienceHubCommand, GainExperienceHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<GainExperienceHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GainExperienceHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public GainExperienceHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<GainExperienceHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the experience-gain outcome.</summary>
    /// <param name="request">The command containing the character ID and XP amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GainExperienceHubResult"/> describing the outcome.</returns>
    public async Task<GainExperienceHubResult> Handle(
        GainExperienceHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            return new GainExperienceHubResult { Success = false, ErrorMessage = "Experience amount must be positive" };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new GainExperienceHubResult { Success = false, ErrorMessage = "Character not found" };

        var oldLevel = character.Level;

        // Apply XP using the same rolling formula as RealmEngine.Shared.Models.Character.GainExperience():
        //   while (xp >= level * 100) { xp -= level * 100; level++; }
        long xp    = character.Experience + request.Amount;
        int  level = character.Level;
        while (xp >= (long)level * 100)
        {
            xp -= (long)level * 100;
            level++;
        }

        character.Experience   = xp;
        character.Level        = level;
        character.LastPlayedAt = DateTimeOffset.UtcNow;

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} gained {Amount} XP from {Source}. Level: {Level}, XP toward next: {Xp}",
            request.CharacterId, request.Amount, request.Source ?? "Unknown", level, xp);

        return new GainExperienceHubResult
        {
            Success       = true,
            NewLevel      = level,
            NewExperience = xp,
            LeveledUp     = level > oldLevel,
            LeveledUpTo   = level > oldLevel ? level : null,
        };
    }
}
