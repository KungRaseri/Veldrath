using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
