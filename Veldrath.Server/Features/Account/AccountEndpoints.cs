using System.Security.Claims;
using Veldrath.Contracts.Account;

namespace Veldrath.Server.Features.Account;

/// <summary>
/// The server's RBAC view of the signed-in account.
///
/// GET /api/account/profile — account id, username, display name, email, roles, and permissions.
///
/// Everything else a player can do to their own account (change email/password/username, link or
/// unlink a provider, list and revoke sessions) is handled by Better Auth in the SvelteKit app, so
/// there are no self-service mutation endpoints here.
/// </summary>
public static class AccountEndpoints
{
    /// <summary>Registers the account endpoints on the provided route builder.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/profile", GetProfileAsync);

        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var profile = await service.GetProfileAsync(user, ct);
        return profile is not null ? Results.Ok(profile) : Results.NotFound();
    }
}
