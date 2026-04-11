using System.Web;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for the OAuth provider-link confirmation flow.
/// After <see cref="AuthService.ExternalLoginOrRegisterAsync"/> determines that a new provider
/// matches an existing account by email, a confirmation token is emailed to the account holder.
/// Clicking the link in that email calls <c>GET /api/auth/link/confirm</c>, which attaches the
/// provider and issues a session exchange code.
/// </summary>
public static class PendingLinkEndpoints
{
    /// <summary>Maps <c>GET /api/auth/link/confirm</c>.</summary>
    public static IEndpointRouteBuilder MapPendingLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/link/confirm", ConfirmLink);
        return app;
    }

    // GET /api/auth/link/confirm?token={rawToken}
    // Validates the confirmation token, attaches the provider, and issues an exchange code.
    private static async Task<IResult> ConfirmLink(
        string token,
        HttpContext context,
        AccountLinkService accountLinkSvc,
        AuthService authService,
        AuthExchangeCodeService exchangeService)
    {
        var (account, pendingToken, error) =
            await accountLinkSvc.ConfirmAndLinkAsync(token, context.RequestAborted);

        if (error is not null)
            return Results.Redirect($"/login?error={Uri.EscapeDataString(error)}");

        var clientIp  = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var response  = await authService.CreateSessionAsync(account!, clientIp, context.RequestAborted);
        var code      = exchangeService.CreateCode(response, response.AccountId);

        var returnUrl = pendingToken!.ReturnUrl;
        string destination;

        if (returnUrl is not null && ExternalAuthEndpoints.IsAllowedReturnUrl(returnUrl))
        {
            var uriBuilder   = new UriBuilder(returnUrl);
            var query        = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["code"]    = code;
            query["aid"]     = response.AccountId.ToString();
            query["linked"]  = "1";
            uriBuilder.Query = query.ToString();
            destination      = uriBuilder.ToString();
        }
        else
        {
            destination = $"/login?code={Uri.EscapeDataString(code)}&aid={response.AccountId}";
        }

        return Results.Redirect(destination, permanent: false);
    }
}
