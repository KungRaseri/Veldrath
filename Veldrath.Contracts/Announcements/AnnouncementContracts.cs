namespace Veldrath.Contracts.Announcements;

/// <summary>Represents a published announcement shown in the client news panel.</summary>
/// <param name="Id">Unique identifier of the announcement.</param>
/// <param name="Title">Short display title.</param>
/// <param name="Body">Main body text of the announcement.</param>
/// <param name="Category">Category tag such as "News", "Update", "Maintenance", or "Event".</param>
/// <param name="IsPinned">When <see langword="true"/> the announcement is always shown first regardless of date.</param>
/// <param name="PublishedAt">UTC timestamp when the announcement was published.</param>
public record AnnouncementDto(
    int Id,
    string Title,
    string Body,
    string Category,
    bool IsPinned,
    DateTimeOffset PublishedAt);
