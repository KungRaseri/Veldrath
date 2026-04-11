using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Minimal API endpoints for email confirmation.
/// GET  /api/auth/confirm-email        — confirm the email address using a token from email (anonymous)
/// POST /api/auth/resend-confirmation  — resend the confirmation email [Authorize]
/// </summary>
public static class EmailConfirmationEndpoints
{
    /// <summary>Registers email-confirmation routes on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapEmailConfirmationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/confirm-email",       ConfirmEmailAsync);
        group.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> ConfirmEmailAsync(
        [FromQuery] string userId,
        [FromQuery] string token,
        AuthService authService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return Results.BadRequest(new { error = "Missing userId or token." });

        var (ok, error) = await authService.ConfirmEmailAsync(userId, token, ct);
        return ok
            ? Results.Ok(new { message = "Email confirmed successfully." })
            : Results.BadRequest(new { error });
    }

    private static async Task<IResult> ResendConfirmationAsync(
        ClaimsPrincipal user,
        AuthService authService,
        CancellationToken ct)
    {
        var (ok, error) = await authService.ResendEmailConfirmationAsync(user, ct);
        return ok
            ? Results.Ok(new { message = "Confirmation email sent." })
            : Results.BadRequest(new { error });
    }
}
