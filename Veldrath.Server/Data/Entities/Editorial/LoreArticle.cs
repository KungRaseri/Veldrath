namespace Veldrath.Server.Data.Entities.Editorial;

/// <summary>A lore article for the world wiki and story content.</summary>
public class LoreArticle
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the URL-friendly slug (unique, e.g. "the-sunken-empire").</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the full Markdown content of the article.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the short plain-text summary shown in list views.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the category tag used to group articles (e.g. "History", "Factions", "Geography").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the publication status.</summary>
    public EditorialStatus Status { get; set; } = EditorialStatus.Draft;

    /// <summary>Gets or sets the UTC timestamp when the article was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the article was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when the article was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the account ID of the author who created this article.</summary>
    public Guid AuthorAccountId { get; set; }
}
