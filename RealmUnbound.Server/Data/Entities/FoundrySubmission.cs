namespace RealmUnbound.Server.Data.Entities;

/// <summary>Type of game content being submitted.</summary>
public enum FoundryContentType
{
    Item,
    Spell,
    Ability,
    Npc,
    Quest,
    Recipe,
    LootTable,
}

/// <summary>Current review state of a <see cref="FoundrySubmission"/>.</summary>
public enum FoundrySubmissionStatus
{
    Pending,
    Approved,
    Rejected,
    Curated,
}

/// <summary>
/// A community-submitted piece of game content awaiting review.
/// Payload is stored as JSONB so any content type can be represented without
/// per-type tables at this stage.
/// </summary>
public class FoundrySubmission
{
    public Guid Id { get; set; }

    public Guid SubmitterId { get; set; }
    public PlayerAccount Submitter { get; set; } = null!;

    public FoundryContentType ContentType { get; set; }

    /// <summary>Human-readable title summarising the submission.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional markdown description giving context, balance rationale, etc.</summary>
    public string? Description { get; set; }

    /// <summary>Serialised content payload (JSONB in Postgres).</summary>
    public string Payload { get; set; } = "{}";

    public FoundrySubmissionStatus Status { get; set; } = FoundrySubmissionStatus.Pending;

    /// <summary>Notes left by the Curator/Archivist during review.</summary>
    public string? ReviewNotes { get; set; }

    public Guid? ReviewerId { get; set; }
    public PlayerAccount? Reviewer { get; set; }

    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }

    public ICollection<FoundryVote> Votes { get; set; } = [];
}
