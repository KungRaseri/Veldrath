using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Veldrath.Contracts.Auth;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Infrastructure.Email;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Handles account registration, login, token refresh, and logout.
/// JWT access tokens are short-lived (default 15 min).
/// Refresh tokens are stored as SHA-256 hashes — raw values are never persisted.
/// Presenting a previously revoked refresh token triggers immediate revocation of all
/// tokens for that account (theft detection).
/// External OAuth logins that share an email with an existing account trigger an
/// email-confirmation flow via <see cref="AccountLinkService"/> rather than silently linking.
/// </summary>
public class AuthService(
    UserManager<PlayerAccount> userManager,
    SignInManager<PlayerAccount> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IRefreshTokenRepository refreshTokenRepo,
    AccountLinkService accountLinkService,
    IEmailSender emailSender,
    IConfiguration config)
{
    public async Task<(AuthResponse? Response, string? Error)> RegisterAsync(
        RegisterRequest request, string clientIp, CancellationToken ct = default)
    {
        var user = new PlayerAccount
        {
            UserName = request.Username,
            NormalizedUserName = request.Username.ToUpperInvariant(),
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return (null, string.Join("; ", result.Errors.Select(e => e.Description)));

        await SendEmailConfirmationAsync(user, ct);

        return (await IssueTokenPairAsync(user, clientIp, ct), null);
    }

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(
        LoginRequest request, string clientIp, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return (null, "Invalid credentials");

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return (null, result.IsLockedOut ? "Account is locked out" : "Invalid credentials");

        return (await IssueTokenPairAsync(user, clientIp, ct), null);
    }

    public async Task<(AuthResponse? Response, string? Error)> RefreshAsync(
        string rawRefreshToken, string clientIp, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var stored = await refreshTokenRepo.GetByTokenHashAsync(hash, ct);

        if (stored is null)
            return (null, "Invalid refresh token");

        if (!stored.IsActive)
        {
            // The cookie may be 1 rotation behind the live token if the Blazor circuit
            // refreshed the JWT in-memory without being able to update the browser cookie.
            // Walk the ReplacedByTokenId chain to find the current active token before
            // treating this as theft.
            var chainActive = await refreshTokenRepo.GetCurrentActiveInChainAsync(stored.Id, ct);
            if (chainActive is null)
            {
                // Chain is dead — either genuine theft or all tokens expired; revoke everything.
                await refreshTokenRepo.RevokeAllForAccountAsync(stored.AccountId, clientIp, ct);
                return (null, "Refresh token has been revoked");
            }
            stored = chainActive;
        }

        var user = await userManager.FindByIdAsync(stored.AccountId.ToString());
        if (user is null)
            return (null, "Account not found");

        // Issue new token pair, then chain the replacement ID into the old record.
        var newRefreshId = Guid.NewGuid();
        var (rawNew, hashNew) = GenerateRefreshToken();
        var refreshDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");

        await refreshTokenRepo.CreateAsync(new RefreshToken
        {
            Id = newRefreshId,
            AccountId = user.Id,
            TokenHash = hashNew,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays),
            CreatedByIp = clientIp,
        }, ct);

        await refreshTokenRepo.RevokeAsync(stored.Id, clientIp, newRefreshId, ct);

        var (roles, permissions) = await ResolveRolesAndPermissionsAsync(user);
        var (jwt, expiry) = GenerateJwt(user, roles, permissions);
        return (new AuthResponse(jwt, rawNew, expiry, user.Id, user.UserName!, roles, permissions, roles.Contains(Roles.Curator), newRefreshId), null);
    }

    public async Task RevokeAsync(
        string rawRefreshToken, string clientIp, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var stored = await refreshTokenRepo.GetByTokenHashAsync(hash, ct);
        if (stored is not null && stored.IsActive)
            await refreshTokenRepo.RevokeAsync(stored.Id, clientIp, null, ct);
    }

    /// <summary>
    /// Resolves or creates a <see cref="PlayerAccount"/> for the supplied external-login identity,
    /// then either issues a session or (when the provider's email already belongs to an unlinked
    /// account) initiates an email-confirmation flow.
    /// </summary>
    /// <param name="provider">OAuth scheme name, e.g. <c>Discord</c>.</param>
    /// <param name="providerKey">Provider-issued subject identifier.</param>
    /// <param name="email">Email supplied by the provider, or <see langword="null"/> if not provided.</param>
    /// <param name="displayName">Display name from the provider, used as a username seed.</param>
    /// <param name="clientIp">Caller IP for refresh-token audit.</param>
    /// <param name="returnUrl">Return URL carried forward for the confirmation flow.</param>
    /// <param name="serverBaseUrl">Base URL of the server used to build the confirmation link.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ExternalLoginResult> ExternalLoginOrRegisterAsync(
        string provider, string providerKey, string? email, string? displayName,
        string clientIp, string? returnUrl = null, string? serverBaseUrl = null,
        CancellationToken ct = default)
    {
        // 1. Find an existing account by linked external login.
        var user = await userManager.FindByLoginAsync(provider, providerKey);

        // 2. Fallback: if the provider supplies an email that matches an existing account
        //    that does not yet have this provider linked, require the account owner to confirm
        //    via email before linking — prevents a hostile provider from hijacking an account.
        if (user is null && email is not null)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                await accountLinkService.RequestLinkAsync(
                    existing, provider, providerKey, displayName,
                    returnUrl, serverBaseUrl ?? string.Empty, ct);

                return new ExternalLoginResult(
                    null, null,
                    ExternalLoginStatus.PendingLinkConfirmation,
                    existing);
            }
        }

        // 3. No existing account matches — create a new one.
        if (user is null)
        {
            var baseName = SanitizeUsername(displayName ?? email ?? providerKey);
            var username = await ResolveUniqueUsernameAsync(baseName, ct);

            user = new PlayerAccount
            {
                UserName       = username,
                Email          = email,
                EmailConfirmed = email is not null,
            };

            var create = await userManager.CreateAsync(user);
            if (!create.Succeeded)
                return new ExternalLoginResult(
                    null,
                    string.Join("; ", create.Errors.Select(e => e.Description)),
                    ExternalLoginStatus.Error);
        }

        // 4. Ensure the external login is linked (idempotent — step 1 already matched).
        var logins = await userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == provider && l.ProviderKey == providerKey))
        {
            var link = await userManager.AddLoginAsync(
                user, new UserLoginInfo(provider, providerKey, provider));
            if (!link.Succeeded)
                return new ExternalLoginResult(
                    null,
                    string.Join("; ", link.Errors.Select(e => e.Description)),
                    ExternalLoginStatus.Error);
        }

        return new ExternalLoginResult(
            await IssueTokenPairAsync(user, clientIp, ct),
            null,
            ExternalLoginStatus.Success);
    }

    /// <summary>
    /// Issues a new JWT + refresh-token pair for <paramref name="user"/>.
    /// Used by the pending-link confirm endpoint after the provider has been attached.
    /// </summary>
    public Task<AuthResponse> CreateSessionAsync(
        PlayerAccount user, string clientIp, CancellationToken ct = default) =>
        IssueTokenPairAsync(user, clientIp, ct);

    /// <summary>Finds a <see cref="PlayerAccount"/> by its primary key, or returns <see langword="null"/> if not found.</summary>
    public Task<PlayerAccount?> FindUserByIdAsync(Guid accountId) =>
        userManager.FindByIdAsync(accountId.ToString());

    // ── Password Reset ────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a password-reset email if an account with the given <paramref name="email"/> exists.
    /// Always returns without disclosing whether the address is registered.
    /// </summary>
    public async Task SendPasswordResetEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user?.Email is null) return; // no info leak

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var foundryBase  = (config["Foundry:BaseUrl"] ?? string.Empty).TrimEnd('/');
        var resetLink    = $"{foundryBase}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={encodedToken}";

        var body = $"""
            <p>Hi {user.UserName},</p>
            <p>You (or someone else) requested a password reset for your Veldrath account.</p>
            <p><a href="{resetLink}">Click here to reset your password</a></p>
            <p>This link is valid for a limited time. If you did not request this, you can safely ignore this email.</p>
            """;

        await emailSender.SendAsync(user.Email, "Reset your Veldrath password", body, ct);
    }

    /// <summary>Resets the password for the account identified by <paramref name="request"/>.</summary>
    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        _ = ct;
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return (false, "Invalid or expired token.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // ── Email Confirmation ────────────────────────────────────────────────────

    /// <summary>
    /// Sends an email-confirmation message to <paramref name="user"/>.
    /// Safe to call on accounts that have no email address — silently returns without sending.
    /// </summary>
    public async Task SendEmailConfirmationAsync(PlayerAccount user, CancellationToken ct = default)
    {
        if (user.Email is null) return;

        var token        = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var foundryBase  = (config["Foundry:BaseUrl"] ?? string.Empty).TrimEnd('/');
        var confirmLink  = $"{foundryBase}/confirm-email?userId={user.Id}&token={encodedToken}";

        var body = $"""
            <p>Hi {user.UserName},</p>
            <p>Please confirm your email address to complete your Veldrath account setup.</p>
            <p><a href="{confirmLink}">Confirm my email address</a></p>
            <p>If you did not create a Veldrath account, you can safely ignore this email.</p>
            """;

        await emailSender.SendAsync(user.Email, "Confirm your Veldrath email address", body, ct);
    }

    /// <summary>Confirms the email address of the account identified by <paramref name="userId"/>.</summary>
    public async Task<(bool Ok, string? Error)> ConfirmEmailAsync(
        string userId, string token, CancellationToken ct = default)
    {
        _ = ct;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, "Invalid confirmation link.");

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? (true, null)
            : (false, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>Re-sends the email-confirmation message to the account identified by <paramref name="principal"/>.</summary>
    public async Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(
        System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var id = principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (id is null) return (false, "Account not found.");

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return (false, "Account not found.");

        if (user.EmailConfirmed) return (false, "Email already confirmed.");

        await SendEmailConfirmationAsync(user, ct);
        return (true, null);
    }

    // Private helpers

    private async Task<AuthResponse> IssueTokenPairAsync(
        PlayerAccount user, string clientIp, CancellationToken ct)
    {
        var (roles, permissions) = await ResolveRolesAndPermissionsAsync(user);
        var (jwt, expiry) = GenerateJwt(user, roles, permissions);
        var (rawRefresh, hashRefresh) = GenerateRefreshToken();
        var refreshDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");

        var refreshToken = new RefreshToken
        {
            AccountId = user.Id,
            TokenHash = hashRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays),
            CreatedByIp = clientIp,
        };
        await refreshTokenRepo.CreateAsync(refreshToken, ct);

        return new AuthResponse(jwt, rawRefresh, expiry, user.Id, user.UserName!,
            roles, permissions, roles.Contains(Roles.Curator), refreshToken.Id);
    }

    /// <summary>
    /// Resolves the full effective role and permission sets for a user.
    /// Role claims are accumulated from <see cref="RoleManager{TRole}.GetClaimsAsync"/> and
    /// per-user overrides are read from <see cref="UserManager{TUser}.GetClaimsAsync"/>.
    /// The union of both sets forms the effective permission list embedded in the JWT.
    /// </summary>
    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)>
        ResolveRolesAndPermissionsAsync(PlayerAccount user)
    {
        var roleNames = (await userManager.GetRolesAsync(user)).ToList();

        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // Collect permissions granted to each role.
        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            var roleClaims = await roleManager.GetClaimsAsync(role);
            foreach (var c in roleClaims.Where(c => c.Type == "permission"))
                permissions.Add(c.Value);
        }

        // Merge per-user permission grants (individual overrides on top of role defaults).
        var userClaims = await userManager.GetClaimsAsync(user);
        foreach (var c in userClaims.Where(c => c.Type == "permission"))
            permissions.Add(c.Value);

        return (roleNames, permissions.ToList());
    }

    private (string Jwt, DateTimeOffset Expiry) GenerateJwt(
        PlayerAccount user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions)
    {
        var keyBytes = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Embed all role names as ClaimTypes.Role so ASP.NET Core policy evaluation works.
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Embed all effective permissions so permission-based policies evaluate from the JWT
        // without a database round-trip per request.
        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims.ToArray(),
            expires: expiry.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashToken(raw));
    }

    private async Task<string> ResolveUniqueUsernameAsync(string baseName, CancellationToken ct)
    {
        _ = ct; // UserManager doesn't expose CT here but the loop is fast
        var candidate = baseName;
        var attempt   = 0;
        while (await userManager.FindByNameAsync(candidate) is not null)
            candidate = $"{baseName}{++attempt}";
        return candidate;
    }

    private static string SanitizeUsername(string raw)
    {
        var clean = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (clean.Length > 32) clean = clean[..32];
        return clean.Length > 0 ? clean : "player";
    }

    /// <summary>Returns the SHA-256 hex digest of <paramref name="rawToken"/>.</summary>
    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
