using System.Web;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Shared OAuth landing endpoint for all Veldrath sub-applications (RealmFoundry, Veldrath.Web).
/// After a successful OAuth exchange, the browser visits <c>GET /api/auth/session</c> carrying
/// the single-use exchange code.  The endpoint redeems the code, writes the <c>rt</c>
/// refresh-token cookie with <c>Domain=Auth:CookieDomain</c> (covering every subdomain in
/// production), then redirects the browser back to the originating application.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>Maps <c>GET /api/auth/session</c>.</summary>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/session", StartSession).RequireRateLimiting("auth-attempts");
        return app;
    }

    // GET /api/auth/session?code={code}&aid={accountId}&redirectTo={url}
    // Redeems a single-use exchange code, writes the cross-subdomain rt cookie, and
    // redirects the browser to the originating application page supplied in redirectTo.
    private static IResult StartSession(
        string? code,
        Guid? aid,
        string? redirectTo,
        AuthExchangeCodeService exchangeService,
        IConfiguration config,
        IWebHostEnvironment env,
        HttpContext ctx)
    {
        var foundryBase = config["Foundry:BaseUrl"];
        var webBase     = config["Web:BaseUrl"];

        if (string.IsNullOrWhiteSpace(code) || !aid.HasValue
            || !exchangeService.TryConsume(code, aid.Value, out var authResponse))
        {
            return Results.Redirect(ErrorRedirect(redirectTo, foundryBase, webBase));
        }

        var cookieDomain = config["Auth:CookieDomain"];

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            MaxAge   = TimeSpan.FromDays(30),
            Path     = "/",
        };

        if (!string.IsNullOrWhiteSpace(cookieDomain))
            cookieOptions.Domain = cookieDomain;

        ctx.Response.Cookies.Append("rt", authResponse.RefreshToken, cookieOptions);

        // Redirect to the originating app if it is on a known-safe origin; otherwise fall back.
        var destination = !string.IsNullOrWhiteSpace(redirectTo)
            && ExternalAuthEndpoints.IsAllowedReturnUrl(redirectTo, foundryBase, webBase)
                ? redirectTo
                : (foundryBase?.TrimEnd('/') + "/") ?? "/";

        return Results.Redirect(destination);
    }

    // Builds a /login?error=auth_failed redirect on the originating app, or falls back to the
    // Foundry or the server root when no valid redirectTo is provided.
    private static string ErrorRedirect(string? redirectTo, string? foundryBase, string? webBase = null)
    {
        if (!string.IsNullOrWhiteSpace(redirectTo)
            && Uri.TryCreate(redirectTo, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host == "127.0.0.1"
                || (foundryBase is not null
                    && Uri.TryCreate(foundryBase, UriKind.Absolute, out var fb)
                    && uri.Host.Equals(fb.Host, StringComparison.OrdinalIgnoreCase))
                || (webBase is not null
                    && Uri.TryCreate(webBase, UriKind.Absolute, out var wb)
                    && uri.Host.Equals(wb.Host, StringComparison.OrdinalIgnoreCase))))
        {
            return $"{uri.Scheme}://{uri.Authority}/login?error=auth_failed";
        }

        return foundryBase is not null
            ? foundryBase.TrimEnd('/') + "/login?error=auth_failed"
            : "/login?error=auth_failed";
    }
}
