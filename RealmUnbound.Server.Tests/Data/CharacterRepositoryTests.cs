using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Data;

public class CharacterRepositoryTests : IDisposable
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

    private static Character MakeCharacter(Guid accountId, string name, int slot = 1) =>
        new() { AccountId = accountId, Name = name, ClassName = "@classes/warriors:fighter", SlotIndex = slot };

    // ── GetByAccountIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByAccountIdAsync_Should_Return_Empty_For_New_Account()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var result = await repo.GetByAccountIdAsync(accountId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByAccountIdAsync_Should_Return_All_Active_Characters()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeCharacter(accountId, "Char_A", 1));
        await repo.CreateAsync(MakeCharacter(accountId, "Char_B", 2));

        var result = await repo.GetByAccountIdAsync(accountId);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByAccountIdAsync_Should_Exclude_Soft_Deleted_Characters()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c1 = await repo.CreateAsync(MakeCharacter(accountId, "Char_Alive",   1));
        var c2 = await repo.CreateAsync(MakeCharacter(accountId, "Char_Deleted", 2));
        await repo.SoftDeleteAsync(c2.Id);

        var result = await repo.GetByAccountIdAsync(accountId);
        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Char_Alive");
    }

    [Fact]
    public async Task GetByAccountIdAsync_Should_Only_Return_Owned_Characters()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountA = await SeedAccountAsync(db, "Owner_A");
        var accountB = await SeedAccountAsync(db, "Owner_B");

        await repo.CreateAsync(MakeCharacter(accountA, "A_Char", 1));
        await repo.CreateAsync(MakeCharacter(accountB, "B_Char", 1));

        var result = await repo.GetByAccountIdAsync(accountA);
        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("A_Char");
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_Should_Return_Character_By_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var created = await repo.CreateAsync(MakeCharacter(accountId, "Findable", 1));

        var found = await repo.GetByIdAsync(created.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Findable");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_For_Unknown_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);

        var result = await repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_For_Soft_Deleted_Character()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "Will_Be_Deleted", 1));
        await repo.SoftDeleteAsync(c.Id);

        var result = await repo.GetByIdAsync(c.Id);
        result.Should().BeNull();
    }

    // ── GetLastPlayedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetLastPlayedAsync_Should_Return_Null_For_Empty_Account()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var result = await repo.GetLastPlayedAsync(accountId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastPlayedAsync_Should_Return_Most_Recently_Played()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeCharacter(accountId, "OldChar",    1));
        var recent = await repo.CreateAsync(MakeCharacter(accountId, "RecentChar", 2));

        // Bump LastPlayedAt on the "recent" character
        recent.LastPlayedAt = DateTimeOffset.UtcNow.AddHours(1);
        await repo.UpdateAsync(recent);

        var last = await repo.GetLastPlayedAsync(accountId);
        last!.Name.Should().Be("RecentChar");
    }

    // ── NameExistsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task NameExistsAsync_Should_Return_True_For_Existing_Name()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeCharacter(accountId, "UniqueName", 1));

        (await repo.NameExistsAsync("UniqueName")).Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_Should_Return_False_For_Unknown_Name()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);

        (await repo.NameExistsAsync("NobodyHasThisName")).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_Should_Return_False_After_Soft_Delete()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "ReclaimableName", 1));
        await repo.SoftDeleteAsync(c.Id);

        // After deletion, the name should be available again
        (await repo.NameExistsAsync("ReclaimableName")).Should().BeFalse();
    }

    // ── GetActiveCountAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveCountAsync_Should_Return_Zero_For_New_Account()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        (await repo.GetActiveCountAsync(accountId)).Should().Be(0);
    }

    [Fact]
    public async Task GetActiveCountAsync_Should_Exclude_Deleted_Characters()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        await repo.CreateAsync(MakeCharacter(accountId, "Active_C1", 1));
        var c2 = await repo.CreateAsync(MakeCharacter(accountId, "Active_C2", 2));
        await repo.SoftDeleteAsync(c2.Id);

        (await repo.GetActiveCountAsync(accountId)).Should().Be(1);
    }

    // ── SoftDeleteAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_Should_Set_DeletedAt()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "ToDelete", 1));
        await repo.SoftDeleteAsync(c.Id);

        // The deleted character should not be returned by GetByIdAsync (which filters deleted)
        var found = await repo.GetByIdAsync(c.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task SoftDeleteAsync_Should_Be_Idempotent()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "IdempotentDelete", 1));
        await repo.SoftDeleteAsync(c.Id);
        // Second call should not throw
        await repo.SoftDeleteAsync(c.Id);

        var found = await repo.GetByIdAsync(c.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task SoftDeleteAsync_Should_Silently_Ignore_Unknown_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);

        // Should not throw
        Func<Task> act = () => repo.SoftDeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ── UpdateCurrentZoneAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateCurrentZoneAsync_Should_Update_Zone_And_LastPlayedAt()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "ZoneUpdater", 1));
        var beforeUpdate = c.LastPlayedAt;

        await Task.Delay(5); // Ensure time passes
        await repo.UpdateCurrentZoneAsync(c.Id, "dungeon-1");

        var updated = await repo.GetByIdAsync(c.Id);
        updated!.CurrentZoneId.Should().Be("dungeon-1");
        updated.LastPlayedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task UpdateCurrentZoneAsync_Should_Silently_Ignore_Unknown_Id()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);

        Func<Task> act = () => repo.UpdateCurrentZoneAsync(Guid.NewGuid(), "dungeon-1");
        await act.Should().NotThrowAsync();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes()
    {
        await using var db = _factory.CreateContext();
        var repo = new CharacterRepository(db);
        var accountId = await SeedAccountAsync(db);

        var c = await repo.CreateAsync(MakeCharacter(accountId, "LevelUpChar", 1));
        c.Level = 5;
        c.Experience = 1234;
        await repo.UpdateAsync(c);

        var found = await repo.GetByIdAsync(c.Id);
        found!.Level.Should().Be(5);
        found.Experience.Should().Be(1234);
    }
}
