using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Handles account registration, login, token refresh, and logout.
/// JWT access tokens are short-lived (default 15 min).
/// Refresh tokens are stored as SHA-256 hashes — raw values are never persisted.
/// Presenting a previously revoked refresh token triggers immediate revocation of all
/// tokens for that account (theft detection).
/// </summary>
public class AuthService(
    UserManager<PlayerAccount> userManager,
    SignInManager<PlayerAccount> signInManager,
    IRefreshTokenRepository refreshTokenRepo,
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
            // Previously valid token now revoked → possible theft; revoke everything.
            await refreshTokenRepo.RevokeAllForAccountAsync(stored.AccountId, clientIp, ct);
            return (null, "Refresh token has been revoked");
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

        var isCurator = await userManager.IsInRoleAsync(user, "Curator");
        var (jwt, expiry) = GenerateJwt(user, isCurator);
        return (new AuthResponse(jwt, rawNew, expiry, user.Id, user.UserName!, isCurator), null);
    }

    public async Task RevokeAsync(
        string rawRefreshToken, string clientIp, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var stored = await refreshTokenRepo.GetByTokenHashAsync(hash, ct);
        if (stored is not null && stored.IsActive)
            await refreshTokenRepo.RevokeAsync(stored.Id, clientIp, null, ct);
    }

    // Internals
    public async Task<(AuthResponse? Response, string? Error)> ExternalLoginOrRegisterAsync(
        string provider, string providerKey, string? email, string? displayName,
        string clientIp, CancellationToken ct = default)
    {
        // 1. Find an existing account by linked external login.
        var user = await userManager.FindByLoginAsync(provider, providerKey);

        // 2. Fallback: match by verified email so existing accounts aren't duplicated.
        if (user is null && email is not null)
            user = await userManager.FindByEmailAsync(email);

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
                return (null, string.Join("; ", create.Errors.Select(e => e.Description)));
        }

        // 3. Ensure the external login is linked (idempotent).
        var logins = await userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == provider && l.ProviderKey == providerKey))
        {
            var link = await userManager.AddLoginAsync(
                user, new UserLoginInfo(provider, providerKey, provider));
            if (!link.Succeeded)
                return (null, string.Join("; ", link.Errors.Select(e => e.Description)));
        }

        return (await IssueTokenPairAsync(user, clientIp, ct), null);
    }

    // Internals
    private async Task<AuthResponse> IssueTokenPairAsync(
        PlayerAccount user, string clientIp, CancellationToken ct)
    {
        var isCurator = await userManager.IsInRoleAsync(user, "Curator");
        var (jwt, expiry) = GenerateJwt(user, isCurator);
        var (rawRefresh, hashRefresh) = GenerateRefreshToken();
        var refreshDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");

        await refreshTokenRepo.CreateAsync(new RefreshToken
        {
            AccountId = user.Id,
            TokenHash = hashRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays),
            CreatedByIp = clientIp,
        }, ct);

        return new AuthResponse(jwt, rawRefresh, expiry, user.Id, user.UserName!, isCurator);
    }

    private (string Jwt, DateTimeOffset Expiry) GenerateJwt(PlayerAccount user, bool isCurator = false)
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
        if (isCurator)
            claims.Add(new Claim(ClaimTypes.Role, "Curator"));

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

    internal static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
