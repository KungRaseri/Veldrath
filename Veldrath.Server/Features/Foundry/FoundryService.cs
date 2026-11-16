using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Features.Foundry;

/// <summary>The community submission queue, and the review decision that lands approved work in the catalog.</summary>
/// <param name="db">Application context: submissions, votes, notifications.</param>
/// <param name="notifications">The one place a notification row is written.</param>
/// <param name="content">
/// The content write path. An approval promotes the payload through it, so an approved submission is content,
/// not a row with a status.
/// </param>
public class FoundryService(
    ApplicationDbContext db,
    FoundryNotificationWriter notifications,
    ContentWriteService content)
{
    // Submissions
    public async Task<(FoundrySubmissionDto? Dto, string? Error)> CreateSubmissionAsync(
        CreateSubmissionRequest request, Guid submitterId, CancellationToken ct)
    {
        if (!Enum.TryParse<FoundryContentType>(request.ContentType, ignoreCase: true, out var contentType))
            return (null, $"Unknown content type: {request.ContentType}");

        var submission = new FoundrySubmission
        {
            Id = Guid.NewGuid(),
            SubmitterId = submitterId,
            ContentType = contentType,
            Title = request.Title,
            Description = request.Description,
            Payload = request.Payload,
            Status = FoundrySubmissionStatus.Pending,
        };

        db.FoundrySubmissions.Add(submission);
        await db.SaveChangesAsync(ct);

        await db.Entry(submission).Reference(s => s.Submitter).LoadAsync(ct);
        return (MapToDto(submission), null);
    }

    public async Task<PagedResult<FoundrySubmissionSummaryDto>> ListSubmissionsAsync(
        string? status, string? contentType, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = db.FoundrySubmissions
            .Include(s => s.Submitter)
            .Include(s => s.Votes)
            .AsNoTracking();

        if (status is not null && Enum.TryParse<FoundrySubmissionStatus>(status, ignoreCase: true, out var st))
            query = query.Where(s => s.Status == st);

        if (contentType is not null && Enum.TryParse<FoundryContentType>(contentType, ignoreCase: true, out var ct2))
            query = query.Where(s => s.ContentType == ct2);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Title.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FoundrySubmissionSummaryDto>(items.Select(MapToSummary).ToList(), total, page, pageSize);
    }

    public async Task<FoundrySubmissionDto?> GetSubmissionAsync(Guid id, CancellationToken ct)
    {
        var submission = await db.FoundrySubmissions
            .Include(s => s.Submitter)
            .Include(s => s.Reviewer)
            .Include(s => s.Votes)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return submission is null ? null : MapToDto(submission);
    }

    // Voting
    public async Task<(FoundrySubmissionSummaryDto? Dto, string? Error)> VoteAsync(
        Guid submissionId, Guid voterId, int value, CancellationToken ct)
    {
        if (value is not (1 or -1))
            return (null, "Vote value must be +1 or -1.");

        var submission = await db.FoundrySubmissions
            .Include(s => s.Submitter)
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission is null)
            return (null, "Submission not found.");

        var existing = submission.Votes.FirstOrDefault(v => v.VoterId == voterId);
        if (existing is not null)
        {
            existing.Value = value; // change vote
        }
        else
        {
            db.FoundryVotes.Add(new FoundryVote
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                VoterId = voterId,
                Value = value,
            });
        }

        await db.SaveChangesAsync(ct);
        return (MapToSummary(submission), null);
    }

    // Review
    public async Task<(FoundrySubmissionDto? Dto, string? Error)> ReviewAsync(
        Guid submissionId, Guid reviewerId, ReviewRequest request, CancellationToken ct)
    {
        var submission = await db.FoundrySubmissions
            .Include(s => s.Submitter)
            .Include(s => s.Reviewer)
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission is null)
            return (null, "Submission not found.");

        string? promotedSlug = null;

        if (request.Approved)
        {
            // Approval is the gate, so the payload has to land in the catalog first. Flipping the status and
            // promoting afterwards would leave an "approved" row with no content behind it when the write fails.
            var promotion = await content.CreateAsync(
                submission.ContentType.ToString().ToLowerInvariant(),
                submission.Payload,
                ct
            );

            if (promotion.Error is not null)
                return (null, promotion.Error);

            promotedSlug = promotion.Slug;
            submission.PromotedSlug = promotedSlug;
        }

        submission.Status = request.Approved
            ? FoundrySubmissionStatus.Approved
            : FoundrySubmissionStatus.Rejected;
        submission.ReviewerId = reviewerId;
        submission.ReviewNotes = request.Notes;
        submission.ReviewedAt = DateTimeOffset.UtcNow;
        submission.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await notifications.NotifyAsync(
            submission.SubmitterId,
            FoundryNotificationKind.Submission,
            submission.Title,
            request.Approved
                ? $"Your submission \"{submission.Title}\" was approved and published as {promotedSlug}."
                : $"Your submission \"{submission.Title}\" was rejected. {request.Notes}",
            submissionId: submissionId,
            cancellationToken: ct);

        await db.Entry(submission).Reference(s => s.Reviewer).LoadAsync(ct);
        return (MapToDto(submission), null);
    }

    // Notifications
    public async Task<IReadOnlyList<FoundryNotificationDto>> GetNotificationsAsync(Guid accountId, CancellationToken ct) =>
        await db.FoundryNotifications
            .AsNoTracking()
            .Where(n => n.RecipientId == accountId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new FoundryNotificationDto(
                n.Id,
                n.Kind.ToString(),
                n.SubmissionId,
                n.MapId,
                n.Title,
                n.Message,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Counts the caller's unread notifications.
    /// </summary>
    /// <param name="accountId">The account whose inbox is counted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The predicate is the same one <see cref="GetNotificationsAsync"/> filters on, so the badge cannot
    /// disagree with the list it summarises. Counting in the database also keeps this cheap enough to call
    /// from the portal layout on every page.
    /// </remarks>
    public Task<int> CountUnreadAsync(Guid accountId, CancellationToken ct) =>
        db.FoundryNotifications.CountAsync(n => n.RecipientId == accountId && !n.IsRead, ct);

    public async Task<bool> MarkNotificationReadAsync(Guid notificationId, Guid accountId, CancellationToken ct)
    {
        var notification = await db.FoundryNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == accountId, ct);

        if (notification is null) return false;

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // Mapping helpers
    private static FoundrySubmissionSummaryDto MapToSummary(FoundrySubmission s) => new(
        s.Id,
        s.ContentType.ToString(),
        s.Title,
        s.Status.ToString(),
        s.Submitter?.UserName ?? "?",
        s.Votes.Sum(v => v.Value),
        s.CreatedAt);

    private static FoundrySubmissionDto MapToDto(FoundrySubmission s) => new(
        s.Id,
        s.ContentType.ToString(),
        s.Title,
        s.Description,
        s.Payload,
        s.Status.ToString(),
        s.Submitter?.UserName ?? "?",
        s.SubmitterId,
        s.Reviewer?.UserName,
        s.ReviewNotes,
        s.Votes.Sum(v => v.Value),
        s.CreatedAt,
        s.UpdatedAt,
        s.ReviewedAt,
        s.PromotedSlug);
}
