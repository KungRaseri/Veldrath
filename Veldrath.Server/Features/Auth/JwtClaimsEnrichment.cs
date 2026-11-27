using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Enriches inbound JWT principals with the account's RBAC claims, and refuses tokens for an account that is
/// banned right now.
/// <para>
/// Tokens minted by <see cref="AuthService"/> already carry role and permission claims.
/// Tokens minted by the SvelteKit front end intentionally contain identity claims only,
/// so the game server resolves roles and permissions from the Identity tables and adds
/// them to the principal. Unknown accounts are just-in-time provisioned by
/// <see cref="AccountProvisioningService"/>; the GUID in the token remains the stable
/// account identifier in both systems.
/// </para>
/// <para>
/// The ban check runs for <em>both</em> token shapes and before any RBAC claim is added. A token that already
/// carries roles is still a bearer credential for an account that may have been banned since it was minted, so
/// skipping the lookup there would leave a banned account working for the token's whole lifetime. A failed or
/// missing lookup refuses the token rather than admitting it.
/// </para>
/// </summary>
public static class JwtClaimsEnrichment
{
    /// <summary>
    /// <see cref="JwtBearerEvents.OnTokenValidated"/> handler that provisions the account
    /// (when needed), refuses an active ban, and appends role and permission claims sourced from Identity.
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

        var services = context.HttpContext.RequestServices;
        var accounts = services.GetRequiredService<IAccountAccessReader>();

        var username = context.Principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                    ?? context.Principal.FindFirstValue(ClaimTypes.Name);
        var email = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                 ?? context.Principal.FindFirstValue(ClaimTypes.Email);
        var emailVerified = bool.TryParse(
            context.Principal.FindFirstValue("email_verified"), out var verified) && verified;

        AccountAccessSnapshot? snapshot;
        try
        {
            snapshot = await accounts.EnsureAccountAsync(
                accountId, username, email, emailVerified, context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Never let provisioning turn an authenticated request into an opaque 500: log the
            // cause (the client only sees a rejected token) and reject the token explicitly.
            // The token subject is deliberately kept out of the message: it is user data, and
            // the request log already correlates the failure with the caller (CodeQL cs/cleartext-storage).
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(JwtClaimsEnrichment))
                .LogError(ex, "Account provisioning threw while enriching an authenticated token.");
            context.Fail("The account for this token could not be provisioned.");
            return;
        }

        if (snapshot is null)
        {
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(JwtClaimsEnrichment))
                .LogWarning("Account provisioning returned no snapshot for an authenticated token.");
            context.Fail("The account for this token could not be provisioned.");
            return;
        }

        if (snapshot.IsActivelyBanned(DateTimeOffset.UtcNow))
        {
            // A generic reason only: the ban reason is shown by the hub's connect-time guard, and the API must not
            // become a way to read moderation notes from a bearer token.
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(JwtClaimsEnrichment))
                .LogWarning("Refused an authenticated token for an actively banned account.");
            context.Fail("This account is not permitted to use the API.");
            return;
        }

        // Tokens issued by Veldrath.Server already embed roles and permissions, so they need no further enrichment —
        // but they still had to pass the ban check above.
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role) ||
            identity.HasClaim(c => c.Type == RolePermissionResolver.PermissionClaimType))
            return;

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
