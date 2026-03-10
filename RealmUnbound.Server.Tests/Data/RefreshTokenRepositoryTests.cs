using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Data;

public class RefreshTokenRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>Persists a minimal PlayerAccount and returns its Id.</summary>
    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db, string? tag = null)
    {
        var name = tag ?? $"Acct_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static RefreshToken MakeToken(Guid accountId, string hash, bool expired = false) =>
        new()
        {
            AccountId    = accountId,
            TokenHash    = hash,
            ExpiresAt    = expired
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.UtcNow.AddDays(30),
            CreatedByIp  = "127.0.0.1",
        };

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Should_Persist_Token()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var token = await repo.CreateAsync(MakeToken(accountId, "hash_abc"));

        token.Id.Should().NotBe(Guid.Empty);
        token.TokenHash.Should().Be("hash_abc");
    }

    // ── GetByTokenHashAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByTokenHashAsync_Should_Return_Token_By_Hash()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeToken(accountId, "find_by_hash"));

        var result = await repo.GetByTokenHashAsync("find_by_hash");
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task GetByTokenHashAsync_Should_Return_Null_For_Unknown_Hash()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);

        var result = await repo.GetByTokenHashAsync("no_such_hash");
        result.Should().BeNull();
    }

    // ── IsActive ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Token_Should_Be_Active_When_Not_Revoked_And_Not_Expired()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var token = await repo.CreateAsync(MakeToken(accountId, "active_token_hash"));
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Token_Should_Be_Inactive_When_Expired()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var token = await repo.CreateAsync(MakeToken(accountId, "expired_token_hash", expired: true));
        token.IsActive.Should().BeFalse();
    }

    // ── RevokeAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_Should_Mark_Token_Revoked()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var token = await repo.CreateAsync(MakeToken(accountId, "revoke_me_hash"));
        await repo.RevokeAsync(token.Id, "127.0.0.1");

        var retrieved = await repo.GetByTokenHashAsync("revoke_me_hash");
        retrieved!.IsActive.Should().BeFalse();
        retrieved.RevokedAt.Should().NotBeNull();
        retrieved.RevokedByIp.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task RevokeAsync_Should_Record_Replacement_Token_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var original = await repo.CreateAsync(MakeToken(accountId, "original_hash"));
        var replacement = await repo.CreateAsync(MakeToken(accountId, "replacement_hash"));

        await repo.RevokeAsync(original.Id, "127.0.0.1", replacedByTokenId: replacement.Id);

        var retrieved = await repo.GetByTokenHashAsync("original_hash");
        retrieved!.ReplacedByTokenId.Should().Be(replacement.Id);
    }

    [Fact]
    public async Task RevokeAsync_Should_Be_Idempotent_For_Already_Revoked_Token()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        var token = await repo.CreateAsync(MakeToken(accountId, "double_revoke_hash"));
        await repo.RevokeAsync(token.Id, "127.0.0.1");
        // Second call should not throw
        Func<Task> act = () => repo.RevokeAsync(token.Id, "127.0.0.2");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAsync_Should_Silently_Ignore_Unknown_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);

        Func<Task> act = () => repo.RevokeAsync(Guid.NewGuid(), "127.0.0.1");
        await act.Should().NotThrowAsync();
    }

    // ── RevokeAllForAccountAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RevokeAllForAccountAsync_Should_Revoke_All_Active_Tokens()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeToken(accountId, "bulk_hash_1"));
        await repo.CreateAsync(MakeToken(accountId, "bulk_hash_2"));
        await repo.CreateAsync(MakeToken(accountId, "bulk_hash_3"));

        await repo.RevokeAllForAccountAsync(accountId, "10.0.0.1");

        var t1 = await repo.GetByTokenHashAsync("bulk_hash_1");
        var t2 = await repo.GetByTokenHashAsync("bulk_hash_2");
        var t3 = await repo.GetByTokenHashAsync("bulk_hash_3");

        t1!.IsActive.Should().BeFalse();
        t2!.IsActive.Should().BeFalse();
        t3!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAllForAccountAsync_Should_Not_Affect_Other_Accounts()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);
        var accountA = await SeedAccountAsync(db, "BulkRevoke_A");
        var accountB = await SeedAccountAsync(db, "BulkRevoke_B");

        await repo.CreateAsync(MakeToken(accountA, "acct_a_hash"));
        await repo.CreateAsync(MakeToken(accountB, "acct_b_hash"));

        await repo.RevokeAllForAccountAsync(accountA, "127.0.0.1");

        var tokenB = await repo.GetByTokenHashAsync("acct_b_hash");
        tokenB!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAllForAccountAsync_Should_Succeed_When_No_Active_Tokens()
    {
        await using var db = _factory.CreateContext();
        var repo = new RefreshTokenRepository(db);

        Func<Task> act = () => repo.RevokeAllForAccountAsync(Guid.NewGuid(), "127.0.0.1");
        await act.Should().NotThrowAsync();
    }
}
