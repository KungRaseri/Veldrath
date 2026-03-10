using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealmUnbound.Client.Services;

// ── DTOs (mirror server AuthDtos) ──────────────────────────────────────────────
public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    Guid AccountId,
    string Username);

// ── Interface ──────────────────────────────────────────────────────────────────
public interface IAuthService
{
    Task<(AuthResponse? Response, string? Error)> RegisterAsync(string email, string username, string password);
    Task<(AuthResponse? Response, string? Error)> LoginAsync(string email, string password);
    Task<bool> RefreshAsync();
    Task LogoutAsync();
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpAuthService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpAuthService> logger) : IAuthService
{
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.TryGetProperty("error", out var e))
                return e.GetString() ?? $"Request failed ({(int)response.StatusCode})";
        }
        catch { }
        var raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw)
            ? $"Request failed ({(int)response.StatusCode})"
            : raw;
    }
    public async Task<(AuthResponse? Response, string? Error)> RegisterAsync(string email, string username, string password)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, username, password));
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth is not null) tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId);
                return (auth, null);
            }

            var error = await ReadErrorAsync(response);
            return (null, error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Register request failed");
            return (null, "Network error. Please check your connection.");
        }
    }

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth is not null) tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId);
                return (auth, null);
            }

            var error = await ReadErrorAsync(response);
            return (null, error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login request failed");
            return (null, "Network error. Please check your connection.");
        }
    }

    public async Task<bool> RefreshAsync()
    {
        if (tokens.RefreshToken is null) return false;

        try
        {
            var response = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
            if (!response.IsSuccessStatusCode)
            {
                tokens.Clear();
                return false;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) { tokens.Clear(); return false; }

            tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token refresh failed");
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        if (tokens.RefreshToken is null) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
            request.Content = JsonContent.Create(new LogoutRequest(tokens.RefreshToken));
            if (tokens.AccessToken is not null)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            await http.SendAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Logout request failed (server may be unreachable)");
        }
        finally
        {
            tokens.Clear();
        }
    }
}
