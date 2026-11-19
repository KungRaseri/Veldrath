using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Veldrath.Contracts.Account;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;

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
/// <param name="roleManager">Identity role manager, so role-derived permissions are reported too.</param>
public class AccountService(
    UserManager<PlayerAccount> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
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

        // The effective permissions, not the per-user grants alone: a role carries its permissions as
        // role claims (`Roles.DefaultPermissionsFor`), which is what the JWT enrichment honours. Reading
        // only the user's own claims reported an empty permission list for every role holder, so the
        // web app hid the whole authoring surface from a ContentEditor and the review queue from a
        // Curator — the roles were there, the permissions the layout checks were not.
        var (roles, permissions) = await RolePermissionResolver.ResolveAsync(user, userManager, roleManager);

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
