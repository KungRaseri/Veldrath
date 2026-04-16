namespace Veldrath.Server.Data.Entities.Editorial;

/// <summary>A site-wide announcement shown as a dismissible banner on Veldrath.Web.</summary>
public class EditorialAnnouncement
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the short display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain-text body of the announcement.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the publication status.</summary>
    public EditorialStatus Status { get; set; } = EditorialStatus.Draft;

    /// <summary>Gets or sets the UTC timestamp when the announcement was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the announcement was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when the announcement was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the account ID of the author who created this announcement.</summary>
    public Guid AuthorAccountId { get; set; }
}
