using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Tests for <see cref="AccountProvisioningService"/> and <see cref="JwtClaimsEnrichment"/>.
/// Covers just-in-time account creation for external (Better Auth) identities and the
/// RBAC claim enrichment applied to identity-only JWTs.
/// </summary>
public class AccountProvisioningServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly ServiceProvider _provider;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<PlayerAccount> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AccountProvisioningService _service;

    public AccountProvisioningServiceTests()
    {
        _db = _dbFactory.CreateContext();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_db);
        services
            .AddIdentityCore<PlayerAccount>(o =>
            {
                o.Password.RequireDigit          = false;
                o.Password.RequiredLength        = 1;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase      = false;
                o.User.RequireUniqueEmail        = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton(sp => new AccountProvisioningService(
            sp.GetRequiredService<UserManager<PlayerAccount>>(),
            sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            _db,
            Mock.Of<ILogger<AccountProvisioningService>>()));

        _provider    = services.BuildServiceProvider();
        _userManager = _provider.GetRequiredService<UserManager<PlayerAccount>>();
        _roleManager = _provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        _service     = _provider.GetRequiredService<AccountProvisioningService>();

        AccountProvisioningService.ClearCache();
    }

    public void Dispose()
    {
        AccountProvisioningService.ClearCache();
        _provider.Dispose();
        _db.Dispose();
        _dbFactory.Dispose();
    }

    // ── Provisioning ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAccount_Creates_Identity_Row_With_Token_Guid()
    {
        var accountId = Guid.NewGuid();

        var snapshot = await _service.EnsureAccountAsync(
            accountId, "DiscordUser", "discord@example.com", emailVerified: true);

        snapshot.Should().NotBeNull();
        snapshot!.AccountId.Should().Be(accountId);
        snapshot.Username.Should().Be("DiscordUser");

        var user = await _userManager.FindByIdAsync(accountId.ToString());
        user.Should().NotBeNull();
        user!.UserName.Should().Be("DiscordUser");
        user.Email.Should().Be("discord@example.com");
        user.EmailConfirmed.Should().BeTrue();
        user.PasswordHash.Should().BeNull("credentials are owned by Better Auth, not Identity");
    }

    [Fact]
    public async Task EnsureAccount_Is_Idempotent_For_Same_Account()
    {
        var accountId = Guid.NewGuid();

        await _service.EnsureAccountAsync(accountId, "First", null, emailVerified: false);
        AccountProvisioningService.ClearCache();
        var second = await _service.EnsureAccountAsync(accountId, "Renamed", null, emailVerified: false);

        second.Should().NotBeNull();
        second!.Username.Should().Be("First");

        var matches = await _db.Users.CountAsync(u => u.Id == accountId);
        matches.Should().Be(1);
    }

    [Fact]
    public async Task EnsureAccount_Sanitizes_And_Deduplicates_Username()
    {
        var occupied = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            UserName = "Taken",
            Email = "taken@example.com",
        };
        var occupiedResult = await _userManager.CreateAsync(occupied);
        occupiedResult.Succeeded.Should().BeTrue(
            because: string.Join("; ", occupiedResult.Errors.Select(e => $"{e.Code}: {e.Description}")));

        var snapshot = await _service.EnsureAccountAsync(
            Guid.NewGuid(), "Taken!", null, emailVerified: false);

        snapshot.Should().NotBeNull();
        snapshot!.Username.Should().Be("Taken1");
    }

    [Fact]
    public async Task EnsureAccount_Falls_Back_When_Email_Is_Already_Owned()
    {
        var existing = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            UserName = "Existing",
            Email = "shared@example.com",
        };
        var existingResult = await _userManager.CreateAsync(existing);
        existingResult.Succeeded.Should().BeTrue(
            because: string.Join("; ", existingResult.Errors.Select(e => $"{e.Code}: {e.Description}")));

        var accountId = Guid.NewGuid();
        var snapshot = await _service.EnsureAccountAsync(
            accountId, "NewPlayer", "shared@example.com", emailVerified: true);

        snapshot.Should().NotBeNull();
        var created = await _userManager.FindByIdAsync(accountId.ToString());
        created.Should().NotBeNull();
        created!.Email.Should().Be(
            $"{accountId:N}@external.veldrath.local",
            "the colliding email must not be attached twice and Identity requires a unique address");
        created.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureAccount_Resolves_Role_And_User_Permissions()
    {
        await _roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Curator));
        var role = await _roleManager.FindByNameAsync(Roles.Curator);
        await _roleManager.AddClaimAsync(
            role!, new Claim(RolePermissionResolver.PermissionClaimType, Permissions.ReviewSubmissions));

        var accountId = Guid.NewGuid();
        await _service.EnsureAccountAsync(accountId, "Curator1", null, emailVerified: false);

        var user = await _userManager.FindByIdAsync(accountId.ToString());
        (await _userManager.AddToRoleAsync(user!, Roles.Curator)).Succeeded.Should().BeTrue();
        (await _userManager.AddClaimAsync(
            user!, new Claim(RolePermissionResolver.PermissionClaimType, Permissions.ViewPlayers)))
            .Succeeded.Should().BeTrue();

        AccountProvisioningService.ClearCache();
        var snapshot = await _service.EnsureAccountAsync(accountId, "Curator1", null, emailVerified: false);

        snapshot.Should().NotBeNull();
        snapshot!.Roles.Should().Contain(Roles.Curator);
        snapshot.Permissions.Should().Contain(Permissions.ReviewSubmissions);
        snapshot.Permissions.Should().Contain(Permissions.ViewPlayers);
    }

    // ── Claims enrichment ────────────────────────────────────────────────────

    [Fact]
    public async Task Enrichment_Adds_Role_And_Permission_Claims_To_Identity_Only_Token()
    {
        await _roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Moderator));
        var role = await _roleManager.FindByNameAsync(Roles.Moderator);
        await _roleManager.AddClaimAsync(
            role!, new Claim(RolePermissionResolver.PermissionClaimType, Permissions.KickPlayers));

        var accountId = Guid.NewGuid();
        await _service.EnsureAccountAsync(accountId, "moderator_user", "mod@example.com", emailVerified: false);
        var user = await _userManager.FindByIdAsync(accountId.ToString());
        (await _userManager.AddToRoleAsync(user!, Roles.Moderator)).Succeeded.Should().BeTrue();
        AccountProvisioningService.ClearCache();

        var principal = BuildPrincipal(accountId, "moderator_user");
        var context   = BuildTokenContext(principal);

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result?.Failure.Should().BeNull();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.HasClaim(ClaimTypes.Role, Roles.Moderator).Should().BeTrue();
        identity.HasClaim(RolePermissionResolver.PermissionClaimType, Permissions.KickPlayers)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Enrichment_Fails_When_Subject_Is_Not_A_Guid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")], "Bearer"));
        var context = BuildTokenContext(principal);

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result?.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task Enrichment_Skips_Database_When_Token_Already_Carries_Roles()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, Roles.Admin),
            ], "Bearer"));

        // An empty service provider proves no provisioning lookup happens on this path.
        var context = BuildTokenContext(principal, new ServiceCollection().BuildServiceProvider());

        await JwtClaimsEnrichment.EnrichAsync(context);

        context.Result?.Failure.Should().BeNull();
        ((ClaimsIdentity)principal.Identity!).HasClaim(ClaimTypes.Role, Roles.Admin).Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildPrincipal(Guid accountId, string username) =>
        new(new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
            ], "Bearer"));

    private TokenValidatedContext BuildTokenContext(
        ClaimsPrincipal principal,
        IServiceProvider? requestServices = null)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices ?? _provider,
        };

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        return new TokenValidatedContext(httpContext, scheme, new JwtBearerOptions())
        {
            Principal = principal,
        };
    }
}
