using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Features.Auth;

public static class ExternalAuthEndpoints
{
    // Scheme names registered by each OAuth provider package.
    private static readonly Dictionary<string, string> ProviderSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["discord"]   = "Discord",
        ["google"]    = "Google",
        ["microsoft"] = "Microsoft",
    };

    public static IEndpointRouteBuilder MapExternalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/external/{provider}", Challenge);
        app.MapGet("/api/auth/external/callback",   Callback);
        return app;
    }

    /// <summary>
    /// Registered on every OAuth provider's <c>OnTicketReceived</c> event.
    /// Issues JWT tokens directly from the OAuth ticket and redirects to
    /// <c>returnUrl</c> — bypassing the external-cookie round-trip that
    /// fails on plain HTTP because <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/>
    /// requires a <c>LoginProvider</c> key the framework never writes.
    /// </summary>
    public static async Task HandleOAuthTicket(TicketReceivedContext ctx)
    {
        var authSvc     = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
        var principal   = ctx.Principal!;
        var email       = principal.FindFirstValue(ClaimTypes.Email);
        var displayName = principal.FindFirstValue(ClaimTypes.Name);
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? principal.FindFirstValue("sub");
        var clientIp    = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var (response, _) = await authSvc.ExternalLoginOrRegisterAsync(
            ctx.Scheme.Name, providerKey ?? "", email, displayName, clientIp);

        if (response is null)
        {
            ctx.HandleResponse();
            ctx.Response.Redirect("/login?error=auth_failed");
            return;
        }

        string? returnUrl = null;
        ctx.Properties?.Items.TryGetValue("returnUrl", out returnUrl);

        if (returnUrl is not null && IsAllowedReturnUrl(returnUrl))
        {
            var uriBuilder   = new UriBuilder(returnUrl);
            var query        = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["jwt"]     = response.AccessToken;
            query["refresh"] = response.RefreshToken;
            query["expires"] = response.AccessTokenExpiry.ToUnixTimeSeconds().ToString();
            uriBuilder.Query = query.ToString();

            ctx.HandleResponse();
            ctx.Response.Redirect(uriBuilder.ToString());
            return;
        }

        // No valid returnUrl — fall through to default cookie sign-in so the
        // /api/auth/external/callback endpoint can return JSON to API callers.
    }

    // GET /api/auth/external/{provider}?returnUrl={optional}
    // Kicks off the OAuth redirect for the requested provider.
    private static IResult Challenge(string provider, string? returnUrl, HttpContext context)
    {
        if (!ProviderSchemes.TryGetValue(provider, out var scheme))
            return Results.BadRequest("Unknown OAuth provider.");

        // returnUrl is the Avalonia client's local HTTP listener address;
        // guard against open-redirect by restricting to localhost only.
        if (returnUrl is not null && !IsAllowedReturnUrl(returnUrl))
            return Results.BadRequest("Invalid return URL.");

        // After the provider redirects back, ASP.NET's OAuth handler completes the
        // exchange and then bounces the user to this endpoint.
        var callbackUri = $"{context.Request.Scheme}://{context.Request.Host}/api/auth/external/callback";

        var properties = new AuthenticationProperties { RedirectUri = callbackUri };
        if (returnUrl is not null)
            properties.Items["returnUrl"] = returnUrl;

        return Results.Challenge(properties, [scheme]);
    }

    // GET /api/auth/external/callback
    // Handles the redirect from the OAuth provider, issues a JWT pair, and returns it.
    private static async Task<IResult> Callback(
        HttpContext       context,
        SignInManager<PlayerAccount> signInManager,
        AuthService       authService)
    {
        // SignInManager reads the transient external cookie (IdentityConstants.ExternalScheme)
        // that the OAuth middleware wrote during the callback exchange.
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return Results.Unauthorized();

        var email       = info.Principal.FindFirstValue(ClaimTypes.Email);
        var displayName = info.Principal.FindFirstValue(ClaimTypes.Name);
        var clientIp    = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var (response, error) = await authService.ExternalLoginOrRegisterAsync(
            info.LoginProvider, info.ProviderKey, email, displayName, clientIp);

        if (response is null)
            return Results.Problem(error ?? "Authentication failed.", statusCode: 400);

        // If the caller provided a returnUrl (Avalonia desktop auth flow), redirect there
        // with the tokens as query parameters so the local HTTP listener can pick them up.
        if (info.AuthenticationProperties?.Items.TryGetValue("returnUrl", out var returnUrl) == true
            && returnUrl is not null
            && IsAllowedReturnUrl(returnUrl))
        {
            var builder = new UriBuilder(returnUrl);
            var query   = HttpUtility.ParseQueryString(builder.Query);
            query["jwt"]     = response.AccessToken;
            query["refresh"] = response.RefreshToken;
            query["expires"] = response.AccessTokenExpiry.ToUnixTimeSeconds().ToString();
            builder.Query    = query.ToString();
            return Results.Redirect(builder.ToString(), permanent: false);
        }

        return Results.Ok(response);
    }

    // Only localhost/127.0.0.1 is accepted as a returnUrl to prevent open-redirect attacks.
    private static bool IsAllowedReturnUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("localhost",  StringComparison.OrdinalIgnoreCase)
            || uri.Host == "127.0.0.1")
        && uri.Scheme is "http" or "https";
}
