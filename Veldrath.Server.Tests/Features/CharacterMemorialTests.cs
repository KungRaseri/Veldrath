using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the A6 hardcore surfaces: the memorial roster (the account's fallen hardcore characters) and the global
/// hardcore board. Both are pure queries over <c>DeletedAt</c> and <c>DifficultyMode</c> — no new tables.
/// </summary>
public class CharacterMemorialTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static async Task<Character> SeedAsync(
        ApplicationDbContext db,
        Guid accountId,
        string name,
        string difficultyMode = "normal",
        DateTimeOffset? deletedAt = null,
        int level = 1,
        long experience = 0)
    {
        // (AccountId, SlotIndex) is unique, so each seeded character takes the next free slot.
        var slotIndex = await db.Characters.CountAsync(existing => existing.AccountId == accountId) + 1;

        var character = new Character
        {
            AccountId = accountId,
            Name = name,
            ClassName = "Warrior",
            SlotIndex = slotIndex,
            DifficultyMode = difficultyMode,
            DeletedAt = deletedAt,
            Level = level,
            Experience = experience,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db, string userName)
    {
        var account = new PlayerAccount { UserName = userName };
        account.NormalizedUserName = userName.ToUpperInvariant();
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    [Fact]
    public async Task Memorial_Returns_Only_Fallen_Hardcore_Characters_Newest_First()
    {
        await using var db = _dbFactory.CreateContext();
        var accountId = await SeedAccountAsync(db, "memorial-owner");
        var otherAccountId = await SeedAccountAsync(db, "someone-else");

        var older = await SeedAsync(db, accountId, "Fallen One", "hardcore", DateTimeOffset.UtcNow.AddDays(-2));
        var newer = await SeedAsync(db, accountId, "Fallen Two", "hardcore", DateTimeOffset.UtcNow.AddDays(-1));
        await SeedAsync(db, accountId, "Still Standing", "hardcore");
        await SeedAsync(db, accountId, "Deleted Normal", "normal", DateTimeOffset.UtcNow);
        await SeedAsync(db, otherAccountId, "Not Mine", "hardcore", DateTimeOffset.UtcNow);

        var memorial = await new CharacterRepository(db).GetMemorialAsync(accountId);

        memorial.Select(character => character.Id).Should().Equal([newer.Id, older.Id],
            "the memorial is hardcore, soft-deleted, this account only, newest death first");
    }

    [Fact]
    public async Task Leaderboard_Orders_By_Level_Then_Experience_And_Keeps_The_Fallen()
    {
        await using var db = _dbFactory.CreateContext();
        var accountId = await SeedAccountAsync(db, "board-owner");

        await SeedAsync(db, accountId, "Journeyman", "hardcore", level: 5, experience: 100);
        await SeedAsync(db, accountId, "Veteran", "hardcore", level: 10, experience: 50);
        await SeedAsync(db, accountId, "Fallen Veteran", "hardcore", DateTimeOffset.UtcNow, level: 10, experience: 900);
        await SeedAsync(db, accountId, "Softie", "normal", level: 99, experience: 9999);

        var (items, total) = await new CharacterRepository(db).GetHardcoreLeaderboardAsync(0, 25);

        total.Should().Be(3, "only hardcore characters are ranked");
        items.Select(character => character.Name)
            .Should().Equal(["Fallen Veteran", "Veteran", "Journeyman"],
                "level first, then experience, and the fallen stay on the board");
    }

    [Fact]
    public async Task Leaderboard_Paginates_Without_Changing_The_Total()
    {
        await using var db = _dbFactory.CreateContext();
        var accountId = await SeedAccountAsync(db, "paging-owner");
        for (var level = 1; level <= 5; level++)
            await SeedAsync(db, accountId, $"Runner {level}", "hardcore", level: level);

        var repository = new CharacterRepository(db);
        var (firstPage, total) = await repository.GetHardcoreLeaderboardAsync(0, 2);
        var (secondPage, totalAgain) = await repository.GetHardcoreLeaderboardAsync(2, 2);

        total.Should().Be(5);
        totalAgain.Should().Be(5);
        firstPage.Select(character => character.Name).Should().Equal(["Runner 5", "Runner 4"]);
        secondPage.Select(character => character.Name).Should().Equal(["Runner 3", "Runner 2"]);
    }

    [Fact]
    public async Task A_Soft_Deleted_Character_Can_No_Longer_Be_Loaded_For_Play()
    {
        await using var db = _dbFactory.CreateContext();
        var accountId = await SeedAccountAsync(db, "gone-owner");
        var fallen = await SeedAsync(db, accountId, "Gone", "hardcore", DateTimeOffset.UtcNow);

        var loaded = await new CharacterRepository(db).GetByIdAsync(fallen.Id);

        loaded.Should().BeNull(
            "hardcore deaths are permanent, so the loader must not hand a deleted character back to login or respawn");
    }
}
