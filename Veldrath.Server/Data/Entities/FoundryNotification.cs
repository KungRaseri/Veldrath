namespace Veldrath.Server.Data.Entities;

/// <summary>What a Foundry notification is about.</summary>
public enum FoundryNotificationKind
{
    /// <summary>A community submission was reviewed.</summary>
    Submission = 0,

    /// <summary>A map was reviewed.</summary>
    MapReview = 1,
}

/// <summary>
/// An in-app notification about a review outcome: a submission decision, or a map decision.
/// </summary>
public class FoundryNotification
{
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }
    public PlayerAccount Recipient { get; set; } = null!;

    /// <summary>What this notification is about; decides which of the references below is set.</summary>
    public FoundryNotificationKind Kind { get; set; } = FoundryNotificationKind.Submission;

    /// <summary>
    /// The submission this is about, when <see cref="Kind"/> is <see cref="FoundryNotificationKind.Submission"/>.
    /// </summary>
    public Guid? SubmissionId { get; set; }

    /// <summary>The submission navigation, present only for a submission notification.</summary>
    public FoundrySubmission? Submission { get; set; }

    /// <summary>
    /// The map this is about, when <see cref="Kind"/> is <see cref="FoundryNotificationKind.MapReview"/>.
    /// </summary>
    /// <remarks>
    /// A plain id rather than a relationship on purpose: maps live in the content database, so there is no key
    /// this context could enforce. A deleted map leaves the notification readable rather than cascading it away.
    /// </remarks>
    public string? MapId { get; set; }

    /// <summary>
    /// A snapshot of what the notification is about — the submission title or the map name.
    /// </summary>
    /// <remarks>
    /// Stored rather than joined so a renamed map does not rewrite history, and so the inbox never has to join a
    /// navigation that may be absent.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    /// <summary>The message shown in the inbox.</summary>
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
