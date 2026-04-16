using Veldrath.Contracts.Auth;

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
}
