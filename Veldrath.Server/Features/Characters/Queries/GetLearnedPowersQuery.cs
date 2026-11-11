using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace Veldrath.Server.Features.Characters.Queries;

/// <summary>
/// One learned power, resolved against the power catalog so the client can render a name, a cost and
/// an effect instead of a bare slug.
/// </summary>
/// <remarks>
/// A slug with no active catalog entry is returned with <see cref="Resolved"/> set to
/// <see langword="false"/> and every descriptive field left empty. The client shows that as an
/// explicit unknown entry, which is the only place a slug is allowed to reach the screen — the
/// alternative, silently dropping the entry, hides content errors.
/// </remarks>
public sealed class LearnedPowerDto
{
    /// <summary>Gets the power slug, exactly as stored in the character's abilities blob.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a catalog entry was found for <see cref="Slug"/>.</summary>
    public bool Resolved { get; init; }

    /// <summary>Gets the player-facing name, or <see langword="null"/> when unresolved.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the authored description, or <see langword="null"/> when unresolved.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the magical school or tradition (e.g. <c>"fire"</c>), or <see langword="null"/> for non-magical powers.</summary>
    public string? School { get; init; }

    /// <summary>Gets how the power is acquired or activated (e.g. <c>"Talent"</c>, <c>"Spell"</c>).</summary>
    public string PowerType { get; init; } = string.Empty;

    /// <summary>Gets the primary effect category (e.g. <c>"Damage"</c>, <c>"Heal"</c>), or <c>"None"</c>.</summary>
    public string EffectType { get; init; } = string.Empty;

    /// <summary>Gets the mana or resource cost of using the power; zero means free.</summary>
    public int ManaCost { get; init; }

    /// <summary>Gets the cooldown in turns; zero means it can be used every turn.</summary>
    public int Cooldown { get; init; }

    /// <summary>Gets the base damage dice notation (e.g. <c>"2d6"</c>), or <see langword="null"/> when the power does not deal damage.</summary>
    public string? BaseDamage { get; init; }

    /// <summary>Gets the base effect value for spell-style powers (e.g. <c>"1d8+WIS"</c>), or <see langword="null"/>.</summary>
    public string? BaseEffectValue { get; init; }

    /// <summary>Gets the damage type applied (e.g. <c>"fire"</c>), or <see langword="null"/>.</summary>
    public string? DamageType { get; init; }

    /// <summary>Gets the range in world units, or <see langword="null"/> for melee and self powers.</summary>
    public int? Range { get; init; }

    /// <summary>Gets the duration in turns, or <see langword="null"/> for instant effects.</summary>
    public int? Duration { get; init; }

    /// <summary>Gets the maximum number of simultaneous targets, or <see langword="null"/> when unlimited or single-target by default.</summary>
    public int? MaxTargets { get; init; }

    /// <summary>Gets the power tier (1–5).</summary>
    public int Tier { get; init; }

    /// <summary>Gets the character level required to learn or use the power.</summary>
    public int RequiredLevel { get; init; }
}

/// <summary>
/// Resolves a character's learned power slugs into catalog entries for the character snapshot.
/// </summary>
/// <param name="Slugs">The learned slugs, in the order the character learned them. Blanks are ignored and duplicates collapse.</param>
public record GetLearnedPowersQuery(IReadOnlyList<string> Slugs) : IRequest<IReadOnlyList<LearnedPowerDto>>;

/// <summary>
/// Handles <see cref="GetLearnedPowersQuery"/> by looking each slug up in the power catalog.
/// </summary>
public class GetLearnedPowersQueryHandler
    : IRequestHandler<GetLearnedPowersQuery, IReadOnlyList<LearnedPowerDto>>
{
    private readonly IPowerRepository _powers;
    private readonly ILogger<GetLearnedPowersQueryHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetLearnedPowersQueryHandler"/>.</summary>
    /// <param name="powers">Power catalog repository.</param>
    /// <param name="logger">Logger used to report slugs that have no catalog entry.</param>
    public GetLearnedPowersQueryHandler(
        IPowerRepository powers,
        ILogger<GetLearnedPowersQueryHandler> logger)
    {
        _powers = powers;
        _logger = logger;
    }

    /// <summary>Resolves every distinct slug, preserving the order the character learned them in.</summary>
    /// <param name="request">The query carrying the learned slugs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per distinct slug, with unresolved species marked rather than dropped.</returns>
    public async Task<IReadOnlyList<LearnedPowerDto>> Handle(
        GetLearnedPowersQuery request,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<LearnedPowerDto>();

        foreach (var candidate in request.Slugs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slug = candidate?.Trim();
            if (string.IsNullOrEmpty(slug) || !seen.Add(slug))
                continue;

            resolved.Add(await ResolveAsync(slug, cancellationToken));
        }

        return resolved;
    }

    private async Task<LearnedPowerDto> ResolveAsync(string slug, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Power? power;
        try
        {
            power = await _powers.GetBySlugAsync(slug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A catalog read failure must not cost the player their character selection; the entry
            // comes back unresolved and the log records which slug and why.
            _logger.LogError(ex, "Power catalog lookup failed for '{Slug}'; sending it unresolved.", slug);
            return new LearnedPowerDto { Slug = slug };
        }

        if (power is null)
        {
            _logger.LogWarning(
                "Character knows power '{Slug}' but the catalog has no entry for it; sending it unresolved.",
                slug);
            return new LearnedPowerDto { Slug = slug };
        }

        return new LearnedPowerDto
        {
            Slug = slug,
            Resolved = true,
            DisplayName = string.IsNullOrWhiteSpace(power.DisplayName) ? power.Name : power.DisplayName,
            Description = power.Description,
            School = power.School,
            PowerType = power.Type.ToString(),
            EffectType = power.EffectType.ToString(),
            ManaCost = power.ManaCost,
            Cooldown = power.Cooldown,
            BaseDamage = power.BaseDamage,
            BaseEffectValue = power.BaseEffectValue,
            DamageType = power.DamageType,
            Range = power.Range,
            Duration = power.Duration,
            MaxTargets = power.MaxTargets,
            Tier = power.Tier,
            RequiredLevel = power.RequiredLevel,
        };
    }
}
