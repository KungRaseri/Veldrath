using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Auth;
using RealmUnbound.Server.Infrastructure.Email;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features.Auth;

/// <summary>Unit tests for <see cref="AccountLinkService"/>.</summary>
public class AccountLinkServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    // ── RequestLinkAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RequestLink_Creates_PendingToken_And_Sends_Email()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo = new EfCorePendingLinkRepository(db);

        var capturedTo      = string.Empty;
        var capturedSubject = string.Empty;
        var capturedBody    = string.Empty;

        var emailMock = new Mock<IEmailSender>();
        emailMock
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((to, sub, body, _) =>
            {
                capturedTo      = to;
                capturedSubject = sub;
                capturedBody    = body;
            })
            .Returns(Task.CompletedTask);

        var userMgr   = BuildUserManager(db);
        var config    = BuildConfig();
        var svc       = new AccountLinkService(repo, emailMock.Object, userMgr, config);

        var account       = await CreateAccountAsync(userMgr, "linktest@example.com", "LinkTestUser");
        const string serverBase = "http://localhost:5000";

        // Act
        await svc.RequestLinkAsync(account, "Discord", "disc-key-123", "TestDiscordUser",
            returnUrl: null, serverBaseUrl: serverBase);

        // Assert — email was sent to the account's address
        capturedTo.Should().Be("linktest@example.com");
        capturedSubject.Should().Contain("Discord");
        capturedBody.Should().Contain($"{serverBase}/api/auth/link/confirm?token=");

        // Assert — a PendingLinkToken row was persisted
        var tokens = db.PendingLinkTokens.Where(t => t.AccountId == account.Id).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].LoginProvider.Should().Be("Discord");
        tokens[0].ProviderKey.Should().Be("disc-key-123");
        tokens[0].IsConfirmed.Should().BeFalse();
        tokens[0].ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RequestLink_Uses_Configured_ExpiryMinutes()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo     = new EfCorePendingLinkRepository(db);
        var email    = Mock.Of<IEmailSender>();
        var userMgr  = BuildUserManager(db);
        var config   = BuildConfig(pendingLinkExpiryMinutes: 15);
        var svc      = new AccountLinkService(repo, email, userMgr, config);

        var account = await CreateAccountAsync(userMgr, "expiry@example.com", "ExpiryUser");

        // Act
        await svc.RequestLinkAsync(account, "Google", "google-key", null, null, "http://localhost");

        // Assert — expiry is approximately 15 minutes from now
        var token = db.PendingLinkTokens.Single(t => t.AccountId == account.Id);
        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    // ── ConfirmAndLinkAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAndLink_ValidToken_Links_Provider_And_Marks_Confirmed()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo     = new EfCorePendingLinkRepository(db);
        var email    = Mock.Of<IEmailSender>();
        var userMgr  = BuildUserManager(db);
        var config   = BuildConfig();
        var svc      = new AccountLinkService(repo, email, userMgr, config);

        var account     = await CreateAccountAsync(userMgr, "confirm@example.com", "ConfirmUser");
        var rawToken    = GenerateRawToken();
        var tokenHash   = AuthService.HashToken(rawToken);

        db.PendingLinkTokens.Add(new PendingLinkToken
        {
            AccountId     = account.Id,
            LoginProvider = "Google",
            ProviderKey   = "goog-subj-1",
            TokenHash     = tokenHash,
            Email         = account.Email!,
            ExpiresAt     = DateTimeOffset.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();

        // Act
        var (resultAccount, resultToken, error) = await svc.ConfirmAndLinkAsync(rawToken);

        // Assert — success, no error
        error.Should().BeNull();
        resultAccount.Should().NotBeNull();
        resultAccount!.Id.Should().Be(account.Id);
        resultToken.Should().NotBeNull();

        // Assert — provider is now linked
        var logins = await userMgr.GetLoginsAsync(resultAccount);
        logins.Should().ContainSingle(l => l.LoginProvider == "Google" && l.ProviderKey == "goog-subj-1");

        // Assert — token is marked confirmed (cannot be reused)
        db.ChangeTracker.Clear();
        var persisted = db.PendingLinkTokens.Single(t => t.TokenHash == tokenHash);
        persisted.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAndLink_ExpiredToken_Returns_Error()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo   = new EfCorePendingLinkRepository(db);
        var svc    = new AccountLinkService(repo, Mock.Of<IEmailSender>(), BuildUserManager(db), BuildConfig());

        var account   = await CreateAccountAsync(BuildUserManager(db), "expired@example.com", "ExpiredUser");
        var rawToken  = GenerateRawToken();

        db.PendingLinkTokens.Add(new PendingLinkToken
        {
            AccountId     = account.Id,
            LoginProvider = "Discord",
            ProviderKey   = "disc-expired",
            TokenHash     = AuthService.HashToken(rawToken),
            Email         = account.Email!,
            ExpiresAt     = DateTimeOffset.UtcNow.AddHours(-1), // already expired
        });
        await db.SaveChangesAsync();

        // Act
        var (resultAccount, _, error) = await svc.ConfirmAndLinkAsync(rawToken);

        // Assert
        resultAccount.Should().BeNull();
        error.Should().Be("link_expired");
    }

    [Fact]
    public async Task ConfirmAndLink_AlreadyConfirmedToken_Returns_Error()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo = new EfCorePendingLinkRepository(db);
        var svc  = new AccountLinkService(repo, Mock.Of<IEmailSender>(), BuildUserManager(db), BuildConfig());

        var account  = await CreateAccountAsync(BuildUserManager(db), "alreadydone@example.com", "AlreadyDoneUser");
        var rawToken = GenerateRawToken();

        db.PendingLinkTokens.Add(new PendingLinkToken
        {
            AccountId     = account.Id,
            LoginProvider = "Microsoft",
            ProviderKey   = "ms-key-dup",
            TokenHash     = AuthService.HashToken(rawToken),
            Email         = account.Email!,
            ExpiresAt     = DateTimeOffset.UtcNow.AddHours(1),
            IsConfirmed   = true, // already used
        });
        await db.SaveChangesAsync();

        // Act
        var (resultAccount, _, error) = await svc.ConfirmAndLinkAsync(rawToken);

        // Assert
        resultAccount.Should().BeNull();
        error.Should().Be("link_already_confirmed");
    }

    [Fact]
    public async Task ConfirmAndLink_UnknownToken_Returns_Error()
    {
        // Arrange
        using var db = _dbFactory.CreateContext();
        var repo = new EfCorePendingLinkRepository(db);
        var svc  = new AccountLinkService(repo, Mock.Of<IEmailSender>(), BuildUserManager(db), BuildConfig());

        var rawToken = GenerateRawToken(); // never persisted

        // Act
        var (resultAccount, _, error) = await svc.ConfirmAndLinkAsync(rawToken);

        // Assert
        resultAccount.Should().BeNull();
        error.Should().Be("link_invalid");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserManager<PlayerAccount> BuildUserManager(RealmUnbound.Server.Data.ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services
            .AddIdentityCore<PlayerAccount>(o =>
            {
                o.Password.RequireDigit           = true;
                o.Password.RequiredLength          = 8;
                o.Password.RequireNonAlphanumeric  = true;
                o.Password.RequireUppercase        = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RealmUnbound.Server.Data.ApplicationDbContext>();

        return services.BuildServiceProvider().GetRequiredService<UserManager<PlayerAccount>>();
    }

    private static IConfiguration BuildConfig(int pendingLinkExpiryMinutes = 60)
    {
        var dict = new Dictionary<string, string?> { ["Auth:PendingLinkExpiryMinutes"] = pendingLinkExpiryMinutes.ToString() };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static async Task<PlayerAccount> CreateAccountAsync(
        UserManager<PlayerAccount> userMgr, string email, string username)
    {
        var account = new PlayerAccount { UserName = username, Email = email, EmailConfirmed = true };
        var result  = await userMgr.CreateAsync(account, "TestP@ss1!");
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    private static string GenerateRawToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
