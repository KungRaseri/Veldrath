using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Foundry;

namespace RealmFoundry.Services;

/// <summary>
/// Typed HttpClient facade for calling the RealmUnbound.Server REST API.
/// Base address is configured via <c>RealmUnbound:ServerUrl</c> at startup.
/// Call <see cref="SetBearerToken"/> after login to authorise requests.
/// </summary>
public class RealmFoundryApiClient(HttpClient http)
{
    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public void ClearBearerToken() =>
        http.DefaultRequestHeaders.Authorization = null;

    // ── Health ────────────────────────────────────────────────────────────

    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) =>
        http.GetAsync("/health", ct)
            .ContinueWith(t => t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode, ct,
                TaskContinuationOptions.None, TaskScheduler.Default);

    // ── Auth ──────────────────────────────────────────────────────────────

    public virtual async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    // ── Submissions ───────────────────────────────────────────────────────

    public async Task<PagedResult<FoundrySubmissionSummaryDto>> GetSubmissionsAsync(
        string? status = null, string? contentType = null,
        string? search = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = "/api/foundry/submissions";
        var qs  = BuildQuery(
            ("status",      status),
            ("contentType", contentType),
            ("search",      search),
            ("page",        page.ToString()),
            ("pageSize",    pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;

        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(ct)
              ?? new([],  0, page, pageSize)
            : new([], 0, page, pageSize);
    }

    public async Task<FoundrySubmissionDto?> GetSubmissionAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/foundry/submissions/{id}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<FoundrySubmissionDto>(ct)
            : null;
    }

    public async Task<(FoundrySubmissionDto? Dto, string? Error)> CreateSubmissionAsync(
        CreateSubmissionRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/foundry/submissions", request, ct);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<FoundrySubmissionDto>(ct), null);

        var body = await resp.Content.ReadAsStringAsync(ct);
        return (null, body);
    }

    public async Task<(FoundrySubmissionSummaryDto? Dto, string? Error)> VoteAsync(
        Guid submissionId, int value, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"/api/foundry/submissions/{submissionId}/vote",
            new { Value = value }, ct);

        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<FoundrySubmissionSummaryDto>(ct), null);

        var body = await resp.Content.ReadAsStringAsync(ct);
        return (null, body);
    }

    public async Task<(FoundrySubmissionDto? Dto, string? Error)> ReviewAsync(
        Guid submissionId, ReviewRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            $"/api/foundry/submissions/{submissionId}/review", request, ct);

        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<FoundrySubmissionDto>(ct), null);

        var body = await resp.Content.ReadAsStringAsync(ct);
        return (null, body);
    }

    // ── Content Browse (public, no auth required) ─────────────────────

    public async Task<IReadOnlyList<ContentTypeInfoDto>> GetContentTypesAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/content/schema", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<ContentTypeInfoDto>>(ct) ?? []
            : [];
    }

    public async Task<PagedResult<ContentSummaryDto>> BrowseContentAsync(
        string contentType, string? search = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = "/api/content/browse";
        var qs  = BuildQuery(
            ("type",     contentType),
            ("search",   search),
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;

        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<ContentSummaryDto>>(ct)
              ?? new([], 0, page, pageSize)
            : new([], 0, page, pageSize);
    }

    public async Task<ContentDetailDto?> GetContentDetailAsync(
        string contentType, string slug, CancellationToken ct = default)
    {
        var resp = await http.GetAsync(
            $"/api/content/browse/{Uri.EscapeDataString(contentType)}/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ContentDetailDto>(ct)
            : null;
    }

    // ── Notifications ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<FoundryNotificationDto>> GetNotificationsAsync(CancellationToken ct = default)    {
        var resp = await http.GetAsync("/api/foundry/notifications", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<FoundryNotificationDto>>(ct) ?? []
            : [];
    }

    public async Task<bool> MarkNotificationReadAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/foundry/notifications/{id}/read", null, ct);
        return resp.IsSuccessStatusCode;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        return string.Join("&", parts);
    }
}
