namespace Veldrath.Contracts.Foundry;

// Requests
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

// Responses
public record FoundrySubmissionSummaryDto(
    Guid Id,
    string ContentType,
    string Title,
    string Status,
    string SubmitterName,
    int VoteScore,
    DateTimeOffset CreatedAt);

/// <summary>Paged wrapper returned by the submission list endpoint.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>An in-app notification about a review outcome.</summary>
/// <param name="Id">Notification identifier.</param>
/// <param name="Kind"><c>Submission</c> or <c>MapReview</c>.</param>
/// <param name="SubmissionId">The submission, when the notification is about one.</param>
/// <param name="MapId">The map, when the notification is about a map review.</param>
/// <param name="Title">Snapshot of the submission title or map name.</param>
/// <param name="Message">Human-readable outcome, including any review notes.</param>
/// <param name="IsRead">Whether the recipient has read it.</param>
/// <param name="CreatedAt">When it was created.</param>
public record FoundryNotificationDto(
    Guid Id,
    string Kind,
    Guid? SubmissionId,
    string? MapId,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <summary>Unread notification total for the calling account.</summary>
/// <param name="Count">How many notifications the caller has not read yet.</param>
/// <remarks>
/// The portal shell renders a badge on every Foundry page. A dedicated count keeps that from materialising
/// the whole inbox, and the shared predicate keeps the badge honest against the list it summarises.
/// </remarks>
public record FoundryUnreadNotificationCountDto(int Count);

/// <summary>One community submission, as the portal reads it.</summary>
/// <param name="PromotedSlug">
/// Slug of the catalog entity an approval wrote, or <c>null</c> when the submission has not been promoted.
/// </param>
public record FoundrySubmissionDto(
    Guid Id,
    string ContentType,
    string Title,
    string? Description,
    string Payload,
    string Status,
    string SubmitterName,
    Guid SubmitterId,
    string? ReviewerName,
    string? ReviewNotes,
    int VoteScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt,
    string? PromotedSlug = null);
