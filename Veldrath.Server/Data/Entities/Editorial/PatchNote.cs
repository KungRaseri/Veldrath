namespace Veldrath.Server.Data.Entities.Editorial;

/// <summary>A versioned patch note entry for the game changelog.</summary>
public class PatchNote
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the URL-friendly slug (unique, e.g. "patch-1-2-0").</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the full Markdown content of the patch note.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the short plain-text summary shown in list views.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the game version this patch note applies to (e.g. "1.2.0").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets the publication status.</summary>
    public EditorialStatus Status { get; set; } = EditorialStatus.Draft;

    /// <summary>Gets or sets the UTC timestamp when the entry was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the entry was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when the entry was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the account ID of the author who created this entry.</summary>
    public Guid AuthorAccountId { get; set; }
}
