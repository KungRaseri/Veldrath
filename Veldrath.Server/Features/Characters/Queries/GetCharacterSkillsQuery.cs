using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;

namespace Veldrath.Server.Features.Characters.Queries;

/// <summary>
/// One skill a character has practised, resolved against the skill catalog so the client can show a
/// name, a category and progress instead of a storage key.
/// </summary>
/// <remarks>
/// A key with no catalog entry comes back with <see cref="Resolved"/> false; the client names that
/// case explicitly. Progress is expressed in XP into the current rank and XP to the next one, because
/// that is what the sheet draws — the client never divides by a constant of its own.
/// </remarks>
public sealed class CharacterSkillDto
{
    /// <summary>Gets the skill identifier as stored (e.g. <c>"swordsmanship"</c>).</summary>
    public string SkillId { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the skill catalog has an entry for <see cref="SkillId"/>.</summary>
    public bool Resolved { get; init; }

    /// <summary>Gets the player-facing name, or <see langword="null"/> when unresolved.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the authored description, or <see langword="null"/> when unresolved.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the catalog category (e.g. <c>"combat"</c>), or <see langword="null"/> when unresolved.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the governing attribute (e.g. <c>"strength"</c>), or <see langword="null"/> when unresolved.</summary>
    public string? GoverningAttribute { get; init; }

    /// <summary>Gets the current rank.</summary>
    public int Rank { get; init; }

    /// <summary>Gets the lifetime XP earned in this skill.</summary>
    public int TotalXp { get; init; }

    /// <summary>Gets the XP earned toward the current rank, the numerator of the sheet's bar.</summary>
    public int XpIntoRank { get; init; }

    /// <summary>Gets the XP one rank costs, the denominator of the sheet's bar.</summary>
    public int XpToNextRank { get; init; }

    /// <summary>Gets the highest rank the skill allows.</summary>
    public int MaxRank { get; init; }
}

/// <summary>
/// Resolves the skill progress a character has stored in its attributes blob into catalog-backed
/// entries for the character snapshot.
/// </summary>
/// <param name="AttributesJson">
/// The character's attributes blob, which is where skill progress lives: one
/// <c>Skill_{id}_XP</c> and one <c>Skill_{id}_Rank</c> integer per practised skill.
/// </param>
public record GetCharacterSkillsQuery(string? AttributesJson) : IRequest<IReadOnlyList<CharacterSkillDto>>;

/// <summary>
/// Handles <see cref="GetCharacterSkillsQuery"/> by reading the stored skill keys and resolving each
/// one against the skill catalog.
/// </summary>
public class GetCharacterSkillsQueryHandler
    : IRequestHandler<GetCharacterSkillsQuery, IReadOnlyList<CharacterSkillDto>>
{
    private const string KeyPrefix = "Skill_";
    private const string XpSuffix = "_XP";
    private const string RankSuffix = "_Rank";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISkillRepository _skills;
    private readonly ILogger<GetCharacterSkillsQueryHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetCharacterSkillsQueryHandler"/>.</summary>
    /// <param name="skills">Skill catalog repository.</param>
    /// <param name="logger">Logger used to report skill keys that have no catalog entry.</param>
    public GetCharacterSkillsQueryHandler(
        ISkillRepository skills,
        ILogger<GetCharacterSkillsQueryHandler> logger)
    {
        _skills = skills;
        _logger = logger;
    }

    /// <summary>Resolves every practised skill, rank order descending then by name.</summary>
    /// <param name="request">The query carrying the attributes blob.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per practised skill, with unresolved keys marked rather than dropped.</returns>
    public async Task<IReadOnlyList<CharacterSkillDto>> Handle(
        GetCharacterSkillsQuery request,
        CancellationToken cancellationToken)
    {
        var progress = ReadProgress(request.AttributesJson);
        var resolved = new List<CharacterSkillDto>(progress.Count);

        foreach (var totalXpBySkill in progress)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resolved.Add(await ResolveAsync(totalXpBySkill.Key, totalXpBySkill.Value, cancellationToken));
        }

        return resolved
            .OrderByDescending(skill => skill.Rank)
            .ThenBy(skill => skill.Name ?? skill.SkillId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reads the lifetime XP stored for each practised skill. A <c>_Rank</c> key with no matching
    /// <c>_XP</c> key still counts as practised, at zero XP.
    /// </summary>
    private Dictionary<string, int> ReadProgress(string? attributesJson)
    {
        var skills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(attributesJson) || attributesJson == "{}")
            return skills;

        Dictionary<string, int> attributes;
        try
        {
            attributes = JsonSerializer.Deserialize<Dictionary<string, int>>(attributesJson, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise the attributes blob; treating the character as having no skills.");
            return skills;
        }

        foreach (var (key, value) in attributes)
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (key.EndsWith(XpSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var skillId = key[KeyPrefix.Length..^XpSuffix.Length];
                if (skillId.Length > 0) skills[skillId] = value;
            }
            else if (key.EndsWith(RankSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var skillId = key[KeyPrefix.Length..^RankSuffix.Length];
                if (skillId.Length > 0 && !skills.ContainsKey(skillId)) skills[skillId] = 0;
            }
        }

        return skills;
    }

    private async Task<CharacterSkillDto> ResolveAsync(
        string skillId,
        int totalXp,
        CancellationToken cancellationToken)
    {
        var xpPerRank = AwardSkillXpHubCommandHandler.XpPerRank;
        var rank = totalXp / xpPerRank;

        RealmEngine.Shared.Models.SkillDefinition? definition;
        try
        {
            definition = await _skills.GetBySlugAsync(skillId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A catalog read failure must not cost the player their character selection; the skill
            // comes back unresolved and the log records which one and why.
            _logger.LogError(ex, "Skill catalog lookup failed for '{SkillId}'; sending it unresolved.", skillId);
            return Unresolved(skillId, rank, totalXp, xpPerRank);
        }

        if (definition is null)
        {
            _logger.LogWarning(
                "Character has skill '{SkillId}' but the catalog has no entry for it; sending it unresolved.",
                skillId);
            return Unresolved(skillId, rank, totalXp, xpPerRank);
        }

        return new CharacterSkillDto
        {
            SkillId = skillId,
            Resolved = true,
            Name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Name : definition.DisplayName,
            Description = definition.Description,
            Category = definition.Category,
            GoverningAttribute = definition.GoverningAttribute,
            Rank = rank,
            TotalXp = totalXp,
            XpIntoRank = totalXp % xpPerRank,
            XpToNextRank = xpPerRank,
            MaxRank = definition.MaxRank,
        };
    }

    private static CharacterSkillDto Unresolved(string skillId, int rank, int totalXp, int xpPerRank) =>
        new()
        {
            SkillId = skillId,
            Resolved = false,
            Rank = rank,
            TotalXp = totalXp,
            XpIntoRank = totalXp % xpPerRank,
            XpToNextRank = xpPerRank,
            MaxRank = 0,
        };
}
