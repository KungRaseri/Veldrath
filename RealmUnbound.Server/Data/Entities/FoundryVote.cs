namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// A single +1 or -1 vote cast by a player on a <see cref="FoundrySubmission"/>.
/// Each player may cast at most one vote per submission (enforced by a unique index).
/// </summary>
public class FoundryVote
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }
    public FoundrySubmission Submission { get; set; } = null!;

    public Guid VoterId { get; set; }
    public PlayerAccount Voter { get; set; } = null!;

    /// <summary>+1 upvote or -1 downvote.</summary>
    public int Value { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
