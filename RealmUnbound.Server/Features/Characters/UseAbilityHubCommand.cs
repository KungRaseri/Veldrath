using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Characters;

/// <summary>
/// Hub command that activates an ability for a character, consuming mana and optionally
/// restoring health for healing abilities.
/// Both mana and health pools are stored within the character's JSON attributes blob.
/// If pools are absent, the handler initialises them from the character's level.
/// </summary>
public record UseAbilityHubCommand : IRequest<UseAbilityHubResult>
{
    /// <summary>Gets the ID of the character using the ability.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>
    /// Gets the ID of the ability to activate (e.g. <c>"fireball"</c>, <c>"heal"</c>).
    /// Ability IDs that contain the word <c>"heal"</c> (case-insensitive) also restore
    /// <see cref="UseAbilityHubCommandHandler.HealingAmount"/> hit points.
    /// </summary>
    public required string AbilityId { get; init; }
}

/// <summary>Result returned by <see cref="UseAbilityHubCommandHandler"/>.</summary>
public record UseAbilityHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the ID of the ability that was used.</summary>
    public string AbilityId { get; init; } = string.Empty;

    /// <summary>Gets the mana cost deducted for using the ability.</summary>
    public int ManaCost { get; init; }

    /// <summary>Gets the character's remaining mana after using the ability.</summary>
    public int RemainingMana { get; init; }

    /// <summary>Gets the health restored by the ability when it has a healing effect; otherwise zero.</summary>
    public int HealthRestored { get; init; }
}

/// <summary>
/// Handles <see cref="UseAbilityHubCommand"/> by loading the server-side character,
/// deserialising the attributes JSON blob, validating the character has sufficient mana,
/// deducting the mana cost, optionally restoring health for healing abilities, and
/// persisting the updated attributes.
/// </summary>
public class UseAbilityHubCommandHandler
    : IRequestHandler<UseAbilityHubCommand, UseAbilityHubResult>
{
    // Keys within the character Attributes JSON blob
    internal const string KeyCurrentMana   = "CurrentMana";
    internal const string KeyMaxMana       = "MaxMana";
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth     = "MaxHealth";

    /// <summary>Mana point cost applied for every ability use.</summary>
    internal const int DefaultManaCost = 10;

    /// <summary>
    /// Hit points restored when the ability ID contains the word <c>"heal"</c> (case-insensitive).
    /// The actual amount restored is capped at <c>MaxHealth - CurrentHealth</c>.
    /// </summary>
    internal const int HealingAmount = 25;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<UseAbilityHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="UseAbilityHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public UseAbilityHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<UseAbilityHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the ability-use outcome.</summary>
    /// <param name="request">The command containing the character ID and ability ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="UseAbilityHubResult"/> describing the outcome.</returns>
    public async Task<UseAbilityHubResult> Handle(
        UseAbilityHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AbilityId))
            return Fail("AbilityId must not be empty.");

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

        // Derive mana pool from level when not explicitly stored (5 MP per level)
        var maxMana     = attrs.TryGetValue(KeyMaxMana,     out var mm) ? mm : character.Level * 5;
        var currentMana = attrs.TryGetValue(KeyCurrentMana, out var cm) ? cm : maxMana;

        if (currentMana < DefaultManaCost)
            return Fail($"Not enough mana. Have {currentMana}, need {DefaultManaCost}.");

        currentMana          -= DefaultManaCost;
        attrs[KeyCurrentMana] = currentMana;
        attrs[KeyMaxMana]     = maxMana;

        // Healing abilities restore HP (capped at MaxHealth)
        var healthRestored = 0;
        if (request.AbilityId.Contains("heal", StringComparison.OrdinalIgnoreCase))
        {
            var maxHealth     = attrs.TryGetValue(KeyMaxHealth,     out var mh) ? mh : character.Level * 10;
            var currentHealth = attrs.TryGetValue(KeyCurrentHealth, out var ch) ? ch : maxHealth;
            healthRestored          = Math.Min(HealingAmount, maxHealth - currentHealth);
            attrs[KeyCurrentHealth] = currentHealth + healthRestored;
            attrs[KeyMaxHealth]     = maxHealth;
        }

        character.Attributes = JsonSerializer.Serialize(attrs);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} used ability {AbilityId}: -{ManaCost} MP (remaining: {Mp}), +{Heal} HP",
            request.CharacterId, request.AbilityId, DefaultManaCost, currentMana, healthRestored);

        return new UseAbilityHubResult
        {
            Success        = true,
            AbilityId      = request.AbilityId,
            ManaCost       = DefaultManaCost,
            RemainingMana  = currentMana,
            HealthRestored = healthRestored,
        };
    }

    private static UseAbilityHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
