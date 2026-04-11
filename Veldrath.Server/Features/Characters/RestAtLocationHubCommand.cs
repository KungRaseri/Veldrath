using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that lets a character rest at an inn or rest point, restoring their
/// current health and mana to their maximum values in exchange for a gold cost.
/// Both health/mana pools and gold are stored within the character's JSON attributes blob.
/// If the pools are absent the handler initialises them from the character's level.
/// </summary>
public record RestAtLocationHubCommand : IRequest<RestAtLocationHubResult>
{
    /// <summary>Gets the ID of the character that will rest.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>Gets the ID of the location (inn / rest point) where the character is resting.</summary>
    public required string LocationId { get; init; }

    /// <summary>Gets the gold cost to rest. Must be non-negative.</summary>
    public int CostInGold { get; init; } = 10;
}

/// <summary>Result returned by <see cref="RestAtLocationHubCommandHandler"/>.</summary>
public record RestAtLocationHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's current health after resting.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's maximum health.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets the character's current mana after resting.</summary>
    public int CurrentMana { get; init; }

    /// <summary>Gets the character's maximum mana.</summary>
    public int MaxMana { get; init; }

    /// <summary>Gets the character's remaining gold after paying for the rest.</summary>
    public int GoldRemaining { get; init; }
}

/// <summary>
/// Handles <see cref="RestAtLocationHubCommand"/> by loading the server-side character,
/// deserialising the attributes JSON blob, validating that the character can afford the rest,
/// restoring health and mana to their maximum values, deducting the gold cost, and persisting
/// the updated attributes.
/// </summary>
public class RestAtLocationHubCommandHandler
    : IRequestHandler<RestAtLocationHubCommand, RestAtLocationHubResult>
{
    // Keys within the character Attributes JSON blob
    internal const string KeyGold          = "Gold";
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth     = "MaxHealth";
    internal const string KeyCurrentMana   = "CurrentMana";
    internal const string KeyMaxMana       = "MaxMana";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<RestAtLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="RestAtLocationHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public RestAtLocationHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<RestAtLocationHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the rest outcome.</summary>
    /// <param name="request">The command containing the character ID, location, and gold cost.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="RestAtLocationHubResult"/> describing the outcome.</returns>
    public async Task<RestAtLocationHubResult> Handle(
        RestAtLocationHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CostInGold < 0)
            return Fail("Rest cost cannot be negative.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        // Parse the attributes blob; default to empty if blank / invalid
        var attrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(character.Attributes) && character.Attributes != "{}")
        {
            try
            {
                attrs = JsonSerializer.Deserialize<Dictionary<string, int>>(
                    character.Attributes, JsonOptions) ?? attrs;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialise attributes for character {Id}; treating as empty.",
                    character.Id);
            }
        }

        // Derive max pools from level when not explicitly stored (10 HP and 5 MP per level)
        var maxHealth = attrs.TryGetValue(KeyMaxHealth, out var mh) ? mh : character.Level * 10;
        var maxMana   = attrs.TryGetValue(KeyMaxMana,   out var mm) ? mm : character.Level * 5;
        var gold      = attrs.TryGetValue(KeyGold,      out var g)  ? g  : 0;

        if (gold < request.CostInGold)
            return Fail($"Not enough gold to rest. Have {gold}, need {request.CostInGold}.");

        // Restore and deduct
        attrs[KeyCurrentHealth] = maxHealth;
        attrs[KeyCurrentMana]   = maxMana;
        attrs[KeyMaxHealth]     = maxHealth;
        attrs[KeyMaxMana]       = maxMana;
        attrs[KeyGold]          = gold - request.CostInGold;

        character.Attributes = JsonSerializer.Serialize(attrs);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} rested at {LocationId}: health {Hp}/{MaxHp}, mana {Mp}/{MaxMp}, gold remaining {Gold}",
            request.CharacterId, request.LocationId,
            maxHealth, maxHealth, maxMana, maxMana, attrs[KeyGold]);

        return new RestAtLocationHubResult
        {
            Success       = true,
            CurrentHealth = maxHealth,
            MaxHealth     = maxHealth,
            CurrentMana   = maxMana,
            MaxMana       = maxMana,
            GoldRemaining = attrs[KeyGold],
        };
    }

    private static RestAtLocationHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
