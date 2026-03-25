namespace RealmUnbound.Server.Data.Entities;

/// <summary>A news or system announcement visible in the client news panel.</summary>
public class Announcement
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the short display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the main body text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the category tag (e.g. "News", "Update", "Maintenance", "Event").</summary>
    public string Category { get; set; } = "News";

    /// <summary>
    /// When <see langword="true"/> this announcement is pinned at the top of the list
    /// regardless of publish date.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the announcement was published.</summary>
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the optional UTC expiry timestamp.
    /// Expired entries are excluded from client-facing queries.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When <see langword="false"/> the announcement is hidden from all queries
    /// regardless of other fields.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
