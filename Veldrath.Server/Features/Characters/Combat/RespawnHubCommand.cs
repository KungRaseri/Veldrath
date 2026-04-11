using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that handles a player choosing to respawn after death in normal mode.
/// Verifies the character is dead (HP at or below zero) and resets them to a safe state.
/// </summary>
/// <param name="CharacterId">The character requesting respawn.</param>
public record RespawnHubCommand(Guid CharacterId)
    : IRequest<RespawnHubResult>;

/// <summary>Result returned by <see cref="RespawnHubCommandHandler"/>.</summary>
public record RespawnHubResult
{
    /// <summary>Gets a value indicating whether the respawn succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's HP after respawning.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's mana after respawning.</summary>
    public int CurrentMana { get; init; }
}

/// <summary>
/// Handles <see cref="RespawnHubCommand"/> by verifying the character was killed (HP ≤ 0),
/// removing any lingering combat session, and restoring the character to minimum viable health.
/// </summary>
public class RespawnHubCommandHandler
    : IRequestHandler<RespawnHubCommand, RespawnHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<RespawnHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="RespawnHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="logger">Logger instance.</param>
    public RespawnHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<RespawnHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the respawn outcome.</summary>
    /// <param name="request">The command containing the character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="RespawnHubResult"/> describing the outcome.</returns>
    public async Task<RespawnHubResult> Handle(
        RespawnHubCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character not found" };

        if (entity.DeletedAt.HasValue)
            return new RespawnHubResult { Success = false, ErrorMessage = "Hardcore character cannot respawn" };

        var attrs = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        attrs.TryGetValue("CurrentHealth", out int currentHp);

        if (currentHp > 0)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character is not dead" };

        int maxHealth = attrs.TryGetValue("MaxHealth", out var mh) ? mh : entity.Level * 10;
        int maxMana   = attrs.TryGetValue("MaxMana",   out var mm) ? mm : entity.Level * 5;

        // Restore to 25% of max HP and full mana.
        int respawnHp   = Math.Max(1, maxHealth / 4);
        int respawnMana = maxMana;

        attrs["CurrentHealth"] = respawnHp;
        attrs["CurrentMana"]   = respawnMana;

        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        CombatSessionStore.Remove(request.CharacterId);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} respawned with {Hp}/{MaxHp} HP",
            request.CharacterId.ToString()[..8], respawnHp, maxHealth);

        return new RespawnHubResult
        {
            Success       = true,
            CurrentHealth = respawnHp,
            CurrentMana   = respawnMana,
        };
    }
}
