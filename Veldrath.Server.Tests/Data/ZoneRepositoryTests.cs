using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

public class ZoneRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // Helpers
    private static Zone MakeZone(string id, string name, bool isStarter = false) =>
        new()
        {
            Id        = id,
            Name      = name,
            IsStarter = isStarter,
            Type      = ZoneType.Wilderness,
        };

    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db)
    {
        var name    = $"Acct_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, Guid accountId, int slotIndex = 1)
    {
        var character = new Character
        {
            AccountId = accountId,
            Name      = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = slotIndex,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    // ZoneRepository
    [Fact]
    public async Task GetAllAsync_Should_Return_All_Zones()
    {
        await using var db = _factory.CreateContext();
        // DB is pre-seeded with 18 zones; verify all are returned
        var repo   = new ZoneRepository(db);
        var result = await repo.GetAllAsync();

        result.Should().HaveCount(18);
        result.Should().Contain(z => z.Id == "crestfall");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Starter_Zones_First()
    {
        await using var db = _factory.CreateContext();
        // Seeded data has "crestfall" as the only starter zone
        var repo   = new ZoneRepository(db);
        var result = await repo.GetAllAsync();

        result.Should().NotBeEmpty();
        result[0].IsStarter.Should().BeTrue();
        result.Skip(1).Should().OnlyContain(z => !z.IsStarter);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_When_No_Zones()
    {
        await using var db = _factory.CreateContext();
        db.Zones.RemoveRange(db.Zones);
        await db.SaveChangesAsync();

        var repo   = new ZoneRepository(db);
        var result = await repo.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Zone_When_Found()
    {
        await using var db = _factory.CreateContext();
        // "crestfall" is pre-seeded; verify lookup by Id
        var repo   = new ZoneRepository(db);
        var result = await repo.GetByIdAsync("crestfall");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Crestfall");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new ZoneRepository(db);
        var result = await repo.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByRegionIdAsync_Should_Return_Zones_In_Region()
    {
        await using var db = _factory.CreateContext();
        // "varenmark" has 6 seeded zones: crestfall, the-droveway, ashlen-wood, grevenmire, the-halrow, drowning-pits
        var repo   = new ZoneRepository(db);
        var result = await repo.GetByRegionIdAsync("varenmark");

        result.Should().HaveCount(6);
        result.Should().OnlyContain(z => z.RegionId == "varenmark");
        result.Should().Contain(z => z.Id == "crestfall");
        result.Should().Contain(z => z.Id == "drowning-pits");
    }

    [Fact]
    public async Task GetByRegionIdAsync_Should_Return_Empty_For_Unknown_Region()
    {
        await using var db = _factory.CreateContext();
        var repo   = new ZoneRepository(db);
        var result = await repo.GetByRegionIdAsync("nonexistent-region");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRegionIdAsync_Should_Return_Zones_Ordered_By_Level()
    {
        await using var db = _factory.CreateContext();
        // greymoor zones: aldenmere (L5), pale-moor (L7), soddenfen (L9), barrow-deeps (L11)
        var repo   = new ZoneRepository(db);
        var result = await repo.GetByRegionIdAsync("greymoor");

        result.Should().HaveCount(4);
        result[0].MinLevel.Should().BeLessThanOrEqualTo(result[1].MinLevel);
        result[1].MinLevel.Should().BeLessThanOrEqualTo(result[2].MinLevel);
        result[2].MinLevel.Should().BeLessThanOrEqualTo(result[3].MinLevel);
    }

    // PlayerSessionRepository
    [Fact]
    public async Task AddAsync_Should_Persist_Session()
    {
        await using var db = _factory.CreateContext();
        // Use a pre-seeded zone so FK constraints are satisfied
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        var session = new PlayerSession
        {
            CharacterId    = character.Id,
            CharacterName  = character.Name,
            ConnectionId   = "conn-1",
            RegionId       = "varenmark",
            ZoneId         = "crestfall",
        };

        var repo = new PlayerSessionRepository(db);
        await repo.AddAsync(session);

        var all = await db.PlayerSessions.ToListAsync();
        all.Should().ContainSingle(s => s.ConnectionId == "conn-1");
    }

    [Fact]
    public async Task GetByZoneIdAsync_Should_Return_Sessions_In_Zone()
    {
        await using var db = _factory.CreateContext();
        // Reuse pre-seeded zones (crestfall and aldenmere)
        var accountId = await SeedAccountAsync(db);
        var char1     = await SeedCharacterAsync(db, accountId, slotIndex: 1);
        var char2     = await SeedCharacterAsync(db, accountId, slotIndex: 2);

        db.PlayerSessions.AddRange(
            new PlayerSession { CharacterId = char1.Id, CharacterName = char1.Name, ConnectionId = "c1", RegionId = "varenmark", ZoneId = "crestfall" },
            new PlayerSession { CharacterId = char2.Id, CharacterName = char2.Name, ConnectionId = "c2", RegionId = "greymoor", ZoneId = "aldenmere" });
        await db.SaveChangesAsync();

        var repo   = new PlayerSessionRepository(db);
        var result = await repo.GetByZoneIdAsync("crestfall");

        result.Should().ContainSingle(s => s.ConnectionId == "c1");
    }

    [Fact]
    public async Task GetByConnectionIdAsync_Should_Return_Session_When_Found()
    {
        await using var db = _factory.CreateContext();
        // Use a pre-seeded zone so FK constraints are satisfied
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-abc",
            RegionId      = "varenmark",
            ZoneId        = "crestfall",
        });
        await db.SaveChangesAsync();

        var repo   = new PlayerSessionRepository(db);
        var result = await repo.GetByConnectionIdAsync("conn-abc");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByConnectionIdAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new PlayerSessionRepository(db);
        var result = await repo.GetByConnectionIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCharacterIdAsync_Should_Return_Session_When_Found()
    {
        await using var db = _factory.CreateContext();
        // Use a pre-seeded zone so FK constraints are satisfied
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-xyz",
            RegionId      = "varenmark",
            ZoneId        = "crestfall",
        });
        await db.SaveChangesAsync();

        var repo   = new PlayerSessionRepository(db);
        var result = await repo.GetByCharacterIdAsync(character.Id);

        result.Should().NotBeNull();
        result!.ConnectionId.Should().Be("conn-xyz");
    }

    [Fact]
    public async Task GetByCharacterIdAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new PlayerSessionRepository(db);
        var result = await repo.GetByCharacterIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Session()
    {
        await using var db = _factory.CreateContext();
        // Use a pre-seeded zone so FK constraints are satisfied
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        var session = new PlayerSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-del",
            RegionId      = "varenmark",
            ZoneId        = "crestfall",
        };
        db.PlayerSessions.Add(session);
        await db.SaveChangesAsync();

        var repo = new PlayerSessionRepository(db);
        await repo.RemoveAsync(session);

        var remaining = await db.PlayerSessions.ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveByConnectionIdAsync_Should_Delete_Session()
    {
        await using var db = _factory.CreateContext();
        // Use a pre-seeded zone so FK constraints are satisfied
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-rem",
            RegionId      = "varenmark",
            ZoneId        = "crestfall",
        });
        await db.SaveChangesAsync();

        var repo = new PlayerSessionRepository(db);
        await repo.RemoveByConnectionIdAsync("conn-rem");

        var remaining = await db.PlayerSessions.ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveByConnectionIdAsync_Should_Be_Noop_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo = new PlayerSessionRepository(db);
        var act  = () => repo.RemoveByConnectionIdAsync("nonexistent");
        await act.Should().NotThrowAsync();
    }
}
