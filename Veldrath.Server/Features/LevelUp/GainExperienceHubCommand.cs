using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.LevelUp;

/// <summary>
/// Server-side command that awards experience to a stored character and persists the updated
/// <see cref="Data.Entities.Character.Level"/> and <see cref="Data.Entities.Character.Experience"/>.
/// Uses the same rolling XP formula as <c>RealmEngine.Shared.Models.Character.GainExperience()</c>:
/// <c>Level × 100</c> XP is required to advance each level; surplus carries over.
/// </summary>
/// <remarks>
/// This command is dispatched server-side only — by the quest turn-in path and by the development-only
/// experience endpoint. It is deliberately not a hub method: a client-callable entry would let any
/// authenticated connection award itself arbitrary experience.
/// </remarks>
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

    /// <summary>
    /// Gets the character's maximum health after the award, or 0 when the attributes blob does not carry a
    /// <c>MaxHealth</c> entry. A level-up rewrites it; an award inside the level reports the stored value.
    /// </summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets the character's current health after the award, or 0 when the blob carries no vitals.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's maximum mana after the award, or 0 when the blob does not carry it.</summary>
    public int MaxMana { get; init; }

    /// <summary>Gets the character's current mana after the award, or 0 when the blob carries no vitals.</summary>
    public int CurrentMana { get; init; }
}

/// <summary>
/// Handles <see cref="GainExperienceHubCommand"/> by loading the character, applying
/// the rolling XP formula, persisting the change, and returning <see cref="GainExperienceHubResult"/>.
/// </summary>
/// <remarks>
/// A level-up also rewrites the derived pools in the attributes blob (<c>MaxHealth</c>, <c>MaxMana</c>) through
/// <see cref="CharacterProgression"/>, along the class curve creation encoded (the row is resolved by
/// <see cref="Data.Entities.Character.ClassName"/>) and never below the stored pool; see that class for why the level
/// and the health bar have to move together.
/// </remarks>
public class GainExperienceHubCommandHandler : IRequestHandler<GainExperienceHubCommand, GainExperienceHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterClassRepository _classRepo;
    private readonly ILogger<GainExperienceHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GainExperienceHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="classRepo">
    /// Catalogue repository used to resolve the character's class curve for the level-up pool recompute.
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public GainExperienceHubCommandHandler(
        ICharacterRepository characterRepo,
        ICharacterClassRepository classRepo,
        ILogger<GainExperienceHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _classRepo = classRepo;
        _logger = logger;
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

        var attributes = ParseAttributes(character);

        var progress = CharacterProgression.AwardExperience(
            character,
            request.Amount,
            attributes,
            ResolveCharacterClass(character.ClassName));

        // Only a level-up changes the blob, so an award that stays inside the level leaves it (and any corrupt
        // blob) exactly as it was.
        if (progress.LevelledUp)
            character.Attributes = JsonSerializer.Serialize(attributes);

        character.LastPlayedAt = DateTimeOffset.UtcNow;

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} gained {Amount} XP from {Source}. Level: {Level}, XP toward next: {Xp}",
            request.CharacterId, request.Amount, request.Source ?? "Unknown",
            progress.NewLevel, progress.RemainingExperience);

        return new GainExperienceHubResult
        {
            Success = true,
            NewLevel = progress.NewLevel,
            NewExperience = progress.RemainingExperience,
            LeveledUp = progress.LevelledUp,
            LeveledUpTo = progress.LevelledUp ? progress.NewLevel : null,
            MaxHealth = attributes.GetValueOrDefault(CharacterProgression.KeyMaxHealth),
            CurrentHealth = attributes.GetValueOrDefault(
                CharacterProgression.KeyCurrentHealth,
                attributes.GetValueOrDefault(CharacterProgression.KeyMaxHealth)),
            MaxMana = attributes.GetValueOrDefault(CharacterProgression.KeyMaxMana),
            CurrentMana = attributes.GetValueOrDefault(
                CharacterProgression.KeyCurrentMana,
                attributes.GetValueOrDefault(CharacterProgression.KeyMaxMana)),
        };
    }

    /// <summary>
    /// Resolves the catalogue row the character was created from, so a level-up extends the class's own pool curve
    /// (level-1 pool plus growth) instead of guessing one. A miss leaves the engine formulas as the fallback.
    /// </summary>
    /// <param name="className">The class name stored on the character row.</param>
    /// <returns>The resolved class, or <see langword="null"/> when the catalogue has no such row or cannot be read.</returns>
    private CharacterClass? ResolveCharacterClass(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return null;

        try
        {
            return _classRepo.GetByName(className);
        }
        catch (Exception ex)
        {
            // A content-catalog read must not swallow an experience award (a quest turn-in would fail after the
            // level was computed); the pool derivation falls back to the engine formulas instead.
            _logger.LogWarning(
                ex,
                "Could not resolve character class {ClassName}; deriving pools from the engine formulas instead.",
                className);
            return null;
        }
    }

    /// <summary>Deserialises the attributes blob, treating a blank or corrupt blob as empty.</summary>
    /// <param name="character">Character whose blob is read.</param>
    /// <returns>The parsed attributes, case-insensitive.</returns>
    private Dictionary<string, int> ParseAttributes(Data.Entities.Character character)
    {
        var attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(character.Attributes) || character.Attributes == "{}")
            return attributes;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes, JsonOptions)
                ?? attributes;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise attributes for character {Id}; treating as empty.",
                character.Id);
            return attributes;
        }
    }
}
