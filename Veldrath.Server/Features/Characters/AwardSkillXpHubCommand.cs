using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that awards skill XP to a character for a specific skill.
/// XP and rank are stored as <c>Skill_{SkillId}_XP</c> and <c>Skill_{SkillId}_Rank</c>
/// keys within the character's JSON attributes blob.
/// A new rank is earned every <see cref="AwardSkillXpHubCommandHandler.XpPerRank"/> XP.
/// </summary>
public record AwardSkillXpHubCommand : IRequest<AwardSkillXpHubResult>
{
    /// <summary>Gets the ID of the character that earns skill XP.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>Gets the skill identifier (e.g. <c>"swordsmanship"</c>, <c>"herbalism"</c>).</summary>
    public required string SkillId { get; init; }

    /// <summary>Gets the amount of XP to award. Must be positive.</summary>
    public required int Amount { get; init; }

    /// <summary>
    /// Gets the character to award the experience to when the caller already holds it and owns the write.
    /// </summary>
    /// <remarks>
    /// Crafting owns the character-row write, so it hands over the character it already loaded. The handler then
    /// applies the award to that instance's attributes blob in memory and does not persist it; the craft writes
    /// the row once, with both the crafted bag (<c>InventoryBlob</c>) and the award (<c>Attributes</c>). The engine's
    /// inventory service is not on the craft path, so there is no second store to overwrite.
    /// </remarks>
    internal Character? LoadedCharacter { get; init; }
}

/// <summary>Result returned by <see cref="AwardSkillXpHubCommandHandler"/>.</summary>
public record AwardSkillXpHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the skill identifier that received XP.</summary>
    public string SkillId { get; init; } = string.Empty;

    /// <summary>Gets the total accumulated XP for the skill after this award.</summary>
    public int TotalXp { get; init; }

    /// <summary>Gets the current rank of the skill after this award.</summary>
    public int CurrentRank { get; init; }

    /// <summary>Gets a value indicating whether this award caused a rank-up.</summary>
    public bool RankedUp { get; init; }
}

/// <summary>
/// Handles <see cref="AwardSkillXpHubCommand"/> by resolving the character (loading it unless the caller handed
/// one over), deserialising the attributes JSON blob, adding the specified XP to the named skill, deriving the
/// new rank, persisting the result when it owns the write, and returning whether a rank-up occurred.
/// </summary>
public class AwardSkillXpHubCommandHandler
    : IRequestHandler<AwardSkillXpHubCommand, AwardSkillXpHubResult>
{
    /// <summary>Amount of total XP required to advance one rank.</summary>
    internal const int XpPerRank = 100;

    private const string KeyPrefix = "Skill_";
    private const string XpSuffix = "_XP";
    private const string RankSuffix = "_Rank";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<AwardSkillXpHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="AwardSkillXpHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public AwardSkillXpHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<AwardSkillXpHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the skill XP outcome.</summary>
    /// <param name="request">The command containing character ID, skill ID, and XP amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AwardSkillXpHubResult"/> describing the outcome.</returns>
    /// <remarks>
    /// When <see cref="AwardSkillXpHubCommand.LoadedCharacter"/> is set the award is applied to that instance and
    /// left unsaved for the caller; otherwise the character is loaded and persisted here.
    /// </remarks>
    public async Task<AwardSkillXpHubResult> Handle(
        AwardSkillXpHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SkillId))
            return Fail("SkillId cannot be empty.");

        if (request.Amount <= 0)
            return Fail("Amount must be positive.");

        var ownsTheWrite = request.LoadedCharacter is null;
        var character = request.LoadedCharacter
            ?? await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null || character.Id != request.CharacterId)
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

        var xpKey = $"{KeyPrefix}{request.SkillId}{XpSuffix}";
        var rankKey = $"{KeyPrefix}{request.SkillId}{RankSuffix}";

        var previousXp = attrs.TryGetValue(xpKey, out var px) ? px : 0;
        var previousRank = attrs.TryGetValue(rankKey, out var pr) ? pr : 0;

        var newXp = previousXp + request.Amount;
        var newRank = newXp / XpPerRank;
        var rankedUp = newRank > previousRank;

        attrs[xpKey] = newXp;
        attrs[rankKey] = newRank;

        character.Attributes = JsonSerializer.Serialize(attrs);
        if (ownsTheWrite)
            await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} earned {Amount} XP in skill {SkillId}: total {TotalXp}, rank {Rank}{RankedUp}",
            request.CharacterId, request.Amount, request.SkillId, newXp, newRank,
            rankedUp ? " (RANK UP)" : string.Empty);

        return new AwardSkillXpHubResult
        {
            Success = true,
            SkillId = request.SkillId,
            TotalXp = newXp,
            CurrentRank = newRank,
            RankedUp = rankedUp,
        };
    }

    private static AwardSkillXpHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
