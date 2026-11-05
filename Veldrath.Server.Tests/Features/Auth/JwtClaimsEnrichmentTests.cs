using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Focused tests for the early-exit and rejection branches of <see cref="JwtClaimsEnrichment"/> — the token
/// validation hook that decides whether a Better Auth minted token is enriched from Identity or refused.
/// </summary>
public class JwtClaimsEnrichmentTests
{
    private static TokenValidatedContext ContextFor(ClaimsPrincipal? principal)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        return new TokenValidatedContext(
            httpContext,
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = principal,
        };
    }

    private static ClaimsPrincipal PrincipalWithSubject(string? subject, params Claim[] extraClaims)
    {
        var claims = new List<Claim>(extraClaims);
        if (subject is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    [Fact]
    public async Task NoPrincipal_IsLeftUnchanged()
    {
        var context = ContextFor(null);

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull("there is nothing to enrich and no reason to reject the token");
    }

    [Fact]
    public async Task PrincipalWithoutClaimsIdentity_IsLeftUnchanged()
    {
        var context = ContextFor(new ClaimsPrincipal());

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task MissingSubject_FailsTheToken()
    {
        var context = ContextFor(PrincipalWithSubject(null));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().NotBeNull();
        context.Result!.Succeeded.Should().BeFalse();
        context.Result!.Failure!.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task NonGuidSubject_FailsTheToken()
    {
        var context = ContextFor(PrincipalWithSubject("not-a-guid"));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result!.Failure!.Message.Should().Be("Account identifier claim is missing or is not a GUID.");
    }

    [Fact]
    public async Task TokenThatAlreadyCarriesRoles_SkipsTheDatabaseRoundTrip()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Moderator"),
            ],
            "Bearer");
        var context = ContextFor(new ClaimsPrincipal(identity));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull();
        identity.Claims.Should().ContainSingle(c => c.Type == ClaimTypes.Role,
            "an already-enriched token must not be enriched twice");
    }

    [Fact]
    public async Task TokenThatAlreadyCarriesPermissions_SkipsTheDatabaseRoundTrip()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(RolePermissionResolver.PermissionClaimType, Permissions.ManageUsers),
            ],
            "Bearer");
        var context = ContextFor(new ClaimsPrincipal(identity));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull();
        identity.Claims.Count(c => c.Type == RolePermissionResolver.PermissionClaimType).Should().Be(1);
    }

    [Fact]
    public async Task NonGuidSubjectIsRejectedEvenWhenOtherClaimsArePresent()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, "someone@example.com"),
                new Claim(ClaimTypes.NameIdentifier, "someone@example.com"),
                new Claim(ClaimTypes.Name, "someone"),
            ],
            "Bearer");
        var context = ContextFor(new ClaimsPrincipal(identity));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result!.Succeeded.Should().BeFalse("Better Auth tokens must carry a GUID subject");
    }
}
