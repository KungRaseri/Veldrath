using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// One character fighting a shared enemy, as the whole zone may see them.
/// </summary>
/// <remarks>
/// Deliberately contains no rewards or inventory: this snapshot is broadcast to everyone at the
/// location, while per-character XP and gold stay on the caller-only <c>CombatTurn</c>.
/// </remarks>
public sealed class CombatParticipantDto
{
    /// <summary>Gets the participant's character id.</summary>
    public Guid CharacterId { get; init; }

    /// <summary>Gets the participant's name, or <see langword="null"/> when the character row is gone.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the participant's current health.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the participant's maximum health.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets the total damage this participant has dealt to the enemy.</summary>
    public int DamageDealt { get; init; }

    /// <summary>Gets this participant's share of the damage dealt, 0–100, rounded.</summary>
    public int ContributionPercent { get; init; }
}

/// <summary>
/// The shared state of one fight: the enemy and everyone who has hit it.
/// </summary>
public sealed class CombatSnapshotDto
{
    /// <summary>Gets the enemy instance id.</summary>
    public Guid EnemyId { get; init; }

    /// <summary>Gets the enemy's display name.</summary>
    public string EnemyName { get; init; } = string.Empty;

    /// <summary>Gets the archetype slug the enemy was spawned from, which is how the client addresses its portrait.</summary>
    public string EnemyArchetypeSlug { get; init; } = string.Empty;

    /// <summary>Gets the portrait frame tier (<c>boss</c>, <c>elite</c> or <c>regular</c>); display metadata only.</summary>
    public string EnemyTier { get; init; } = "regular";

    /// <summary>Gets the enemy's combat level.</summary>
    public int EnemyLevel { get; init; }

    /// <summary>Gets the enemy's current health.</summary>
    public int EnemyCurrentHealth { get; init; }

    /// <summary>Gets the enemy's maximum health.</summary>
    public int EnemyMaxHealth { get; init; }

    /// <summary>Gets the names of the abilities the enemy can use.</summary>
    public IReadOnlyList<string> EnemyAbilityNames { get; init; } = [];

    /// <summary>Gets the participants, highest contributor first.</summary>
    public IReadOnlyList<CombatParticipantDto> Participants { get; init; } = [];
}

/// <summary>
/// Builds the shared snapshot of a live fight so every client at the location can render it.
/// </summary>
/// <param name="EnemyId">The enemy instance to snapshot.</param>
public record GetCombatSnapshotQuery(Guid EnemyId) : IRequest<CombatSnapshotDto?>;

/// <summary>
/// Handles <see cref="GetCombatSnapshotQuery"/> by reading the live enemy and each participant's
/// health from their attributes blob.
/// </summary>
public class GetCombatSnapshotQueryHandler
    : IRequestHandler<GetCombatSnapshotQuery, CombatSnapshotDto?>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IEnemyInstanceStore _enemies;
    private readonly ICharacterRepository _characters;
    private readonly ILogger<GetCombatSnapshotQueryHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetCombatSnapshotQueryHandler"/>.</summary>
    /// <param name="enemies">Live enemy instance store.</param>
    /// <param name="characters">Character repository, used for names and current health.</param>
    /// <param name="logger">Logger instance.</param>
    public GetCombatSnapshotQueryHandler(
        IEnemyInstanceStore enemies,
        ICharacterRepository characters,
        ILogger<GetCombatSnapshotQueryHandler> logger)
    {
        _enemies = enemies;
        _characters = characters;
        _logger = logger;
    }

    /// <summary>Returns the snapshot, or <see langword="null"/> when the enemy no longer exists.</summary>
    /// <param name="request">The query carrying the enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shared fight state, or <see langword="null"/> when the enemy has been removed.</returns>
    public async Task<CombatSnapshotDto?> Handle(
        GetCombatSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var enemy = await _enemies.TryGetAsync(request.EnemyId, cancellationToken);
        if (enemy is null)
            return null;

        var totalDamage = enemy.DamageContributions.Values.Sum();
        var participants = new List<CombatParticipantDto>(enemy.Participants.Count);

        foreach (var characterId in enemy.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var damage = enemy.DamageContributions.TryGetValue(characterId, out var dealt) ? dealt : 0;
            var character = await LoadCharacterAsync(characterId, cancellationToken);
            var attributes = ReadAttributes(character?.Attributes, characterId);

            var maxHealth = attributes.TryGetValue("MaxHealth", out var mh) ? mh : 0;
            var maxManaHealthFallback = character is null ? 0 : character.Level * 10;
            if (maxHealth == 0) maxHealth = maxManaHealthFallback;

            participants.Add(new CombatParticipantDto
            {
                CharacterId = characterId,
                Name = character?.Name,
                CurrentHealth = attributes.TryGetValue("CurrentHealth", out var ch) ? ch : maxHealth,
                MaxHealth = maxHealth,
                DamageDealt = damage,
                ContributionPercent = totalDamage > 0 ? (int)Math.Round(damage * 100.0 / totalDamage) : 0,
            });
        }

        var abilityNames = enemy.Template.Abilities
            .Select(ability => string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Name : ability.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return new CombatSnapshotDto
        {
            EnemyId = enemy.Id,
            EnemyName = enemy.Name,
            EnemyArchetypeSlug = enemy.ArchetypeSlug,
            EnemyTier = CombatHelpers.ResolveTier(enemy.Template),
            EnemyLevel = enemy.Level,
            EnemyCurrentHealth = enemy.CurrentHealth,
            EnemyMaxHealth = enemy.MaxHealth,
            EnemyAbilityNames = abilityNames,
            Participants = participants
                .OrderByDescending(participant => participant.DamageDealt)
                .ThenBy(participant => participant.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private async Task<Data.Entities.Character?> LoadCharacterAsync(Guid characterId, CancellationToken cancellationToken)
    {
        try
        {
            return await _characters.GetByIdAsync(characterId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One unreadable participant must not blank the whole snapshot: the row is reported
            // without a name and the reason is logged.
            _logger.LogError(ex, "Failed to load participant {CharacterId} for the combat snapshot.", characterId);
            return null;
        }
    }

    private Dictionary<string, int> ReadAttributes(string? attributesJson, Guid characterId)
    {
        if (string.IsNullOrWhiteSpace(attributesJson) || attributesJson == "{}")
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(attributesJson, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise attributes for participant {CharacterId}; reporting them at zero health.",
                characterId);
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
