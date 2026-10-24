using Serilog;

namespace Veldrath.Server.Infrastructure;

/// <summary>
/// Registers the browser CORS policy shared by the SvelteKit web app, the desktop WebView
/// shell, and local development.
/// </summary>
public static class CorsSetup
{
    /// <summary>CORS policy name applied to the REST endpoints and the SignalR hub.</summary>
    public const string PolicyName = "AllowLocalClient";

    /// <summary>
    /// Adds the <see cref="PolicyName"/> policy to the service collection.
    /// </summary>
    /// <param name="builder">The application builder whose configuration is read.</param>
    /// <returns>The distinct origins the policy allows, in the order they were resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown in the Production environment when the resolved list contains only localhost
    /// origins, which can never work for a deployed browser client.
    /// </exception>
    /// <remarks>
    /// <c>Cors:AllowedOrigins</c> wins when set, either as an indexed array
    /// (<c>Cors__AllowedOrigins__0</c>) or as a single comma-separated value
    /// (<c>Cors__AllowedOrigins=https://a,https://b</c>). Otherwise the local development
    /// origins plus <c>Web:BaseUrl</c> are allowed, so the deployed SvelteKit origin
    /// (cross-subdomain from <c>api.veldrath.com</c>) is always accepted.
    /// </remarks>
    public static string[] AddVeldrathCors(this WebApplicationBuilder builder)
    {
        var configuredOrigins = (builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [])
            .SelectMany(entry => entry.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        var webBaseUrl = builder.Configuration["Web:BaseUrl"];
        var allowedOrigins = (configuredOrigins.Length > 0
                ? configuredOrigins
                : new[]
                {
                    "http://localhost:5173",
                    "https://localhost:5173",
                    // SvelteKit web container (see web/client/Dockerfile).
                    "http://localhost:8081",
                })
            .Concat(string.IsNullOrWhiteSpace(webBaseUrl)
                ? []
                : new[] { webBaseUrl.TrimEnd('/') })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Allowing only localhost in production can never work: the browser blocks the
        // cross-origin SignalR negotiate and API calls, surfacing as an opaque
        // "Failed to complete negotiation with the server: NetworkError" in the client.
        if (builder.Environment.IsProduction() &&
            !allowedOrigins.Any(origin => !origin.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "No public browser origin is allowed by CORS. Set Web__BaseUrl (for example " +
                "https://veldrath.com) or Cors__AllowedOrigins__0 in production, otherwise SignalR " +
                "negotiation and browser API calls fail with a NetworkError.");
        }

        Log.Information("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
                policy.WithOrigins(allowedOrigins)
                      // Explicit allow-list prevents TRACE/CONNECT and limits header exposure.
                      // SignalR adds x-signalr-user-agent to negotiate preflight requests.
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .WithHeaders(
                          "Authorization", "Content-Type", "X-Requested-With", "X-SignalR-User-Agent")
                      .AllowCredentials());
        });

        return allowedOrigins;
    }
}
