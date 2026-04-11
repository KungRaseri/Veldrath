using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that spends a character's unallocated attribute points.
/// Validates that the character has enough points, applies the increases, and persists the result.
/// </summary>
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
        _logger        = logger;
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
            Success          = true,
            PointsSpent      = totalToSpend,
            RemainingPoints  = existing["UnspentAttributePoints"],
            NewAttributes    = existing,
        };
    }

    private static AllocateAttributePointsHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
