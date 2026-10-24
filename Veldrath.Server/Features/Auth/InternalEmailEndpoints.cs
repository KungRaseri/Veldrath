using System.Net;
using System.Net.Mail;
using Veldrath.Server.Infrastructure.Email;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Request payload for an internal password-reset email.
/// </summary>
/// <param name="To">Recipient email address.</param>
/// <param name="UserName">Optional display name used in the message greeting.</param>
/// <param name="ResetUrl">Absolute URL the recipient must open to choose a new password.</param>
public sealed record InternalPasswordResetEmailRequest(string To, string? UserName, string ResetUrl);

/// <summary>
/// Request payload for an internal email-verification message.
/// </summary>
/// <param name="To">Recipient email address.</param>
/// <param name="UserName">Optional display name used in the message greeting.</param>
/// <param name="VerifyUrl">Absolute URL the recipient must open to confirm the address.</param>
public sealed record InternalVerificationEmailRequest(string To, string? UserName, string VerifyUrl);

/// <summary>Result returned by the internal email endpoints.</summary>
/// <param name="Sent">Whether the message was handed to the configured email sender.</param>
public sealed record InternalEmailResponse(bool Sent);

/// <summary>
/// Internal email endpoints used by the SvelteKit server so Better Auth can deliver
/// password-reset and address-verification messages through the game server's existing
/// SMTP transport (<see cref="SmtpEmailSender"/> / <see cref="NullEmailSender"/>).
/// <para>
/// Requests must present the shared secret from <c>InternalBridge:Token</c> in the
/// <c>X-Internal-Bridge-Token</c> header. The endpoints are not exposed to browsers.
/// </para>
/// </summary>
public static class InternalEmailEndpoints
{
    /// <summary>Maps the internal email endpoints.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapInternalEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal/email").WithTags("Internal");

        group.MapPost("/password-reset", SendPasswordResetAsync);
        group.MapPost("/verification", SendVerificationAsync);

        return app;
    }

    private static async Task<IResult> SendPasswordResetAsync(
        InternalPasswordResetEmailRequest request,
        HttpContext context,
        IConfiguration config,
        IEmailSender emailSender,
        CancellationToken ct)
    {
        if (InternalBridgeAuthorization.Validate(context, config, "email") is { } failure)
            return failure;

        if (!TryValidateRequest(request.To, request.ResetUrl, out var error))
            return Results.BadRequest(new { error });

        var html = BuildPasswordResetHtml(request.UserName, request.ResetUrl);
        await emailSender.SendAsync(request.To, "Reset your Veldrath password", html, ct);

        return Results.Ok(new InternalEmailResponse(true));
    }

    private static async Task<IResult> SendVerificationAsync(
        InternalVerificationEmailRequest request,
        HttpContext context,
        IConfiguration config,
        IEmailSender emailSender,
        CancellationToken ct)
    {
        if (InternalBridgeAuthorization.Validate(context, config, "email") is { } failure)
            return failure;

        if (!TryValidateRequest(request.To, request.VerifyUrl, out var error))
            return Results.BadRequest(new { error });

        var html = BuildVerificationHtml(request.UserName, request.VerifyUrl);
        await emailSender.SendAsync(request.To, "Confirm your Veldrath email address", html, ct);

        return Results.Ok(new InternalEmailResponse(true));
    }

    private static bool TryValidateRequest(string? to, string? url, out string error)
    {
        if (string.IsNullOrWhiteSpace(to) || !MailAddress.TryCreate(to, out _))
        {
            error = "A valid recipient email address is required.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "An absolute http(s) URL is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string BuildPasswordResetHtml(string? userName, string resetUrl)
    {
        var greeting = WebUtility.HtmlEncode(DisplayName(userName));
        var href     = WebUtility.HtmlEncode(resetUrl);

        return $"""
            <div style="font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1f2937;line-height:1.6">
              <h1 style="color:#7f1d1d;font-size:22px">Reset your Veldrath password</h1>
              <p>Hi {greeting},</p>
              <p>We received a request to reset the password for your Veldrath account.
                 This link expires in one hour.</p>
              <p>
                <a href="{href}"
                   style="display:inline-block;padding:10px 18px;border-radius:6px;background:#7f1d1d;color:#ffffff;text-decoration:none">
                  Choose a new password
                </a>
              </p>
              <p style="color:#6b7280;font-size:13px">If you did not request a reset, you can safely ignore this email.</p>
            </div>
            """;
    }

    private static string BuildVerificationHtml(string? userName, string verifyUrl)
    {
        var greeting = WebUtility.HtmlEncode(DisplayName(userName));
        var href     = WebUtility.HtmlEncode(verifyUrl);

        return $"""
            <div style="font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#1f2937;line-height:1.6">
              <h1 style="color:#7f1d1d;font-size:22px">Confirm your email address</h1>
              <p>Hi {greeting},</p>
              <p>Confirm this address to finish setting up your Veldrath account and keep it recoverable.</p>
              <p>
                <a href="{href}"
                   style="display:inline-block;padding:10px 18px;border-radius:6px;background:#7f1d1d;color:#ffffff;text-decoration:none">
                  Confirm email address
                </a>
              </p>
              <p style="color:#6b7280;font-size:13px">If you did not create a Veldrath account, you can safely ignore this email.</p>
            </div>
            """;
    }

    private static string DisplayName(string? userName) =>
        string.IsNullOrWhiteSpace(userName) ? "adventurer" : userName.Trim();
}
