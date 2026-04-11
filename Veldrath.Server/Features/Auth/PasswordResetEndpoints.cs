using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Auth;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for password reset.
/// POST /api/auth/forgot-password  — request a password-reset email (always 200, no info leak)
/// POST /api/auth/reset-password   — complete the reset with a token from email
/// </summary>
public static class PasswordResetEndpoints
{
    /// <summary>Registers password-reset routes on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting("auth-attempts");
        group.MapPost("/reset-password",  ResetPasswordAsync) .RequireRateLimiting("auth-attempts");

        return app;
    }

    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        AuthService authService,
        CancellationToken ct)
    {
        // Always return 200 regardless of whether the email is registered to prevent
        // account enumeration via the password-reset flow.
        await authService.SendPasswordResetEmailAsync(request.Email, ct);
        return Results.Ok(new { message = "If an account with that email exists, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        AuthService authService,
        CancellationToken ct)
    {
        var (ok, error) = await authService.ResetPasswordAsync(request, ct);
        return ok
            ? Results.Ok(new { message = "Password has been reset successfully." })
            : Results.BadRequest(new { error });
    }
}
