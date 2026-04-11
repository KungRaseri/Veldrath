using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Account;

namespace Veldrath.Server.Features.Account;

/// <summary>
/// Self-service account management endpoints. All routes require an authenticated user.
///
/// GET    /api/account/profile                     — view own profile
/// PUT    /api/account/profile                     — update display name and bio
/// POST   /api/account/password                    — change password
/// PUT    /api/account/username                    — change username
/// GET    /api/account/sessions                    — list active refresh-token sessions
/// DELETE /api/account/sessions/{id:guid}          — revoke a specific session
/// DELETE /api/account/sessions                    — revoke all sessions except the current one
/// GET    /api/account/providers                   — list linked OAuth providers
/// DELETE /api/account/providers/{provider}        — unlink an OAuth provider
/// </summary>
public static class AccountEndpoints
{
    /// <summary>Registers all self-service account endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/profile",                    GetProfileAsync);
        group.MapPut("/profile",                    UpdateProfileAsync);
        group.MapPost("/password",                  ChangePasswordAsync).RequireRateLimiting("auth-attempts");
        group.MapPut("/username",                   ChangeUsernameAsync).RequireRateLimiting("auth-attempts");
        group.MapGet("/sessions",                   GetSessionsAsync);
        group.MapDelete("/sessions/{id:guid}",      RevokeSessionAsync);
        group.MapDelete("/sessions",                RevokeOtherSessionsAsync);
        group.MapGet("/providers",                  GetProvidersAsync);
        group.MapDelete("/providers/{provider}",    UnlinkProviderAsync);

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

    private static async Task<IResult> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var (ok, error) = await service.UpdateProfileAsync(user, request, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var (ok, error) = await service.ChangePasswordAsync(user, request, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }

    private static async Task<IResult> ChangeUsernameAsync(
        [FromBody] ChangeUsernameRequest request,
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var (ok, error) = await service.ChangeUsernameAsync(user, request, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }

    // ── Session management ────────────────────────────────────────────────────

    private static async Task<IResult> GetSessionsAsync(
        ClaimsPrincipal user,
        AccountService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        // The current token ID can't be recovered from the JWT itself, so we pass null —
        // callers who want IsCurrent populated must supply it via the header below.
        Guid? currentTokenId = ctx.Request.Headers.TryGetValue("X-Current-Session-Id", out var hdr)
            && Guid.TryParse(hdr, out var tid) ? tid : null;

        var sessions = await service.GetSessionsAsync(user, currentTokenId, ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid id,
        ClaimsPrincipal user,
        AccountService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (ok, error) = await service.RevokeSessionAsync(user, id, ip, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }

    private static async Task<IResult> RevokeOtherSessionsAsync(
        [FromBody] RevokeOtherSessionsRequest request,
        ClaimsPrincipal user,
        AccountService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (ok, error) = await service.RevokeOtherSessionsAsync(user, request.CurrentSessionId, ip, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }

    // ── OAuth provider management ─────────────────────────────────────────────

    private static async Task<IResult> GetProvidersAsync(
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var providers = await service.GetLinkedProvidersAsync(user, ct);
        return Results.Ok(providers);
    }

    private static async Task<IResult> UnlinkProviderAsync(
        string provider,
        [FromBody] UnlinkProviderRequest request,
        ClaimsPrincipal user,
        AccountService service,
        CancellationToken ct)
    {
        var (ok, error) = await service.UnlinkProviderAsync(user, provider, request.ProviderKey, ct);
        return ok ? Results.NoContent() : Results.BadRequest(new { error });
    }
}
