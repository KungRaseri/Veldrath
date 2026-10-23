using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Immutable snapshot of an account after just-in-time provisioning, including the
/// effective RBAC claims that should be attached to the authenticated principal.
/// </summary>
/// <param name="AccountId">Stable account identifier taken from the JWT <c>sub</c> claim.</param>
/// <param name="Username">Identity username, unique per account.</param>
/// <param name="IsBanned">Whether the account is currently banned at the account level.</param>
/// <param name="BannedUntil">UTC expiry of a temporary ban, or <see langword="null"/> for a permanent ban.</param>
/// <param name="Roles">Role names assigned to the account.</param>
/// <param name="Permissions">Effective permission grants from roles and per-user overrides.</param>
public sealed record AccountAccessSnapshot(
    Guid AccountId,
    string Username,
    bool IsBanned,
    DateTimeOffset? BannedUntil,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Provisions ASP.NET Core Identity accounts for identities that are owned by an
/// external provider (currently Better Auth in SvelteKit).
/// <para>
/// Better Auth is the source of truth for web credentials and sessions. The game server
/// only needs a matching <see cref="PlayerAccount"/> row so existing Identity-based
/// repositories, character ownership, moderation state, and RBAC continue to work.
/// When a valid JWT arrives for an unknown <c>sub</c>, a row is created on demand with
/// that GUID as its primary key — no password is set because credentials stay in Better Auth.
/// </para>
/// </summary>
public sealed class AccountProvisioningService(
    UserManager<PlayerAccount> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<AccountProvisioningService> logger)
{
    // Identity is configured with RequireUniqueEmail, so every account needs an address.
    // External identities without an email claim receive a deterministic placeholder that
    // can never collide with a real address.
    private const string ExternalEmailDomain = "external.veldrath.local";

    // Enrichment runs on every authenticated request. Tokens that carry no RBAC claims
    // (Better Auth tokens) would otherwise cause a role/claim lookup per request, so
    // snapshots are cached briefly per account.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<Guid, (AccountAccessSnapshot Snapshot, DateTimeOffset ExpiresAt)> Cache = new();

    /// <summary>
    /// Clears the per-process access snapshot cache. Call after changing a user's roles or
    /// permissions when the change must take effect immediately (tests, admin tooling).
    /// </summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Ensures an account exists for <paramref name="accountId"/> and resolves its access.
    /// </summary>
    /// <param name="accountId">Account identifier from the JWT <c>sub</c> claim.</param>
    /// <param name="username">Preferred username from the JWT; sanitized and de-duplicated if needed.</param>
    /// <param name="email">Optional email address supplied by the identity provider.</param>
    /// <param name="emailVerified">Whether the identity provider has verified the email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The provisioned account snapshot, or <see langword="null"/> when provisioning failed
    /// (for example a database error or an unsupported identity).
    /// </returns>
    public async Task<AccountAccessSnapshot?> EnsureAccountAsync(
        Guid accountId,
        string? username,
        string? email,
        bool emailVerified,
        CancellationToken ct = default)
    {
        if (Cache.TryGetValue(accountId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Snapshot;

        var user = await userManager.FindByIdAsync(accountId.ToString());
        if (user is null)
            user = await CreateAccountAsync(accountId, username, email, emailVerified);

        if (user is null)
            return null;

        var (roles, permissions) = await RolePermissionResolver.ResolveAsync(user, userManager, roleManager);

        var snapshot = new AccountAccessSnapshot(
            user.Id,
            user.UserName ?? accountId.ToString(),
            user.IsBanned,
            user.BannedUntil,
            roles,
            permissions);

        Cache[accountId] = (snapshot, DateTimeOffset.UtcNow.Add(CacheDuration));
        return snapshot;
    }

    private async Task<PlayerAccount?> CreateAccountAsync(
        Guid accountId,
        string? username,
        string? email,
        bool emailVerified)
    {
        var baseName = SanitizeUsername(username);
        var candidate = baseName;
        var attempt = 0;
        while (await userManager.FindByNameAsync(candidate) is not null)
            candidate = $"{baseName}{++attempt}";

        var user = new PlayerAccount
        {
            Id = accountId,
            UserName = candidate,
            Email = string.IsNullOrWhiteSpace(email)
                ? $"{accountId:N}@{ExternalEmailDomain}"
                : email,
            EmailConfirmed = emailVerified,
            DisplayName = candidate,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await userManager.CreateAsync(user);

        // An existing Identity account may already own this email. Retry with the
        // deterministic placeholder address instead of failing the login.
        if (!result.Succeeded &&
            result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            logger.LogWarning(
                "JIT provisioning for account {AccountId}: email/username collision ({Errors}); retrying with a placeholder email.",
                accountId, string.Join("; ", result.Errors.Select(e => e.Description)));

            user.Email = $"{accountId:N}@{ExternalEmailDomain}";
            user.NormalizedEmail = null;
            user.EmailConfirmed = false;
            result = await userManager.CreateAsync(user);
        }

        if (result.Succeeded)
        {
            logger.LogInformation(
                "JIT provisioned game account {AccountId} as '{Username}' from external identity provider.",
                accountId, user.UserName);
            return user;
        }

        // A concurrent request may have created the same account first.
        var existing = await userManager.FindByIdAsync(accountId.ToString());
        if (existing is not null)
            return existing;

        logger.LogError(
            "JIT provisioning failed for account {AccountId}: {Errors}",
            accountId, string.Join("; ", result.Errors.Select(e => e.Description)));
        return null;
    }

    private static string SanitizeUsername(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "player";

        var clean = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (clean.Length > 32) clean = clean[..32];
        return clean.Length > 0 ? clean : "player";
    }
}
