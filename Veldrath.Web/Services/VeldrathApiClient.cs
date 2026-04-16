using Veldrath.Contracts.Auth;
using Veldrath.Contracts.Editorial;
using Veldrath.Contracts.Foundry;

namespace Veldrath.Web.Services;

/// <summary>
/// Typed HTTP client facade for calling the Veldrath.Server REST API from Veldrath.Web.
/// Base address is configured via <c>Veldrath:ServerUrl</c> at startup.
/// Call <see cref="SetBearerToken"/> after login to authorise authenticated requests.
/// </summary>
public class VeldrathApiClient(HttpClient http)
{
    /// <summary>Sets the <c>Authorization: Bearer</c> header for all subsequent requests.</summary>
    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    /// <summary>Clears the <c>Authorization</c> header.</summary>
    public void ClearBearerToken() =>
        http.DefaultRequestHeaders.Authorization = null;

    /// <summary>Returns <c>true</c> when the server's <c>/health</c> endpoint is reachable.</summary>
    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) =>
        http.GetAsync("/health", ct)
            .ContinueWith(t => t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode, ct,
                TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>
    /// Issues a fresh JWT without rotating the refresh token.
    /// Intended for Blazor circuit proactive token renewal.
    /// </summary>
    public virtual async Task<RenewJwtResponse?> RenewJwtAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/renew-jwt", new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<RenewJwtResponse>(ct) : null;
    }

    /// <summary>Rotates the refresh token and returns a new token pair.</summary>
    public virtual async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>Revokes the given refresh token on the server, ending the session. Best-effort — failures are silently swallowed so the local sign-out always completes.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        try { await http.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken }, ct); }
        catch { /* best-effort */ }
    }

    // ── Editorial (public, no auth required) ─────────────────────────────────

    /// <summary>Returns a paged list of published patch notes.</summary>
    public async Task<PagedResult<PatchNoteSummaryDto>?> GetPatchNotesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/editorial/patch-notes?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<PatchNoteSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published patch note by slug.</summary>
    public async Task<PatchNoteDto?> GetPatchNoteAsync(string slug, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/editorial/patch-notes/{Uri.EscapeDataString(slug)}", ct);
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
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<LoreArticleSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published lore article by slug.</summary>
    public async Task<LoreArticleDto?> GetLoreArticleAsync(string slug, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/editorial/lore/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<LoreArticleDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of published editorial announcements.</summary>
    public async Task<PagedResult<EditorialAnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 5, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/editorial/announcements?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<EditorialAnnouncementDto>>(ct)
            : null;
    }
}
