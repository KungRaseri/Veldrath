using Microsoft.AspNetCore.Identity;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Resolves the effective role and permission sets for a <see cref="PlayerAccount"/>.
/// Shared by token issuance (<see cref="AuthService"/>) and inbound token enrichment so
/// both paths always agree on how RBAC claims are derived.
/// </summary>
public static class RolePermissionResolver
{
    /// <summary>Claim type used for fine-grained permission grants.</summary>
    public const string PermissionClaimType = "permission";

    /// <summary>
    /// Resolves the full effective role and permission sets for a user.
    /// Role claims are accumulated from <see cref="RoleManager{TRole}.GetClaimsAsync"/> and
    /// per-user overrides are read from <see cref="UserManager{TUser}.GetClaimsAsync"/>.
    /// The union of both sets forms the effective permission list embedded in the JWT.
    /// </summary>
    /// <param name="user">The account whose access should be resolved.</param>
    /// <param name="userManager">Identity user manager used for role and claim lookups.</param>
    /// <param name="roleManager">Identity role manager used for role claim lookups.</param>
    /// <returns>The account's role names and the union of role and user permission grants.</returns>
    public static async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveAsync(
        PlayerAccount user,
        UserManager<PlayerAccount> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        var roleNames = (await userManager.GetRolesAsync(user)).ToList();
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // Collect permissions granted to each role.
        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            var roleClaims = await roleManager.GetClaimsAsync(role);
            foreach (var c in roleClaims.Where(c => c.Type == PermissionClaimType))
                permissions.Add(c.Value);
        }

        // Merge per-user permission grants (individual overrides on top of role defaults).
        var userClaims = await userManager.GetClaimsAsync(user);
        foreach (var c in userClaims.Where(c => c.Type == PermissionClaimType))
            permissions.Add(c.Value);

        return (roleNames, permissions.ToList());
    }
}
