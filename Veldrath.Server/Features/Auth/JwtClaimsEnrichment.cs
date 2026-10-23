using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Enriches inbound JWT principals with the account's RBAC claims.
/// <para>
/// Tokens minted by <see cref="AuthService"/> already carry role and permission claims.
/// Tokens minted by the SvelteKit front end intentionally contain identity claims only,
/// so the game server resolves roles and permissions from the Identity tables and adds
/// them to the principal. Unknown accounts are just-in-time provisioned by
/// <see cref="AccountProvisioningService"/>; the GUID in the token remains the stable
/// account identifier in both systems.
/// </para>
/// </summary>
public static class JwtClaimsEnrichment
{
    /// <summary>
    /// <see cref="JwtBearerEvents.OnTokenValidated"/> handler that provisions the account
    /// (when needed) and appends role and permission claims sourced from Identity.
    /// </summary>
    /// <param name="context">The token validation context supplied by the JWT bearer handler.</param>
    /// <returns>A task that completes when enrichment has finished.</returns>
    public static async Task EnrichAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var subject = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var accountId))
        {
            context.Fail("Account identifier claim is missing or is not a GUID.");
            return;
        }

        // Tokens issued by Veldrath.Server already embed roles and permissions for an
        // account that is known to exist, so avoid the database round-trip.
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role) ||
            identity.HasClaim(c => c.Type == RolePermissionResolver.PermissionClaimType))
            return;

        var services = context.HttpContext.RequestServices;
        var provisioning = services.GetRequiredService<AccountProvisioningService>();

        var username = context.Principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                    ?? context.Principal.FindFirstValue(ClaimTypes.Name);
        var email = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                 ?? context.Principal.FindFirstValue(ClaimTypes.Email);
        var emailVerified = bool.TryParse(
            context.Principal.FindFirstValue("email_verified"), out var verified) && verified;

        var snapshot = await provisioning.EnsureAccountAsync(
            accountId, username, email, emailVerified, context.HttpContext.RequestAborted);

        if (snapshot is null)
        {
            context.Fail("The account for this token could not be provisioned.");
            return;
        }

        foreach (var role in snapshot.Roles)
        {
            if (!identity.HasClaim(ClaimTypes.Role, role))
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in snapshot.Permissions)
        {
            if (!identity.HasClaim(RolePermissionResolver.PermissionClaimType, permission))
                identity.AddClaim(new Claim(RolePermissionResolver.PermissionClaimType, permission));
        }
    }
}
