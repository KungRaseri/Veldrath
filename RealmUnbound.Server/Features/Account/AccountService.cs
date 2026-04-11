using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Veldrath.Contracts.Account;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Account;

/// <summary>
/// Handles self-service account management: profile updates, password/username changes,
/// session enumeration, session revocation, and OAuth provider linking.
/// </summary>
public class AccountService(
    UserManager<PlayerAccount> userManager,
    ApplicationDbContext db,
    IRefreshTokenRepository refreshTokenRepo)
{
    // ── Profile ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="AccountProfileDto"/> for the given user, including linked providers,
    /// effective roles/permissions, and the count of currently active sessions.
    /// </summary>
    public async Task<AccountProfileDto?> GetProfileAsync(
        ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return null;

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var userClaims = await userManager.GetClaimsAsync(user);
        var permissions = userClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        var logins = await userManager.GetLoginsAsync(user);
        var linkedProviders = logins
            .Select(l => new LinkedProviderDto(l.LoginProvider, l.ProviderKey, DateTimeOffset.MinValue))
            .ToList();

        // ExpiresAt > DateTimeOffset.UtcNow cannot be translated by the SQLite EF Core provider.
        // Filter on RevokedAt in SQL, then apply the expiry check (IsActive) in memory.
        var activeSessions = await db.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.AccountId == user.Id && rt.RevokedAt == null)
            .ToListAsync(ct);
        var activeSessionCount = activeSessions.Count(rt => rt.IsActive);

        var hasPassword = await userManager.HasPasswordAsync(user);

        return new AccountProfileDto(
            user.Id,
            user.UserName!,
            user.DisplayName,
            user.Bio,
            user.Email,
            hasPassword,
            user.CreatedAt,
            user.LastSeenAt,
            roles,
            permissions,
            linkedProviders,
            activeSessionCount);
    }

    /// <summary>Updates the optional public profile fields (<see cref="PlayerAccount.DisplayName"/> and <see cref="PlayerAccount.Bio"/>).</summary>
    public async Task<(bool Ok, string? Error)> UpdateProfileAsync(
        ClaimsPrincipal principal, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return (false, "Account not found.");

        user.DisplayName = request.DisplayName;
        user.Bio = request.Bio;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // ── Password / Username ───────────────────────────────────────────────────

    /// <summary>
    /// Changes the password for the authenticated account.
    /// Requires the current password to be supplied as an authorisation check.
    /// </summary>
    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(
        ClaimsPrincipal principal, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return (false, "Account not found.");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Changes the username for the authenticated account.
    /// Returns an error if the requested username is already taken.
    /// </summary>
    public async Task<(bool Ok, string? Error)> ChangeUsernameAsync(
        ClaimsPrincipal principal, ChangeUsernameRequest request, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return (false, "Account not found.");

        var existing = await userManager.FindByNameAsync(request.NewUsername);
        if (existing is not null && existing.Id != user.Id)
            return (false, "Username is already taken.");

        var result = await userManager.SetUserNameAsync(user, request.NewUsername);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // ── Sessions ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active refresh-token sessions for the authenticated account.
    /// The <paramref name="currentTokenId"/> is used to flag the caller's own session as current.
    /// </summary>
    public async Task<IReadOnlyList<AccountSessionDto>> GetSessionsAsync(
        ClaimsPrincipal principal, Guid? currentTokenId, CancellationToken ct = default)
    {
        if (!TryGetAccountId(principal, out var accountId)) return [];

        // ExpiresAt > DateTimeOffset.UtcNow cannot be translated by the SQLite EF Core provider.
        // Filter on RevokedAt in SQL, then apply the expiry check (IsActive) in memory.
        var tokens = await db.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.AccountId == accountId && rt.RevokedAt == null)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(ct);

        return tokens
            .Where(rt => rt.IsActive)
            .Select(rt => new AccountSessionDto(rt.Id, rt.CreatedByIp, rt.CreatedAt, rt.ExpiresAt, rt.Id == currentTokenId))
            .ToList();
    }

    /// <summary>
    /// Revokes the specified refresh-token session. Returns an error if the session does not
    /// belong to the authenticated account (IDOR guard).
    /// </summary>
    public async Task<(bool Ok, string? Error)> RevokeSessionAsync(
        ClaimsPrincipal principal, Guid sessionId, string clientIp, CancellationToken ct = default)
    {
        if (!TryGetAccountId(principal, out var accountId))
            return (false, "Account not found.");

        var token = await db.RefreshTokens.FindAsync([sessionId], ct);
        if (token is null || token.AccountId != accountId)
            return (false, "Session not found.");

        if (!token.IsActive)
            return (false, "Session is already expired or revoked.");

        await refreshTokenRepo.RevokeAsync(sessionId, clientIp, null, ct);
        return (true, null);
    }

    /// <summary>
    /// Revokes all active refresh-token sessions for the authenticated account except the one
    /// identified by <paramref name="currentTokenId"/> (so the caller stays logged in).
    /// </summary>
    public async Task<(bool Ok, string? Error)> RevokeOtherSessionsAsync(
        ClaimsPrincipal principal, Guid currentTokenId, string clientIp, CancellationToken ct = default)
    {
        if (!TryGetAccountId(principal, out var accountId))
            return (false, "Account not found.");

        var others = await db.RefreshTokens
            .Where(rt => rt.AccountId == accountId && rt.Id != currentTokenId && rt.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var t in others)
        {
            t.RevokedAt = now;
            t.RevokedByIp = clientIp;
        }

        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    // ── OAuth Providers ───────────────────────────────────────────────────────

    /// <summary>Returns all OAuth providers linked to the authenticated account.</summary>
    public async Task<IReadOnlyList<LinkedProviderDto>> GetLinkedProvidersAsync(
        ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return [];

        var logins = await userManager.GetLoginsAsync(user);
        return logins
            .Select(l => new LinkedProviderDto(l.LoginProvider, l.ProviderKey, DateTimeOffset.MinValue))
            .ToList();
    }

    /// <summary>
    /// Removes the specified OAuth provider from the authenticated account.
    /// Fails if unlinking would leave the account with no remaining login method
    /// (no password and no other providers).
    /// </summary>
    public async Task<(bool Ok, string? Error)> UnlinkProviderAsync(
        ClaimsPrincipal principal, string provider, string providerKey, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(principal, ct);
        if (user is null) return (false, "Account not found.");

        var logins = await userManager.GetLoginsAsync(user);
        var hasPassword = await userManager.HasPasswordAsync(user);
        var remainingAfterUnlink = logins.Count(l => !l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (!hasPassword && remainingAfterUnlink == 0)
            return (false, "Cannot unlink this provider: it is your only login method. Set a password first.");

        var result = await userManager.RemoveLoginAsync(user, provider, providerKey);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<PlayerAccount?> ResolveUserAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!TryGetAccountId(principal, out var id)) return null;
        return await userManager.FindByIdAsync(id.ToString());
    }

    private static bool TryGetAccountId(ClaimsPrincipal principal, out Guid id)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out id);
    }
}
