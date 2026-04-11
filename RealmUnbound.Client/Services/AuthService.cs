using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Veldrath.Contracts.Auth;

namespace Veldrath.Client.Services;

// Interface
public interface IAuthService
{
    Task<(AuthResponse? Response, AppError? Error)> RegisterAsync(string email, string username, string password);
    Task<(AuthResponse? Response, AppError? Error)> LoginAsync(string email, string password);
    Task<(AuthResponse? Response, AppError? Error)> LoginExternalAsync(string provider, CancellationToken ct = default);
    Task<bool> RefreshAsync();
    Task LogoutAsync();
}

// Implementation
public class HttpAuthService(
    HttpClient http,
    TokenStore tokens,
    TokenPersistenceService persistence,
    ILogger<HttpAuthService> logger) : IAuthService
{
    private static async Task<AppError> ReadErrorAsync(HttpResponseMessage response, string context = "")
    {
        var serverMessage = await HttpResponseHelper.ExtractServerMessageAsync(response);

        var statusCode = (int)response.StatusCode;
        var friendly = (context, statusCode) switch
        {
            ("login",    401) or ("login",    403)
                when serverMessage is not null
                  && serverMessage.Contains("locked out", StringComparison.OrdinalIgnoreCase)
                                                    => "Account locked. Too many failed login attempts. Please try again later.",
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
                if (auth is not null)
                {
                    tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                               auth.AccessTokenExpiry, auth.IsCurator);
                    persistence.SaveCurrent(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                                            auth.AccessTokenExpiry, auth.IsCurator);
                }
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
                if (auth is not null)
                {
                    tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                               auth.AccessTokenExpiry, auth.IsCurator);
                    persistence.SaveCurrent(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                                            auth.AccessTokenExpiry, auth.IsCurator);
                }
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
                persistence.Clear();
                return false;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) { tokens.Clear(); persistence.Clear(); return false; }

            tokens.Set(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                        auth.AccessTokenExpiry, auth.IsCurator);
            persistence.SaveCurrent(auth.AccessToken, auth.RefreshToken, auth.Username, auth.AccountId,
                                    auth.AccessTokenExpiry, auth.IsCurator);
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
                request.Headers.Authorization = tokens.BearerHeader();
            await http.SendAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Logout request failed (server may be unreachable)");
        }
        finally
        {
            tokens.Clear();
            persistence.Clear();
        }
    }

    public async Task<(AuthResponse? Response, AppError? Error)> LoginExternalAsync(
        string provider, CancellationToken ct = default)
    {
        using var listener = new OAuthLocalListener();

        // Build the challenge URL, passing the localhost callback as returnUrl.
        var serverBase = http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        var challengeUrl = $"{serverBase}/api/auth/external/{Uri.EscapeDataString(provider)}"
                         + $"?returnUrl={Uri.EscapeDataString(listener.CallbackUrl)}";

        try
        {
            // Open the system browser so the user can authenticate with the provider.
            Process.Start(new ProcessStartInfo(challengeUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open browser for OAuth");
            return (null, new AppError("Could not open the browser. Please try again."));
        }

        var result = await listener.WaitForCallbackAsync(ct);
        if (result is null)
            return (null, new AppError("Authentication timed out or was cancelled."));

        // Redeem the exchange code for a full token pair — server validates and returns AuthResponse.
        HttpResponseMessage exchangeResponse;
        try
        {
            exchangeResponse = await http.PostAsJsonAsync(
                "api/auth/exchange",
                new ExchangeCodeRequest(result.Code, result.AccountId),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth exchange request failed");
            return (null, new AppError("Could not complete sign-in. Please try again."));
        }

        if (!exchangeResponse.IsSuccessStatusCode)
        {
            var err = await ReadErrorAsync(exchangeResponse, "OAuth exchange");
            return (null, err);
        }

        var response = await exchangeResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
        if (response is null)
            return (null, new AppError("Invalid response from server."));

        tokens.Set(response.AccessToken, response.RefreshToken, response.Username, response.AccountId,
                   response.AccessTokenExpiry, response.IsCurator);
        persistence.SaveCurrent(response.AccessToken, response.RefreshToken, response.Username, response.AccountId,
                                response.AccessTokenExpiry, response.IsCurator);
        return (response, null);
    }
}
