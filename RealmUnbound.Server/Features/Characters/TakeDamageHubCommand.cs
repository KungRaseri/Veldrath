using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Characters;

/// <summary>
/// Hub command that applies damage to a character's current health stored in the attributes blob.
/// Health is clamped to zero — the character may reach 0 HP (dead) but cannot go negative.
/// If the character has no stored health pool the handler initialises one from the character level
/// (10 HP per level) before applying damage.
/// </summary>
public record TakeDamageHubCommand : IRequest<TakeDamageHubResult>
{
    /// <summary>Gets the ID of the character taking damage.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>Gets the amount of damage to inflict. Must be a positive value.</summary>
    public required int DamageAmount { get; init; }

    /// <summary>Gets an optional label describing the damage source (e.g. <c>"Enemy"</c>, <c>"Trap"</c>).</summary>
    public string? Source { get; init; }
}

/// <summary>Result returned by <see cref="TakeDamageHubCommandHandler"/>.</summary>
public record TakeDamageHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's current health after the damage was applied.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's maximum health.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets a value indicating whether the character reached 0 HP.</summary>
    public bool IsDead { get; init; }
}

/// <summary>
/// Handles <see cref="TakeDamageHubCommand"/> by loading the server-side character,
/// deserialising the attributes JSON blob, reducing <c>CurrentHealth</c> by the damage amount
/// (clamped to zero), persisting the updated blob, and returning the outcome.
/// </summary>
public class TakeDamageHubCommandHandler : IRequestHandler<TakeDamageHubCommand, TakeDamageHubResult>
{
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth     = "MaxHealth";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<TakeDamageHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="TakeDamageHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public TakeDamageHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<TakeDamageHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the damage outcome.</summary>
    /// <param name="request">The command containing the character ID and damage amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TakeDamageHubResult"/> describing the outcome.</returns>
    public async Task<TakeDamageHubResult> Handle(
        TakeDamageHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.DamageAmount <= 0)
            return new TakeDamageHubResult { Success = false, ErrorMessage = "Damage amount must be positive" };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new TakeDamageHubResult { Success = false, ErrorMessage = $"Character {request.CharacterId} not found" };

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

        // Derive max health from level when not explicitly stored (10 HP per level)
        var maxHealth     = attrs.TryGetValue(KeyMaxHealth, out var mh) ? mh : character.Level * 10;
        var currentHealth = attrs.TryGetValue(KeyCurrentHealth, out var ch) ? ch : maxHealth;

        var newHealth = Math.Max(0, currentHealth - request.DamageAmount);

        attrs[KeyCurrentHealth] = newHealth;
        attrs[KeyMaxHealth]     = maxHealth;

        character.Attributes   = JsonSerializer.Serialize(attrs);
        character.LastPlayedAt = DateTimeOffset.UtcNow;

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} took {Damage} damage from {Source}. HP: {Old} → {New}/{Max}",
            request.CharacterId, request.DamageAmount, request.Source ?? "Unknown",
            currentHealth, newHealth, maxHealth);

        return new TakeDamageHubResult
        {
            Success       = true,
            CurrentHealth = newHealth,
            MaxHealth     = maxHealth,
            IsDead        = newHealth == 0,
        };
    }
}
