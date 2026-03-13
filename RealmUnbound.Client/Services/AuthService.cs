using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Auth;

namespace RealmUnbound.Client.Services;

// ── Interface ──────────────────────────────────────────────────────────────────
public interface IAuthService
{
    Task<(AuthResponse? Response, AppError? Error)> RegisterAsync(string email, string username, string password);
    Task<(AuthResponse? Response, AppError? Error)> LoginAsync(string email, string password);
    Task<bool> RefreshAsync();
    Task LogoutAsync();
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpAuthService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpAuthService> logger) : IAuthService
{
    private static async Task<AppError> ReadErrorAsync(HttpResponseMessage response, string context = "")
    {
        string? serverMessage = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.TryGetProperty("error", out var e))
                serverMessage = e.GetString();
        }
        catch { }

        if (serverMessage is null)
        {
            try { serverMessage = await response.Content.ReadAsStringAsync(); }
            catch { }
            if (string.IsNullOrWhiteSpace(serverMessage))
                serverMessage = null;
        }

        var statusCode = (int)response.StatusCode;
        var friendly = (context, statusCode) switch
        {
            ("login",    401) or ("login",    403) => "Incorrect email or password. Please try again.",
            ("login",    429)                       => "Too many login attempts. Please wait a moment and try again.",
            ("register", 409)                       => "An account with that email or username already exists.",
            ("register", 400) when serverMessage is not null => serverMessage,
            ("register", 429)                       => "Too many attempts. Please wait a moment and try again.",
            (_,          500) or (_, 503)           => "The server encountered an error. Please try again later.",
            _                                       => serverMessage ?? "An error occurred. Please try again."
        };

        // Only surface technical details when they add information beyond the friendly message
        var details = serverMessage != null && serverMessage != friendly ? serverMessage : null;
        return new AppError(friendly, details);
    }
    public async Task<(AuthResponse? Response, AppError? Error)> RegisterAsync(string email, string username, string password)
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

            return (null, await ReadErrorAsync(response, "register"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Register request failed");
            return (null, new AppError("Network error. Please check your connection."));
        }
    }

    public async Task<(AuthResponse? Response, AppError? Error)> LoginAsync(string email, string password)
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

            return (null, await ReadErrorAsync(response, "login"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login request failed");
            return (null, new AppError("Network error. Please check your connection."));
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
