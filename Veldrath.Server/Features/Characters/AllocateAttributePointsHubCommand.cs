using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that spends a character's unallocated attribute points.
/// Validates that the character has enough points, applies the increases, and persists the result.
/// </summary>
/// <remarks>
/// The points it spends are granted by <see cref="Veldrath.Server.Features.LevelUp.CharacterProgression"/> on every
/// level-up (3 per level, 2 more on every fifth) and stored in the attributes blob under
/// <c>UnspentAttributePoints</c> — the exact spelling matched below. A level-up that crossed no level banks nothing,
/// and a character created by the engine carries none until it levels.
/// </remarks>
public record AllocateAttributePointsHubCommand : IRequest<AllocateAttributePointsHubResult>
{
    /// <summary>Gets the ID of the character receiving the attribute increases.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>
    /// Gets the attribute allocation map. Keys are attribute names (e.g. <c>"Strength"</c>,
    /// <c>"Dexterity"</c>); values are the number of points to spend on that attribute.
    /// All values must be positive and the total must not exceed <see cref="UnspentAttributePoints"/>.
    /// </summary>
    public required Dictionary<string, int> Allocations { get; init; }
}

/// <summary>Result returned by <see cref="AllocateAttributePointsHubCommandHandler"/>.</summary>
public record AllocateAttributePointsHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the number of attribute points that were spent.</summary>
    public int PointsSpent { get; init; }

    /// <summary>Gets the remaining unspent attribute points after this allocation.</summary>
    public int RemainingPoints { get; init; }

    /// <summary>Gets the updated attribute values after applying the allocation.</summary>
    public Dictionary<string, int> NewAttributes { get; init; } = [];
}

/// <summary>
/// Handles <see cref="AllocateAttributePointsHubCommand"/> by loading the server-side character,
/// deserialising the attributes JSON blob, applying the point allocations, and persisting the result.
/// </summary>
/// <remarks>
/// <para>
/// An allocation writes the attribute and the <c>UnspentAttributePoints</c> balance and nothing else. It deliberately
/// does not recompute <c>MaxHealth</c>/<c>MaxMana</c>/<c>CurrentHealth</c>/<c>CurrentMana</c>: the stored pools are the
/// class curve <see cref="Veldrath.Server.Features.LevelUp.CharacterProgression"/> projects on a level-up — the
/// class's level-1 pool plus its per-level growth, with the stored pool as the floor — and that curve does not read an
/// attribute. Calling the same recompute from here would be a no-op while a class row resolves and would pay the
/// engine's <c>Constitution × 10</c> / <c>Wisdom × 5</c> formulas only for a character whose class row does not, so one
/// point would be worth 10 health to some characters and nothing to the rest.
/// </para>
/// <para>
/// The attributes take effect where combat reads them: <c>Strength</c> raises player damage, <c>Constitution</c>
/// reduces incoming damage (<c>CombatHelpers.CalculateEnemyDamage</c>) and <c>Intelligence</c> raises magic damage.
/// <c>Wisdom</c>, <c>Dexterity</c> and <c>Charisma</c> have no server-side reader yet, so a point there is inert until
/// a rule consumes it. The client's health bar is the stored <c>MaxHealth</c>, so allocating <c>Constitution</c> does
/// not move the bar — that is the recorded decision, not a regression. The facts in
/// <c>AllocateAttributePointsHubCommandTests</c> pin it.
/// </para>
/// </remarks>
public class AllocateAttributePointsHubCommandHandler
    : IRequestHandler<AllocateAttributePointsHubCommand, AllocateAttributePointsHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<AllocateAttributePointsHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="AllocateAttributePointsHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public AllocateAttributePointsHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<AllocateAttributePointsHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the allocation outcome.</summary>
    /// <param name="request">The command containing the character ID and per-attribute point amounts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AllocateAttributePointsHubResult"/> describing the outcome.</returns>
    public async Task<AllocateAttributePointsHubResult> Handle(
        AllocateAttributePointsHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Allocations.Count == 0)
            return Fail("No attribute allocations provided.");

        if (request.Allocations.Values.Any(v => v <= 0))
            return Fail("All attribute allocation values must be positive.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        // Parse the JSON attributes blob; default to empty if blank / invalid
        var existing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(character.Attributes) && character.Attributes != "{}")
        {
            try
            {
                existing = JsonSerializer.Deserialize<Dictionary<string, int>>(
                    character.Attributes, JsonOptions) ?? existing;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialise attributes for character {Id}; treating as empty.", character.Id);
            }
        }

        int totalToSpend = request.Allocations.Values.Sum();

        // Unspent points are stored in the attributes blob under the well-known key.
        existing.TryGetValue("UnspentAttributePoints", out int available);
        if (totalToSpend > available)
            return Fail($"Not enough unspent attribute points. Have {available}, need {totalToSpend}.");

        // Apply increases
        foreach (var (attr, points) in request.Allocations)
        {
            existing.TryGetValue(attr, out int current);
            existing[attr] = current + points;
        }

        existing["UnspentAttributePoints"] = available - totalToSpend;

        character.Attributes = JsonSerializer.Serialize(existing);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} spent {Points} attribute points. Remaining: {Remaining}",
            character.Id, totalToSpend, existing["UnspentAttributePoints"]);

        return new AllocateAttributePointsHubResult
        {
            Success = true,
            PointsSpent = totalToSpend,
            RemainingPoints = existing["UnspentAttributePoints"],
            NewAttributes = existing,
        };
    }

    private static AllocateAttributePointsHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
