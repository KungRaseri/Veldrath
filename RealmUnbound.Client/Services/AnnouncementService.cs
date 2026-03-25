using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Announcements;
using System.Net.Http.Json;

namespace RealmUnbound.Client.Services;

/// <summary>Fetches announcements from the server news feed.</summary>
public interface IAnnouncementService
{
    /// <summary>
    /// Returns active announcements from the server, or an empty list if the server
    /// is unreachable or returns no data.
    /// </summary>
    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken ct = default);
}

/// <summary>HTTP-backed implementation of <see cref="IAnnouncementService"/>.</summary>
public class HttpAnnouncementService(HttpClient http, ILogger<HttpAnnouncementService> logger)
    : IAnnouncementService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await http.GetFromJsonAsync<List<AnnouncementDto>>("api/announcements", ct);
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch announcements from server");
            return [];
        }
    }
}
