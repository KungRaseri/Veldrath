using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealmUnbound.Client.Services;

internal static class HttpResponseHelper
{
    /// <summary>
    /// Attempts to read a server-supplied error message from the response body.
    /// Tries JSON first (looks for an "error" property), then falls back to raw text.
    /// Returns null when the body is empty or unreadable.
    /// </summary>
    internal static async Task<string?> ExtractServerMessageAsync(HttpResponseMessage response)
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

        return serverMessage;
    }

    /// <summary>
    /// Returns the bearer authentication header for the current access token, or null when unauthenticated.
    /// </summary>
    internal static AuthenticationHeaderValue? BearerHeader(this TokenStore tokens) =>
        tokens.AccessToken is { } t
            ? new AuthenticationHeaderValue("Bearer", t)
            : null;
}
