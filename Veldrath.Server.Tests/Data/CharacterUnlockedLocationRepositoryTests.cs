using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

[Trait("Category", "Repository")]
public class CharacterUnlockedLocationRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db)
    {
        var name    = $"Acct_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> SeedCharacterAsync(ApplicationDbContext db, Guid accountId)
    {
        var slot = db.Characters.Count(c => c.AccountId == accountId) + 1;
        var c = new Character { AccountId = accountId, Name = $"Char_{Guid.NewGuid():N}", ClassName = "Warrior", SlotIndex = slot };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    // GetUnlockedSlugsAsync
    [Fact]
    public async Task GetUnlockedSlugsAsync_ReturnsEmpty_ForNewCharacter()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        var result = await repo.GetUnlockedSlugsAsync(characterId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnlockedSlugsAsync_ReturnsAllUnlockedSlugs()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        await repo.AddUnlockAsync(characterId, "secret-cave",   "quest");
        await repo.AddUnlockAsync(characterId, "hidden-shrine", "item");

        var result = await repo.GetUnlockedSlugsAsync(characterId);

        result.Should().HaveCount(2);
        result.Should().Contain("secret-cave");
        result.Should().Contain("hidden-shrine");
    }

    [Fact]
    public async Task GetUnlockedSlugsAsync_IsScopedToCharacter()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var charA           = await SeedCharacterAsync(db, accountId);
        var charB           = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        await repo.AddUnlockAsync(charA, "secret-cave", "quest");

        var resultA = await repo.GetUnlockedSlugsAsync(charA);
        var resultB = await repo.GetUnlockedSlugsAsync(charB);

        resultA.Should().Contain("secret-cave");
        resultB.Should().BeEmpty();
    }

    // IsUnlockedAsync
    [Fact]
    public async Task IsUnlockedAsync_ReturnsFalse_WhenNotUnlocked()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        var result = await repo.IsUnlockedAsync(characterId, "secret-cave");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUnlockedAsync_ReturnsTrue_AfterUnlock()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        await repo.AddUnlockAsync(characterId, "secret-cave", "quest");
        var result = await repo.IsUnlockedAsync(characterId, "secret-cave");

        result.Should().BeTrue();
    }

    // AddUnlockAsync
    [Fact]
    public async Task AddUnlockAsync_PersistsUnlockRow()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        await repo.AddUnlockAsync(characterId, "hidden-ruins", "manual");

        db.CharacterUnlockedLocations.Should().ContainSingle(u =>
            u.CharacterId == characterId &&
            u.LocationSlug == "hidden-ruins" &&
            u.UnlockSource == "manual");
    }

    [Fact]
    public async Task AddUnlockAsync_IgnoresDuplicate_DoesNotThrow()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);

        await repo.AddUnlockAsync(characterId, "secret-cave", "quest");
        var act = () => repo.AddUnlockAsync(characterId, "secret-cave", "quest");
        await act.Should().NotThrowAsync();

        db.CharacterUnlockedLocations.Count(u =>
            u.CharacterId == characterId && u.LocationSlug == "secret-cave")
            .Should().Be(1);
    }

    [Fact]
    public async Task AddUnlockAsync_SetsUnlockSourceAndTimestamp()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var characterId     = await SeedCharacterAsync(db, accountId);
        var repo            = new CharacterUnlockedLocationRepository(db);
        var before          = DateTimeOffset.UtcNow.AddSeconds(-1);

        await repo.AddUnlockAsync(characterId, "mystic-altar", "skill_check_active");

        var row = db.CharacterUnlockedLocations.Single(u => u.LocationSlug == "mystic-altar");
        row.UnlockSource.Should().Be("skill_check_active");
        row.UnlockedAt.Should().BeOnOrAfter(before);
    }
}
