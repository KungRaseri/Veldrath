using Veldrath.Auth;
using Veldrath.Contracts.Editorial;
using Veldrath.Contracts.Foundry;

namespace Veldrath.Web.Services;

/// <summary>
/// Typed HTTP client facade for calling the Veldrath.Server REST API from Veldrath.Web.
/// Authentication endpoints are provided by <see cref="VeldrathAuthApiClient"/>.
/// Configure the base address via <c>Veldrath:ServerUrl</c> at startup.
/// Call <see cref="VeldrathAuthApiClient.SetBearerToken"/> after login to authorise authenticated requests.
/// </summary>
public class VeldrathApiClient(HttpClient http) : VeldrathAuthApiClient(http)
{
    // ── Editorial (public, no auth required) ─────────────────────────────────

    /// <summary>Returns a paged list of published patch notes.</summary>
    public async Task<PagedResult<PatchNoteSummaryDto>?> GetPatchNotesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/patch-notes?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<PatchNoteSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published patch note by slug.</summary>
    public async Task<PatchNoteDto?> GetPatchNoteAsync(string slug, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/patch-notes/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PatchNoteDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of published lore articles, optionally filtered by category.</summary>
    public async Task<PagedResult<LoreArticleSummaryDto>?> GetLoreArticlesAsync(int page = 1, int pageSize = 20, string? category = null, CancellationToken ct = default)
    {
        var url = $"/api/editorial/lore?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category))
            url += $"&category={Uri.EscapeDataString(category)}";
        var resp = await Http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<LoreArticleSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published lore article by slug.</summary>
    public async Task<LoreArticleDto?> GetLoreArticleAsync(string slug, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/lore/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<LoreArticleDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of published editorial announcements.</summary>
    public async Task<PagedResult<EditorialAnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 5, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/announcements?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<EditorialAnnouncementDto>>(ct)
            : null;
    }
}

