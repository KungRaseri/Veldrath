namespace Veldrath.Contracts.Editorial;

/// <summary>Summary of a patch note shown in list views.</summary>
/// <param name="Id">Unique identifier of the patch note.</param>
/// <param name="Slug">URL-friendly slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Summary">Short plain-text excerpt.</param>
/// <param name="Version">Game version this patch applies to.</param>
/// <param name="Status">Publication status ("Draft" or "Published").</param>
/// <param name="PublishedAt">UTC timestamp when the entry was published, or <see langword="null"/> if still a draft.</param>
public record PatchNoteSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Version,
    string Status,
    DateTimeOffset? PublishedAt);

/// <summary>Full patch note including Markdown content.</summary>
/// <param name="Id">Unique identifier of the patch note.</param>
/// <param name="Slug">URL-friendly slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Summary">Short plain-text excerpt.</param>
/// <param name="Version">Game version this patch applies to.</param>
/// <param name="Status">Publication status ("Draft" or "Published").</param>
/// <param name="PublishedAt">UTC timestamp when the entry was published, or <see langword="null"/> if still a draft.</param>
/// <param name="Content">Full Markdown content.</param>
public record PatchNoteDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Version,
    string Status,
    DateTimeOffset? PublishedAt,
    string Content);

/// <summary>Summary of a lore article shown in list views.</summary>
/// <param name="Id">Unique identifier of the article.</param>
/// <param name="Slug">URL-friendly slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Summary">Short plain-text excerpt.</param>
/// <param name="Category">Category tag (e.g. "History", "Factions").</param>
/// <param name="Status">Publication status ("Draft" or "Published").</param>
/// <param name="PublishedAt">UTC timestamp when the article was published, or <see langword="null"/> if still a draft.</param>
public record LoreArticleSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Category,
    string Status,
    DateTimeOffset? PublishedAt);

/// <summary>Full lore article including Markdown content.</summary>
/// <param name="Id">Unique identifier of the article.</param>
/// <param name="Slug">URL-friendly slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Summary">Short plain-text excerpt.</param>
/// <param name="Category">Category tag.</param>
/// <param name="Status">Publication status ("Draft" or "Published").</param>
/// <param name="PublishedAt">UTC timestamp when the article was published, or <see langword="null"/> if still a draft.</param>
/// <param name="Content">Full Markdown content.</param>
public record LoreArticleDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Category,
    string Status,
    DateTimeOffset? PublishedAt,
    string Content);

/// <summary>A site-wide dismissible banner announcement.</summary>
/// <param name="Id">Unique identifier of the announcement.</param>
/// <param name="Title">Short display title.</param>
/// <param name="Body">Plain-text body of the announcement.</param>
/// <param name="Status">Publication status ("Draft" or "Published").</param>
/// <param name="PublishedAt">UTC timestamp when the announcement was published, or <see langword="null"/> if still a draft.</param>
public record EditorialAnnouncementDto(
    Guid Id,
    string Title,
    string Body,
    string Status,
    DateTimeOffset? PublishedAt);

/// <summary>Request body for creating a new patch note.</summary>
/// <param name="Slug">URL-friendly slug (must be unique).</param>
/// <param name="Title">Display title.</param>
/// <param name="Content">Full Markdown content.</param>
/// <param name="Summary">Short plain-text excerpt for list views.</param>
/// <param name="Version">Game version this patch applies to.</param>
public record CreatePatchNoteRequest(
    string Slug,
    string Title,
    string Content,
    string Summary,
    string Version);

/// <summary>Request body for updating an existing patch note.</summary>
/// <param name="Title">Display title.</param>
/// <param name="Content">Full Markdown content.</param>
/// <param name="Summary">Short plain-text excerpt for list views.</param>
/// <param name="Version">Game version this patch applies to.</param>
/// <param name="Status">New publication status ("Draft" or "Published").</param>
public record UpdatePatchNoteRequest(
    string Title,
    string Content,
    string Summary,
    string Version,
    string Status);

/// <summary>Request body for creating a new lore article.</summary>
/// <param name="Slug">URL-friendly slug (must be unique).</param>
/// <param name="Title">Display title.</param>
/// <param name="Content">Full Markdown content.</param>
/// <param name="Summary">Short plain-text excerpt for list views.</param>
/// <param name="Category">Category tag (e.g. "History", "Factions", "Geography").</param>
public record CreateLoreArticleRequest(
    string Slug,
    string Title,
    string Content,
    string Summary,
    string Category);

/// <summary>Request body for updating an existing lore article.</summary>
/// <param name="Title">Display title.</param>
/// <param name="Content">Full Markdown content.</param>
/// <param name="Summary">Short plain-text excerpt for list views.</param>
/// <param name="Category">Category tag.</param>
/// <param name="Status">New publication status ("Draft" or "Published").</param>
public record UpdateLoreArticleRequest(
    string Title,
    string Content,
    string Summary,
    string Category,
    string Status);

/// <summary>Request body for creating a new editorial announcement.</summary>
/// <param name="Title">Short display title.</param>
/// <param name="Body">Plain-text body of the announcement.</param>
public record CreateAnnouncementRequest(
    string Title,
    string Body);

/// <summary>Request body for updating an existing editorial announcement.</summary>
/// <param name="Title">Short display title.</param>
/// <param name="Body">Plain-text body of the announcement.</param>
/// <param name="Status">New publication status ("Draft" or "Published").</param>
public record UpdateAnnouncementRequest(
    string Title,
    string Body,
    string Status);
