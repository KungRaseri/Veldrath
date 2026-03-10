using Microsoft.AspNetCore.Mvc;

namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for authentication.
/// POST /api/auth/register  — create account, returns token pair
/// POST /api/auth/login     — verify credentials, returns token pair
/// POST /api/auth/refresh   — rotate refresh token, returns new token pair
/// POST /api/auth/logout    — revoke refresh token [Authorize]
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login",    LoginAsync);
        group.MapPost("/refresh",  RefreshAsync);
        group.MapPost("/logout",   LogoutAsync).RequireAuthorization();

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
}
