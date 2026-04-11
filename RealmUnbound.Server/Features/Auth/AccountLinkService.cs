using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Infrastructure.Email;

namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Handles the provider-link confirmation flow.
/// When a new OAuth login arrives whose email matches an existing account that does not
/// yet have that provider linked, <see cref="RequestLinkAsync"/> generates a time-limited
/// confirmation token, persists it, and sends a confirmation email.
/// The account is not modified until the user presents the raw token to
/// <c>GET /api/auth/link/confirm</c>, which calls <see cref="ConfirmAndLinkAsync"/>.
/// </summary>
public class AccountLinkService(
    IPendingLinkRepository pendingLinkRepo,
    IEmailSender emailSender,
    UserManager<PlayerAccount> userManager,
    IConfiguration config)
{
    /// <summary>
    /// Generates a pending link token for <paramref name="account"/>, persists it, and sends
    /// a confirmation email to the account's registered address.
    /// </summary>
    /// <param name="account">Existing account the new provider will be linked to on confirmation.</param>
    /// <param name="provider">OAuth scheme name (e.g. <c>Discord</c>).</param>
    /// <param name="providerKey">Provider-issued subject identifier.</param>
    /// <param name="providerDisplayName">Optional display name from the provider.</param>
    /// <param name="returnUrl">
    /// URL to redirect to after a successful confirmation, e.g. the Foundry profile page.
    /// <see langword="null"/> defaults to the <c>/login</c> page.
    /// </param>
    /// <param name="serverBaseUrl">Base URL of the server, used to build the confirmation link.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RequestLinkAsync(
        PlayerAccount account,
        string provider,
        string providerKey,
        string? providerDisplayName,
        string? returnUrl,
        string serverBaseUrl,
        CancellationToken ct = default)
    {
        var expiryMinutes = int.TryParse(config["Auth:PendingLinkExpiryMinutes"], out var m) ? m : 60;

        // Generate a cryptographically random 32-byte token; only the hash is persisted.
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(rawBytes).ToLowerInvariant();
        var tokenHash = AuthService.HashToken(rawToken);

        var pending = new PendingLinkToken
        {
            AccountId           = account.Id,
            LoginProvider       = provider,
            ProviderKey         = providerKey,
            ProviderDisplayName = providerDisplayName,
            TokenHash           = tokenHash,
            Email               = account.Email ?? string.Empty,
            ReturnUrl           = returnUrl,
            ExpiresAt           = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
        };

        await pendingLinkRepo.CreateAsync(pending, ct);

        var confirmUrl = $"{serverBaseUrl.TrimEnd('/')}/api/auth/link/confirm?token={rawToken}";
        var body       = BuildConfirmationEmail(account.UserName ?? account.Email ?? "Player", provider, confirmUrl, expiryMinutes);

        await emailSender.SendAsync(
            account.Email!,
            $"Link your {provider} account to RealmUnbound",
            body,
            ct);
    }

    /// <summary>
    /// Validates the raw confirmation token, links the provider to the matching account, and
    /// marks the token as confirmed so it cannot be reused.
    /// </summary>
    /// <param name="rawToken">The raw (unhashed) token from the confirmation URL query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// On success: the linked <see cref="PlayerAccount"/> and the consumed <see cref="PendingLinkToken"/>
    /// (caller may read <see cref="PendingLinkToken.ReturnUrl"/>).
    /// On failure: <see langword="null"/> account, <see langword="null"/> token, and a human-readable error string.
    /// </returns>
    public async Task<(PlayerAccount? Account, PendingLinkToken? Token, string? Error)> ConfirmAndLinkAsync(
        string rawToken,
        CancellationToken ct = default)
    {
        var hash  = AuthService.HashToken(rawToken);
        var token = await pendingLinkRepo.GetByTokenHashAsync(hash, ct);

        if (token is null)
            return (null, null, "link_invalid");

        if (token.IsConfirmed)
            return (null, null, "link_already_confirmed");

        if (DateTimeOffset.UtcNow >= token.ExpiresAt)
            return (null, null, "link_expired");

        // AddLoginAsync is idempotent — calling it twice for the same provider/key is safe.
        var linkResult = await userManager.AddLoginAsync(
            token.Account,
            new UserLoginInfo(token.LoginProvider, token.ProviderKey, token.ProviderDisplayName ?? token.LoginProvider));

        if (!linkResult.Succeeded)
            return (null, null, string.Join("; ", linkResult.Errors.Select(e => e.Description)));

        await pendingLinkRepo.ConfirmAsync(token.Id, ct);

        return (token.Account, token, null);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string BuildConfirmationEmail(
        string username, string provider, string confirmUrl, int expiryMinutes)
    {
        return $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto">
              <h2>Link your {provider} account</h2>
              <p>Hi <strong>{username}</strong>,</p>
              <p>
                A sign-in attempt with <strong>{provider}</strong> matched your RealmUnbound account.
                To link this provider to your account, click the button below within
                <strong>{expiryMinutes} minutes</strong>.
              </p>
              <p style="margin:2em 0">
                <a href="{confirmUrl}"
                   style="background:#5865f2;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  Confirm account link
                </a>
              </p>
              <p>If you did not initiate this request, you can safely ignore this email — your account will not be modified.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:2em 0"/>
              <p style="color:#999;font-size:12px">
                This link expires in {expiryMinutes} minutes and can only be used once.
              </p>
            </body></html>
            """;
    }
}
