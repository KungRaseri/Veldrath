using Veldrath.Contracts.Account;
using Veldrath.Contracts.Auth;
using Veldrath.Contracts.Admin;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Foundry;
using Veldrath.Contracts.Players;

namespace RealmFoundry.Services;

/// <summary>
/// Typed HttpClient facade for calling the Veldrath.Server REST API.
/// Base address is configured via <c>Veldrath:ServerUrl</c> at startup.
/// Call <see cref="SetBearerToken"/> after login to authorise requests.
/// </summary>
public class RealmFoundryApiClient(HttpClient http)
{
    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public void ClearBearerToken() =>
        http.DefaultRequestHeaders.Authorization = null;

    // Health
    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) =>
        http.GetAsync("/health", ct)
            .ContinueWith(t => t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode, ct,
                TaskContinuationOptions.None, TaskScheduler.Default);

    // Auth
    public virtual async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>
    /// Issues a fresh JWT without rotating the refresh token.
    /// Intended for Blazor circuit proactive token renewal — the HttpOnly cookie refresh token stays in sync permanently.
    /// </summary>
    public virtual async Task<RenewJwtResponse?> RenewJwtAsync(string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/renew-jwt", new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<RenewJwtResponse>(ct) : null;
    }

    /// <summary>Revokes the given refresh token on the server, ending the session. Best-effort — failures are silently swallowed so the local sign-out always completes.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        try { await http.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(refreshToken), ct); }
        catch { /* best-effort — local sign-out always succeeds */ }
    }

    /// <summary>Redeems a single-use exchange code issued by the OAuth callback for a full auth response.</summary>
    public async Task<AuthResponse?> ExchangeCodeAsync(string code, Guid accountId, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/exchange", new ExchangeCodeRequest(code, accountId), ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>Requests a password-reset email for the given address. Always returns successfully to prevent account enumeration.</summary>
    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default) =>
        await http.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), ct);

    /// <summary>Completes a password reset using a token received by email.</summary>
    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, token, newPassword), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Confirms an email address using a userId + token from the confirmation link.</summary>
    public async Task<(bool Ok, string? Error)> ConfirmEmailAsync(
        string userId, string token, CancellationToken ct = default)
    {
        var url  = $"/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        var resp = await http.GetAsync(url, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Requests the server to resend the email-confirmation message for the authenticated account.</summary>
    public async Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(CancellationToken ct = default)
    {
        var resp = await http.PostAsync("/api/auth/resend-confirmation", null, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    // Submissions
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

    // Content Browse (public, no auth required)
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

    // Notifications
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

    // Helpers
    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        return string.Join("&", parts);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>Returns a paged list of player accounts with optional filtering.</summary>
    public async Task<AdminUserListResponse?> GetUsersAsync(
        string? search   = null,
        string? role     = null,
        string? status   = null,
        string? sort     = null,
        int     page     = 1,
        int     pageSize = 25,
        CancellationToken ct = default)
    {
        var url = "/api/admin/users";
        var qs  = BuildQuery(
            ("search",   search),
            ("role",     role),
            ("status",   status),
            ("sort",     sort),
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;

        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AdminUserListResponse>(ct)
            : null;
    }

    /// <summary>Returns the full detail for a single player account.</summary>
    public async Task<PlayerDetailDto?> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/admin/users/{id}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlayerDetailDto>(ct)
            : null;
    }

    /// <summary>Returns the public profile for a player account including their top character info.</summary>
    public async Task<PlayerProfileDto?> GetPlayerProfileAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/players/{id}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlayerProfileDto>(ct)
            : null;
    }

    /// <summary>Assigns a role to an account. Returns (true, null) on success or (false, errorMessage) on failure.</summary>
    public async Task<(bool Ok, string? Error)> AssignRoleAsync(Guid id, string role, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"/api/admin/users/{id}/roles", new AssignRoleRequest(role), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Revokes a role from an account.</summary>
    public async Task<(bool Ok, string? Error)> RevokeRoleAsync(Guid id, string role, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/admin/users/{id}/roles/{Uri.EscapeDataString(role)}", ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Grants a per-user permission claim to an account.</summary>
    public async Task<(bool Ok, string? Error)> GrantPermissionAsync(Guid id, string permission, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"/api/admin/users/{id}/permissions", new GrantPermissionRequest(permission), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Revokes a per-user permission claim from an account.</summary>
    public async Task<(bool Ok, string? Error)> RevokePermissionAsync(Guid id, string permission, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/admin/users/{id}/permissions/{Uri.EscapeDataString(permission)}", ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Sends a kick signal to all active sessions for the specified account.</summary>
    public async Task<(bool Ok, string? Error)> KickPlayerAsync(KickPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/kick", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Bans an account and kicks its active sessions.</summary>
    public async Task<(bool Ok, string? Error)> BanPlayerAsync(BanPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/ban", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Lifts a ban from an account.</summary>
    public async Task<(bool Ok, string? Error)> UnbanPlayerAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/unban", new UnbanPlayerRequest(id), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Broadcasts an announcement to all connected game clients.</summary>
    public async Task<(bool Ok, string? Error)> BroadcastAnnouncementAsync(BroadcastAnnouncementRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/broadcast", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Issues a formal warning to a player account.</summary>
    public async Task<(bool Ok, string? Error)> WarnPlayerAsync(WarnPlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/warn", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Mutes a player's chat for an optional duration.</summary>
    public async Task<(bool Ok, string? Error)> MutePlayerAsync(MutePlayerRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/mute", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Lifts an active chat mute from an account.</summary>
    public async Task<(bool Ok, string? Error)> UnmutePlayerAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/admin/players/unmute", new UnmutePlayerRequest(id), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Returns a paged admin audit log, optionally filtered by target account.</summary>
    public async Task<PagedAdminResult<AuditEntryDto>?> GetAuditLogAsync(
        int page = 1, int pageSize = 50, Guid? targetId = null, CancellationToken ct = default)
    {
        var url = "/api/admin/audit";
        var qs  = BuildQuery(
            ("targetId", targetId?.ToString()),
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedAdminResult<AuditEntryDto>>(ct)
            : null;
    }

    /// <summary>Returns all active player sessions, optionally filtered by region or zone.</summary>
    public async Task<IReadOnlyList<ActiveSessionDto>> GetSessionsAsync(
        string? regionId = null, string? zoneId = null, CancellationToken ct = default)
    {
        var url = "/api/admin/sessions";
        var qs  = BuildQuery(("regionId", regionId), ("zoneId", zoneId));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<ActiveSessionDto>>(ct) ?? []
            : [];
    }

    /// <summary>Returns a paged list of player reports, optionally filtered by resolution status.</summary>
    public async Task<PagedAdminResult<PlayerReportDto>?> GetReportsAsync(
        int page = 1, int pageSize = 50, bool? resolved = null, CancellationToken ct = default)
    {
        var url = "/api/admin/reports";
        var qs  = BuildQuery(
            ("resolved", resolved?.ToString().ToLowerInvariant()),
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedAdminResult<PlayerReportDto>>(ct)
            : null;
    }

    /// <summary>Marks a player report as resolved.</summary>
    public async Task<(bool Ok, string? Error)> ResolveReportAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.PutAsync($"/api/admin/reports/{id}/resolve", null, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    // ── Self-Service Account Management ──────────────────────────────────────

    /// <summary>Returns the authenticated user's own account profile.</summary>
    public async Task<AccountProfileDto?> GetAccountProfileAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/account/profile", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AccountProfileDto>(ct)
            : null;
    }

    /// <summary>Updates the authenticated user's optional display name and bio.</summary>
    public async Task<(bool Ok, string? Error)> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/account/profile", request, ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Changes the authenticated user's password.</summary>
    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/account/password", request, ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Changes the authenticated user's username.</summary>
    public async Task<(bool Ok, string? Error)> ChangeUsernameAsync(ChangeUsernameRequest request, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/account/username", request, ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Returns all active refresh-token sessions for the authenticated user.</summary>
    public async Task<IReadOnlyList<AccountSessionDto>?> GetAccountSessionsAsync(Guid? currentSessionId = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/account/sessions");
        if (currentSessionId.HasValue)
            req.Headers.TryAddWithoutValidation("X-Current-Session-Id", currentSessionId.Value.ToString());
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<AccountSessionDto>>(ct)
            : null;
    }

    /// <summary>Revokes the specified active session.</summary>
    public async Task<(bool Ok, string? Error)> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/account/sessions/{sessionId}", ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Revokes all active sessions except the one identified by <paramref name="currentSessionId"/>.</summary>
    public async Task<(bool Ok, string? Error)> RevokeAllOtherSessionsAsync(Guid currentSessionId, CancellationToken ct = default)
    {
        var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/sessions")
        {
            Content = JsonContent.Create(new RevokeOtherSessionsRequest(currentSessionId))
        }, ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Returns all OAuth providers linked to the authenticated user's account.</summary>
    public async Task<IReadOnlyList<LinkedProviderDto>?> GetLinkedProvidersAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/account/providers", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<LinkedProviderDto>>(ct)
            : null;
    }

    /// <summary>Removes the specified OAuth provider from the authenticated user's account.</summary>
    public async Task<(bool Ok, string? Error)> UnlinkProviderAsync(string provider, string providerKey, CancellationToken ct = default)
    {
        var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/account/providers/{provider}")
        {
            Content = JsonContent.Create(new UnlinkProviderRequest(providerKey))
        }, ct);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Returns the live server status snapshot including zone populations.</summary>
    public async Task<ServerStatusDto?> GetServerStatusAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/admin/status", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ServerStatusDto>(ct)
            : null;
    }

    // ── Moderator (view_players) ─────────────────────────────────────────────

    /// <summary>Returns a paged list of player accounts from the moderation endpoint.</summary>
    public async Task<AdminUserListResponse?> GetModUsersAsync(
        string? search = null, string? status = null, string? sort = null,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var url = "/api/mod/users";
        var qs  = BuildQuery(("search", search), ("status", status), ("sort", sort),
                             ("page", page.ToString()), ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AdminUserListResponse>(ct)
            : null;
    }

    /// <summary>Returns the full detail for a single player account from the moderation endpoint.</summary>
    public async Task<PlayerDetailDto?> GetModUserAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/mod/users/{id}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlayerDetailDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of player reports from the moderation endpoint.</summary>
    public async Task<PagedAdminResult<PlayerReportDto>?> GetModReportsAsync(
        int page = 1, int pageSize = 50, bool? resolved = null, CancellationToken ct = default)
    {
        var url = "/api/mod/reports";
        var qs  = BuildQuery(
            ("resolved", resolved?.ToString().ToLowerInvariant()),
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()));
        if (!string.IsNullOrEmpty(qs)) url += "?" + qs;
        var resp = await http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedAdminResult<PlayerReportDto>>(ct)
            : null;
    }

    /// <summary>Resolves a player report via the moderation endpoint.</summary>
    public async Task<(bool Ok, string? Error)> ResolveModReportAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await http.PutAsync($"/api/mod/reports/{id}/resolve", null, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }

    // ── Web Report Submission ────────────────────────────────────────────────

    /// <summary>Submits a report against another user from the Foundry web portal.</summary>
    public async Task<(bool Ok, string? Error)> SubmitReportAsync(
        SubmitReportRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/reports", request, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync(ct));
    }
}

/// <summary>Paged response for the admin user list endpoint.</summary>
/// <param name="Total">Total number of matching accounts.</param>
/// <param name="Page">Current page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Items">Accounts on the current page.</param>
public record AdminUserListResponse(int Total, int Page, int PageSize, IReadOnlyList<PlayerSummaryDto> Items);

/// <summary>Generic paged response wrapper used by admin list endpoints.</summary>
/// <typeparam name="T">The item type returned in each page.</typeparam>
/// <param name="Total">Total number of matching records.</param>
/// <param name="Page">Current page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Items">Items on the current page.</param>
public record PagedAdminResult<T>(int Total, int Page, int PageSize, IReadOnlyList<T> Items);
