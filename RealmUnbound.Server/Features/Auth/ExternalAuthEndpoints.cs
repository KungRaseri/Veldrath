using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for OAuth external authentication.
/// Supports both the standard login/register flow and a provider-link flow (<c>mode=link</c>)
/// that adds a new external login to an already-authenticated account.
/// </summary>
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
    /// <c>returnUrl</c> with a single-use opaque exchange code instead of the
    /// raw token pair, preventing tokens from appearing in browser history or server logs.
    /// When <c>mode=link</c> is present in the state properties the provider is linked to an
    /// existing authenticated account rather than creating a new session.
    /// </summary>
    public static async Task HandleOAuthTicket(TicketReceivedContext ctx)
    {
        var authSvc     = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
        var exchangeSvc = ctx.HttpContext.RequestServices.GetRequiredService<AuthExchangeCodeService>();
        var userMgr     = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<PlayerAccount>>();
        var principal   = ctx.Principal!;
        var email       = principal.FindFirstValue(ClaimTypes.Email);
        var displayName = principal.FindFirstValue(ClaimTypes.Name);
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? principal.FindFirstValue("sub");
        var clientIp    = ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // ── Link mode: attach provider to an existing authenticated account ────
        string? mode = null;
        string? accountIdStr = null;
        ctx.Properties?.Items.TryGetValue("mode", out mode);
        ctx.Properties?.Items.TryGetValue("accountId", out accountIdStr);
        if (mode == "link" && Guid.TryParse(accountIdStr, out var linkAccountId))
        {
            var linkTarget = await userMgr.FindByIdAsync(linkAccountId.ToString());
            if (linkTarget is null)
            {
                ctx.HandleResponse();
                ctx.Response.Redirect("/profile?error=link_failed");
                return;
            }

            // AddLoginAsync is idempotent — safe to call if already linked.
            await userMgr.AddLoginAsync(linkTarget,
                new UserLoginInfo(ctx.Scheme.Name, providerKey ?? "", ctx.Scheme.Name));

            string? linkReturnUrl = null;
            ctx.Properties?.Items.TryGetValue("returnUrl", out linkReturnUrl);
            ctx.HandleResponse();
            ctx.Response.Redirect(linkReturnUrl is not null && IsAllowedReturnUrl(linkReturnUrl)
                ? linkReturnUrl + "?linked=1"
                : "/profile?linked=1");
            return;
        }

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
            // Mint a single-use exchange code bound to this account — never expose the raw tokens in a URL.
            var code         = exchangeSvc.CreateCode(response, response.AccountId);
            var uriBuilder   = new UriBuilder(returnUrl);
            var query        = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["code"]    = code;
            query["aid"]     = response.AccountId.ToString();
            uriBuilder.Query = query.ToString();

            ctx.HandleResponse();
            ctx.Response.Redirect(uriBuilder.ToString());
            return;
        }

        // No valid returnUrl — fall through to default cookie sign-in so the
        // /api/auth/external/callback endpoint can return JSON to API callers.
    }

    // GET /api/auth/external/{provider}?returnUrl={optional}&mode=link&accountId={guid}
    // Kicks off the OAuth redirect for the requested provider.
    // When mode=link and accountId are supplied the provider will be linked to the specified
    // existing account instead of creating a new session.
    private static IResult Challenge(string provider, string? returnUrl, string? mode, Guid? accountId, HttpContext context)
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
        if (mode == "link" && accountId.HasValue)
        {
            properties.Items["mode"]      = "link";
            properties.Items["accountId"] = accountId.Value.ToString();
        }

        return Results.Challenge(properties, [scheme]);
    }

    // GET /api/auth/external/callback
    // Handles the redirect from the OAuth provider, issues a JWT pair, and returns it.
    private static async Task<IResult> Callback(
        HttpContext       context,
        SignInManager<PlayerAccount> signInManager,
        AuthService       authService,
        AuthExchangeCodeService exchangeService)
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
        // with an exchange code so the local HTTP listener can pick it up safely.
        if (info.AuthenticationProperties?.Items.TryGetValue("returnUrl", out var returnUrl) == true
            && returnUrl is not null
            && IsAllowedReturnUrl(returnUrl))
        {
            var code    = exchangeService.CreateCode(response, response.AccountId);
            var builder = new UriBuilder(returnUrl);
            var query   = HttpUtility.ParseQueryString(builder.Query);
            query["code"]  = code;
            query["aid"]   = response.AccountId.ToString();
            builder.Query  = query.ToString();
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
