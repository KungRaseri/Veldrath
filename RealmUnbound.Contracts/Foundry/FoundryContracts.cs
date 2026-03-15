namespace RealmUnbound.Contracts.Foundry;

// ── Requests ──────────────────────────────────────────────────────────────

public record CreateSubmissionRequest(
    /// <summary>See <c>FoundryContentType</c> enum values: Item, Spell, Ability, Npc, Quest, Recipe, LootTable.</summary>
    string ContentType,
    string Title,
    /// <summary>JSON payload matching the shape of the chosen content type.</summary>
    string Payload,
    /// <summary>Optional markdown description — balance rationale, lore context, etc.</summary>
    string? Description = null);

public record VoteRequest(
    /// <summary>+1 for upvote, -1 for downvote.</summary>
    int Value);

public record ReviewRequest(
    bool Approved,
    string? Notes = null);

// ── Responses ─────────────────────────────────────────────────────────────

public record FoundrySubmissionSummaryDto(
    Guid     Id,
    string   ContentType,
    string   Title,
    string   Status,
    string   SubmitterName,
    int      VoteScore,
    DateTimeOffset CreatedAt);

/// <summary>Paged wrapper returned by the submission list endpoint.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public record FoundryNotificationDto(
    Guid           Id,
    Guid           SubmissionId,
    string         SubmissionTitle,
    string         Message,
    bool           IsRead,
    DateTimeOffset CreatedAt);

public record FoundrySubmissionDto(
    Guid     Id,
    string   ContentType,
    string   Title,
    string?  Description,
    string   Payload,
    string   Status,
    string   SubmitterName,
    Guid     SubmitterId,
    string?  ReviewerName,
    string?  ReviewNotes,
    int      VoteScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);
