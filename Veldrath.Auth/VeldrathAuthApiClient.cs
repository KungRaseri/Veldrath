using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Veldrath.Contracts.Account;
using Veldrath.Contracts.Auth;

namespace Veldrath.Auth;

/// <summary>
/// Typed HTTP client that wraps all Veldrath.Server authentication endpoints.
/// Configure the base address via <c>AddHttpClient&lt;VeldrathAuthApiClient&gt;()</c> during DI registration.
/// Derive from this class to add application-specific endpoints while inheriting all auth operations.
/// </summary>
public class VeldrathAuthApiClient(HttpClient http) : IVeldrathAuthApiClient
{
    /// <summary>
    /// The underlying <see cref="HttpClient"/> for use by derived classes that add
    /// application-specific endpoints alongside the inherited auth operations.
    /// </summary>
    protected HttpClient Http => http;

    /// <summary>Gets the configured server base URL, e.g. <c>https://api.veldrath.com</c>.</summary>
    public string ServerBaseUrl => http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

    /// <summary>Sets the <c>Authorization: Bearer</c> header on all subsequent requests.</summary>
    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Clears the <c>Authorization</c> header.</summary>
    public void ClearBearerToken() =>
        http.DefaultRequestHeaders.Authorization = null;

    /// <summary>Returns <c>true</c> when the server's <c>/health</c> endpoint is reachable.</summary>
    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) =>
        http.GetAsync("/health", ct)
            .ContinueWith(t => t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode, ct,
                TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>Registers a new account. Returns <see langword="null"/> on failure.</summary>
    public virtual async Task<AuthResponse?> RegisterAsync(
        string email, string username, string password, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, username, password), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>Authenticates with email and password. Returns <see langword="null"/> on failure.</summary>
    public virtual async Task<AuthResponse?> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>Rotates the refresh token and returns a new token pair. Returns <see langword="null"/> on failure.</summary>
    public virtual async Task<AuthResponse?> RefreshTokenAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>
    /// Issues a fresh JWT without rotating the refresh token.
    /// Returns <see langword="null"/> if the refresh token is revoked or expired.
    /// </summary>
    public virtual async Task<RenewJwtResponse?> RenewJwtAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/renew-jwt",
            new { RefreshToken = refreshToken }, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RenewJwtResponse>(ct) : null;
    }

    /// <summary>
    /// Revokes the given refresh token on the server, ending the session.
    /// Best-effort — failures are silently swallowed so the local sign-out always completes.
    /// </summary>
    public virtual async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        try { await http.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(refreshToken), ct); }
        catch { /* best-effort */ }
    }

    /// <summary>Redeems a single-use exchange code for a full token pair. Returns <see langword="null"/> on failure.</summary>
    public virtual async Task<AuthResponse?> ExchangeCodeAsync(
        string code, Guid accountId, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/exchange",
            new ExchangeCodeRequest(code, accountId), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(ct) : null;
    }

    /// <summary>Creates a single-use exchange code for the currently authenticated account.</summary>
    public virtual async Task<CreateExchangeCodeResponse?> CreateExchangeCodeAsync(
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/auth/create-exchange-code", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<CreateExchangeCodeResponse>(ct) : null;
    }

    /// <summary>Requests a password-reset email. Always returns successfully to prevent account enumeration.</summary>
    public virtual async Task ForgotPasswordAsync(string email, CancellationToken ct = default) =>
        await http.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email), ct);

    /// <summary>Completes a password reset using a token received by email.</summary>
    public virtual async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, newPassword), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    /// <summary>Confirms an email address using a <paramref name="userId"/> and <paramref name="token"/> from the confirmation link.</summary>
    public virtual async Task<(bool Ok, string? Error)> ConfirmEmailAsync(
        string userId, string token, CancellationToken ct = default)
    {
        var url = $"/api/auth/confirm-email"
                + $"?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        var resp = await http.GetAsync(url, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    /// <summary>Requests the server to resend the email-confirmation message for the authenticated account.</summary>
    public virtual async Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(
        CancellationToken ct = default)
    {
        var resp = await http.PostAsync("/api/auth/resend-confirmation", null, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    // ── Self-service account management ──────────────────────────────────────

    /// <summary>Returns the authenticated account's own full profile including linked providers.</summary>
    public virtual async Task<AccountProfileDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/account/profile", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AccountProfileDto>(ct) : null;
    }

    /// <summary>Updates the authenticated account's optional public display name and biography.</summary>
    public virtual async Task<(bool Ok, string? Error)> UpdateProfileAsync(
        string? displayName, string? bio, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest(displayName, bio), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    /// <summary>Changes the authenticated account's username.</summary>
    public virtual async Task<(bool Ok, string? Error)> ChangeUsernameAsync(
        string newUsername, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsync("/api/account/username",
            new ChangeUsernameRequest(newUsername), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    /// <summary>Changes the authenticated account's password.</summary>
    public virtual async Task<(bool Ok, string? Error)> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/account/password",
            new ChangePasswordRequest(currentPassword, newPassword), ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    /// <summary>Returns all OAuth providers currently linked to the authenticated account.</summary>
    public virtual async Task<IReadOnlyList<LinkedProviderDto>> GetLinkedProvidersAsync(
        CancellationToken ct = default)
    {
        var resp = await http.GetAsync("/api/account/providers", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<LinkedProviderDto>>(ct) ?? []
            : [];
    }

    /// <summary>
    /// Removes the specified OAuth provider login from the authenticated account.
    /// Fails if the provider is the account's only login method and no password is set.
    /// </summary>
    public virtual async Task<(bool Ok, string? Error)> UnlinkProviderAsync(
        string provider, string providerKey, CancellationToken ct = default)
    {
        var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/account/providers/{provider}")
        {
            Content = JsonContent.Create(new UnlinkProviderRequest(providerKey))
        }, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorAsync(resp.Content, ct));
    }

    // ── Session management ────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active refresh-token sessions for the authenticated account.
    /// Pass <paramref name="currentSessionId"/> to flag the caller's own session as current.
    /// </summary>
    public virtual async Task<IReadOnlyList<AccountSessionDto>> GetSessionsAsync(
        Guid? currentSessionId = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/account/sessions");
        if (currentSessionId.HasValue)
            req.Headers.Add("X-Current-Session-Id", currentSessionId.Value.ToString());
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<AccountSessionDto>>(ct) ?? []
            : [];
    }

    /// <summary>Revokes the specified refresh-token session. Returns <c>false</c> on failure.</summary>
    public virtual async Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/api/account/sessions/{sessionId}", ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// Revokes all active sessions for the authenticated account except the one identified by
    /// <paramref name="currentSessionId"/>.
    /// </summary>
    public virtual async Task<bool> RevokeOtherSessionsAsync(
        Guid currentSessionId, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/account/sessions")
        {
            Content = JsonContent.Create(new RevokeOtherSessionsRequest(currentSessionId))
        };
        var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>
    /// Reads the response body and extracts the value of the <c>error</c> field when the body is
    /// a JSON object, e.g. <c>{"error":"Token invalid."}</c>.
    /// Falls back to returning the raw string when the body is not valid JSON or lacks an
    /// <c>error</c> property.
    /// </summary>
    private static async Task<string?> ReadErrorAsync(HttpContent content, CancellationToken ct)
    {
        var raw = await content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString();
        }
        catch { }
        return raw;
    }
}
