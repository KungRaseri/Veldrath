using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Auth;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for authentication.
/// POST /api/auth/register              — create account, returns token pair
/// POST /api/auth/login                 — verify credentials, returns token pair
/// POST /api/auth/refresh               — rotate refresh token, returns new token pair
/// POST /api/auth/logout                — revoke refresh token [Authorize]
/// POST /api/auth/exchange              — redeem a single-use OAuth exchange code for a token pair
/// GET  /api/auth/create-exchange-code  — issue a short-lived SSO handoff code [Authorize]
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register",              RegisterAsync).RequireRateLimiting("auth-attempts");
        group.MapPost("/login",                 LoginAsync)   .RequireRateLimiting("auth-attempts");
        group.MapPost("/refresh",               RefreshAsync) .RequireRateLimiting("auth-attempts");
        group.MapPost("/logout",                LogoutAsync)  .RequireAuthorization();
        group.MapPost("/exchange",              ExchangeAsync).RequireRateLimiting("auth-attempts");
        group.MapGet ("/create-exchange-code",  CreateExchangeCodeAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        AuthService authService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (response, error) = await authService.RegisterAsync(request, ip, ct);
        return response is not null
            ? Results.Ok(response)
            : Results.BadRequest(new { error });
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        AuthService authService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (response, error) = await authService.LoginAsync(request, ip, ct);
        return response is not null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: 401);
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        AuthService authService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (response, error) = await authService.RefreshAsync(request.RefreshToken, ip, ct);
        return response is not null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: 401);
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        AuthService authService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await authService.RevokeAsync(request.RefreshToken, ip, ct);
        return Results.NoContent();
    }

    private static IResult ExchangeAsync(
        [FromBody] ExchangeCodeRequest request,
        AuthExchangeCodeService exchangeService)
    {
        if (!exchangeService.TryConsume(request.Code, request.AccountId, out var response))
            return Results.BadRequest(new { error = "Invalid or expired exchange code." });

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateExchangeCodeAsync(
        ClaimsPrincipal user,
        AuthService authService,
        AuthExchangeCodeService exchangeService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var idStr = user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(idStr, out var accountId))
            return Results.Unauthorized();

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Re-issue a fresh auth session so the exchange code carries a valid, non-expired token pair.
        var userEntity = await authService.FindUserByIdAsync(accountId);
        if (userEntity is null) return Results.Unauthorized();

        var authResponse = await authService.CreateSessionAsync(userEntity, ip, ct);
        var code = exchangeService.CreateCode(authResponse, accountId);

        return Results.Ok(new CreateExchangeCodeResponse(code, accountId));
    }
}
