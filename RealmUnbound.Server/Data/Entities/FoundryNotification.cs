namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// An in-app notification sent to a submitter when their submission is reviewed.
/// </summary>
public class FoundryNotification
{
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }
    public PlayerAccount Recipient { get; set; } = null!;

    public Guid SubmissionId { get; set; }
    public FoundrySubmission Submission { get; set; } = null!;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
