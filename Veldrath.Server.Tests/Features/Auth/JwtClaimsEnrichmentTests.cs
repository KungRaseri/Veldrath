using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Focused tests for <see cref="JwtClaimsEnrichment"/> — the token validation hook that decides whether a token is
/// enriched from Identity or refused. The ban branch applies to both token shapes: a token that already carries RBAC
/// claims is still a live credential for an account that may have been banned since it was minted.
/// </summary>
public class JwtClaimsEnrichmentTests
{
    private static TokenValidatedContext ContextFor(
        ClaimsPrincipal? principal, IAccountAccessReader? reader = null)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddSingleton(reader ?? new FakeAccountAccessReader(Clean(Guid.NewGuid())))
                .BuildServiceProvider(),
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

    private static AccountAccessSnapshot Clean(Guid accountId) =>
        new(accountId, "player", false, null, ["Moderator"], [Permissions.WarnPlayers]);

    private static AccountAccessSnapshot Banned(Guid accountId, DateTimeOffset? until = null) =>
        new(accountId, "player", true, until, ["Moderator"], [Permissions.WarnPlayers]);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnrichAsync_RejectsAnActiveBanWithOrWithoutRbacClaims(bool carriesRbacClaims)
    {
        var accountId = Guid.NewGuid();
        Claim[] extra = carriesRbacClaims ? [new Claim(ClaimTypes.Role, "Moderator")] : [];
        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()), .. extra], "Bearer");
        var context = ContextFor(
            new ClaimsPrincipal(identity),
            new FakeAccountAccessReader(Banned(accountId)));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().NotBeNull("a banned account's token must stop validating");
        context.Result!.Succeeded.Should().BeFalse();
        context.Result!.Failure!.Message.Should()
            .Be("This account is not permitted to use the API.", "the API must not echo the moderation note");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnrichAsync_AllowsExpiredBan(bool carriesRbacClaims)
    {
        var accountId = Guid.NewGuid();
        Claim[] extra = carriesRbacClaims ? [new Claim(ClaimTypes.Role, "Moderator")] : [];
        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()), .. extra], "Bearer");
        var context = ContextFor(
            new ClaimsPrincipal(identity),
            new FakeAccountAccessReader(Banned(accountId, DateTimeOffset.UtcNow.AddMinutes(-1))));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull("a temporary ban stops applying once its expiry passes");
    }

    [Fact]
    public async Task EnrichAsync_RejectsWhenTheAccountCannotBeResolved()
    {
        // Fail closed: no snapshot is not permission to enter.
        var context = ContextFor(
            PrincipalWithSubject(Guid.NewGuid().ToString()),
            new FakeAccountAccessReader(null));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result!.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task EnrichAsync_RejectsWhenTheLookupThrows()
    {
        var context = ContextFor(
            PrincipalWithSubject(Guid.NewGuid().ToString()),
            new FakeAccountAccessReader(Clean(Guid.NewGuid()), new InvalidOperationException("db down")));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result!.Succeeded.Should().BeFalse("a provisioning failure must not become an admitted request");
    }

    [Fact]
    public async Task EnrichAsync_DoesNotDuplicateClaimsOnAnAlreadyEnrichedToken()
    {
        var accountId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(ClaimTypes.Role, "Moderator"),
                new Claim(RolePermissionResolver.PermissionClaimType, Permissions.WarnPlayers),
            ],
            "Bearer");
        var context = ContextFor(new ClaimsPrincipal(identity), new FakeAccountAccessReader(Clean(accountId)));

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result.Should().BeNull();
        identity.Claims.Should().ContainSingle(c => c.Type == ClaimTypes.Role,
            "an already-enriched token must not be enriched twice");
        identity.Claims.Count(c => c.Type == RolePermissionResolver.PermissionClaimType).Should().Be(1);
    }

    /// <summary>Account store returning a fixed snapshot, or throwing / returning null for the failure branches.</summary>
    private sealed class FakeAccountAccessReader(
        AccountAccessSnapshot? snapshot, Exception? failure = null) : IAccountAccessReader
    {
        public Task<AccountAccessSnapshot?> EnsureAccountAsync(
            Guid accountId,
            string? username,
            string? email,
            bool emailVerified,
            CancellationToken ct = default) =>
            failure is not null
                ? Task.FromException<AccountAccessSnapshot?>(failure)
                : Task.FromResult(snapshot);

        public Task EvictAsync(Guid accountId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
