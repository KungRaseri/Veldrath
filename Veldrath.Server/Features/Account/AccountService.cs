using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Veldrath.Contracts.Account;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Account;

/// <summary>
/// Exposes the server's RBAC view of an account.
/// </summary>
/// <remarks>
/// Better Auth (the SvelteKit app) owns credentials, registration, login, profile edits, linked
/// providers, and sessions. What remains here is the part the server is authoritative for: the
/// Identity row that carries role and permission claims used by the JWT enrichment and the admin
/// endpoints.
/// </remarks>
/// <param name="userManager">Identity user manager used to read roles and permission claims.</param>
public class AccountService(UserManager<PlayerAccount> userManager)
{
    /// <summary>
    /// Builds the <see cref="AccountProfileDto"/> for the authenticated principal.
    /// </summary>
    /// <param name="principal">Authenticated principal whose <see cref="ClaimTypes.NameIdentifier"/> is the account id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profile, or <see langword="null"/> when no Identity row exists for the account.</returns>
    public async Task<AccountProfileDto?> GetProfileAsync(
        ClaimsPrincipal principal, CancellationToken ct = default)
    {
        _ = ct;
        var user = await ResolveUserAsync(principal);
        if (user is null) return null;

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var permissions = (await userManager.GetClaimsAsync(user))
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value)
            .ToList();

        return new AccountProfileDto(
            user.Id,
            user.UserName!,
            user.DisplayName,
            user.Email,
            roles,
            permissions);
    }

    private async Task<PlayerAccount?> ResolveUserAsync(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id)
            ? await userManager.FindByIdAsync(id.ToString())
            : null;
    }
}
