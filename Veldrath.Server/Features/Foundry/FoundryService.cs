using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Foundry;

public class FoundryService(ApplicationDbContext db)
{
    // Submissions
    public async Task<(FoundrySubmissionDto? Dto, string? Error)> CreateSubmissionAsync(
        CreateSubmissionRequest request, Guid submitterId, CancellationToken ct)
    {
        if (!Enum.TryParse<FoundryContentType>(request.ContentType, ignoreCase: true, out var contentType))
            return (null, $"Unknown content type: {request.ContentType}");

        var submission = new FoundrySubmission
        {
            Id          = Guid.NewGuid(),
            SubmitterId = submitterId,
            ContentType = contentType,
            Title       = request.Title,
            Description = request.Description,
            Payload     = request.Payload,
            Status      = FoundrySubmissionStatus.Pending,
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
                Id           = Guid.NewGuid(),
                SubmissionId = submissionId,
                VoterId      = voterId,
                Value        = value,
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

        submission.Status     = request.Approved
            ? FoundrySubmissionStatus.Approved
            : FoundrySubmissionStatus.Rejected;
        submission.ReviewerId  = reviewerId;
        submission.ReviewNotes = request.Notes;
        submission.ReviewedAt  = DateTimeOffset.UtcNow;
        submission.UpdatedAt   = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        db.FoundryNotifications.Add(new FoundryNotification
        {
            Id           = Guid.NewGuid(),
            RecipientId  = submission.SubmitterId,
            SubmissionId = submissionId,
            Message      = request.Approved
                ? $"Your submission \"{submission.Title}\" was approved!"
                : $"Your submission \"{submission.Title}\" was rejected. {request.Notes}",
        });
        await db.SaveChangesAsync(ct);

        await db.Entry(submission).Reference(s => s.Reviewer).LoadAsync(ct);
        return (MapToDto(submission), null);
    }

    // Notifications
    public async Task<IReadOnlyList<FoundryNotificationDto>> GetNotificationsAsync(Guid accountId, CancellationToken ct) =>
        await db.FoundryNotifications
            .Include(n => n.Submission)
            .AsNoTracking()
            .Where(n => n.RecipientId == accountId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new FoundryNotificationDto(
                n.Id,
                n.SubmissionId,
                n.Submission!.Title,
                n.Message,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(ct);

    public async Task<bool> MarkNotificationReadAsync(Guid notificationId, Guid accountId, CancellationToken ct)
    {
        var notification = await db.FoundryNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == accountId, ct);

        if (notification is null) return false;

        notification.IsRead = true;
        notification.ReadAt  = DateTimeOffset.UtcNow;
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
        s.ReviewedAt);
}
