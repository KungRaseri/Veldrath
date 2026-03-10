using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
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

        var (jwt, expiry) = GenerateJwt(user);
        return (new AuthResponse(jwt, rawNew, expiry, user.Id, user.UserName!), null);
    }

    public async Task RevokeAsync(
        string rawRefreshToken, string clientIp, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var stored = await refreshTokenRepo.GetByTokenHashAsync(hash, ct);
        if (stored is not null && stored.IsActive)
            await refreshTokenRepo.RevokeAsync(stored.Id, clientIp, null, ct);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private async Task<AuthResponse> IssueTokenPairAsync(
        PlayerAccount user, string clientIp, CancellationToken ct)
    {
        var (jwt, expiry) = GenerateJwt(user);
        var (rawRefresh, hashRefresh) = GenerateRefreshToken();
        var refreshDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");

        await refreshTokenRepo.CreateAsync(new RefreshToken
        {
            AccountId = user.Id,
            TokenHash = hashRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays),
            CreatedByIp = clientIp,
        }, ct);

        return new AuthResponse(jwt, rawRefresh, expiry, user.Id, user.UserName!);
    }

    private (string Jwt, DateTimeOffset Expiry) GenerateJwt(PlayerAccount user)
    {
        var keyBytes = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiry.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashToken(raw));
    }

    internal static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
