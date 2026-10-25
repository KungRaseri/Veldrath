using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Party;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the C1 party commands: create, leave, kick and read, against the real persistence layer.
/// </summary>
public class PartyCommandTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string? name = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = name ?? $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = """{"CurrentHealth":42,"MaxHealth":100}""",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static CreatePartyHubCommandHandler CreateHandler(ApplicationDbContext db) =>
        new(new PartyStore(db), NullLogger<CreatePartyHubCommandHandler>.Instance);

    private static LeavePartyHubCommandHandler LeaveHandler(ApplicationDbContext db) =>
        new(new PartyStore(db), NullLogger<LeavePartyHubCommandHandler>.Instance);

    private static KickPartyMemberHubCommandHandler KickHandler(ApplicationDbContext db) =>
        new(new PartyStore(db), NullLogger<KickPartyMemberHubCommandHandler>.Instance);

    private static GetPartyHubQueryHandler GetHandler(ApplicationDbContext db) =>
        new(new PartyStore(db));

    // ── Create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParty_Makes_The_Creator_The_Sole_Leader()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await CreateHandler(db).Handle(new CreatePartyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Party.Should().NotBeNull();
        result.Party!.LeaderCharacterId.Should().Be(character.Id);
        result.Party.LootRule.Should().Be(Party.LootRuleContribution);
        result.Party.Members.Should().ContainSingle();
        result.Party.Members[0].IsLeader.Should().BeTrue();
        result.Party.Members[0].CurrentHealth.Should().Be(42, "vitals come from the character's attribute blob");
        result.Party.Members[0].MaxHealth.Should().Be(100);
    }

    [Fact]
    public async Task CreateParty_Refuses_A_Character_Who_Is_Already_Grouped()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        await CreateHandler(db).Handle(new CreatePartyHubCommand(character.Id), CancellationToken.None);

        var second = await CreateHandler(db).Handle(new CreatePartyHubCommand(character.Id), CancellationToken.None);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("already in a party");
        db.Parties.Count().Should().Be(1, "the refusal must not create a second party");
    }

    // ── Leave ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveParty_Disbands_The_Party_When_The_Last_Member_Leaves()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var created = await CreateHandler(db).Handle(new CreatePartyHubCommand(character.Id), CancellationToken.None);

        var result = await LeaveHandler(db).Handle(new LeavePartyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PartyDisbanded.Should().BeTrue();
        result.PartyId.Should().Be(created.Party!.PartyId, "the hub needs the id to leave the SignalR group");
        db.Parties.Count().Should().Be(0);
        db.PartyMembers.Count().Should().Be(0);
    }

    [Fact]
    public async Task LeaveParty_Passes_Leadership_To_The_Longest_Standing_Member()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        var veteran = await SeedCharacterAsync(db);
        var rookie = await SeedCharacterAsync(db);

        var created = await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);
        var partyId = created.Party!.PartyId;

        db.PartyMembers.Add(new PartyMemberEntity
        {
            PartyId = partyId,
            CharacterId = veteran.Id,
            JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        });
        db.PartyMembers.Add(new PartyMemberEntity
        {
            PartyId = partyId,
            CharacterId = rookie.Id,
            JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var result = await LeaveHandler(db).Handle(new LeavePartyHubCommand(leader.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Party.Should().NotBeNull();
        result.Party!.LeaderCharacterId.Should().Be(veteran.Id, "promotion goes to the member who joined first");
        result.Party.Members.Should().HaveCount(2);
        result.Party.Members.Single(member => member.IsLeader).CharacterId.Should().Be(veteran.Id);
    }

    [Fact]
    public async Task LeaveParty_Refuses_When_The_Character_Is_Solo()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await LeaveHandler(db).Handle(new LeavePartyHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not in a party");
    }

    // ── Kick ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task KickPartyMember_Removes_The_Target_And_Returns_A_Fresh_Snapshot()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        var member = await SeedCharacterAsync(db);

        var created = await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);
        db.PartyMembers.Add(new PartyMemberEntity { PartyId = created.Party!.PartyId, CharacterId = member.Id });
        await db.SaveChangesAsync();

        var result = await KickHandler(db).Handle(
            new KickPartyMemberHubCommand(leader.Id, member.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Party!.Members.Should().ContainSingle();
        result.Party.Members[0].CharacterId.Should().Be(leader.Id);
    }

    [Fact]
    public async Task KickPartyMember_Refuses_When_The_Caller_Is_Not_The_Leader()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        var member = await SeedCharacterAsync(db);
        var outsider = await SeedCharacterAsync(db);

        var created = await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);
        db.PartyMembers.Add(new PartyMemberEntity { PartyId = created.Party!.PartyId, CharacterId = member.Id });
        await db.SaveChangesAsync();

        var result = await KickHandler(db).Handle(
            new KickPartyMemberHubCommand(member.Id, outsider.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("leader");
    }

    [Fact]
    public async Task KickPartyMember_Refuses_A_Leader_Removing_Themselves()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);

        var result = await KickHandler(db).Handle(
            new KickPartyMemberHubCommand(leader.Id, leader.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("leave the party instead");
    }

    // ── Read ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetParty_Reports_A_Solo_Character_As_A_Successful_Empty_Answer()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await GetHandler(db).Handle(new GetPartyHubQuery(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue("being solo is a normal state, not an error");
        result.Party.Should().BeNull();
    }

    [Fact]
    public async Task GetParty_Returns_The_Stored_Party_From_A_Fresh_Context()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        await CreateHandler(db).Handle(new CreatePartyHubCommand(character.Id), CancellationToken.None);

        // A new context is the restart case: the party must come back from the database, not from memory.
        await using var restarted = _dbFactory.CreateContext();
        var result = await GetHandler(restarted).Handle(new GetPartyHubQuery(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Party.Should().NotBeNull();
        result.Party!.LeaderCharacterId.Should().Be(character.Id);
    }

    // ── Combat participation (C3) ──────────────────────────────────────────────

    [Fact]
    public async Task GetOnlinePartyMembersAtLocation_Is_Empty_For_A_Solo_Character()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var peers = await new PartyStore(db).GetOnlinePartyMembersAtLocationAsync(character.Id);

        peers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOnlinePartyMembersAtLocation_Ignores_Members_Who_Are_Not_Online_Here()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);
        await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);

        var store = new PartyStore(db);
        var partyId = await db.Parties.Select(party => party.Id).SingleAsync();
        await store.AddMemberAsync(partyId, ally.Id);

        // Both stand in the same place, but neither has a PlayerSession, so neither is actually there.
        leader.CurrentZoneId = "zone-1";
        leader.CurrentZoneLocationSlug = "forest";
        ally.CurrentZoneId = "zone-1";
        ally.CurrentZoneLocationSlug = "forest";
        await db.SaveChangesAsync();

        var peers = await store.GetOnlinePartyMembersAtLocationAsync(leader.Id);

        peers.Should().BeEmpty("an offline member is not standing in the room");
    }

    [Fact]
    public async Task GetOnlinePartyMembersAtLocation_Returns_Only_Members_Online_At_That_Location()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);
        var elsewhere = await SeedCharacterAsync(db);

        await CreateHandler(db).Handle(new CreatePartyHubCommand(leader.Id), CancellationToken.None);
        var store = new PartyStore(db);
        var partyId = await db.Parties.Select(party => party.Id).SingleAsync();
        await store.AddMemberAsync(partyId, ally.Id);
        await store.AddMemberAsync(partyId, elsewhere.Id);

        // Positions live on the Characters row, which the travel and navigation paths keep current. ExecuteUpdate
        // writes them straight to the database, so the store's own AsNoTracking reads see the truth.
        await db.Characters
            .Where(character => character.Id == leader.Id || character.Id == ally.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(character => character.CurrentZoneId, "zone-1")
                .SetProperty(character => character.CurrentZoneLocationSlug, "forest"));
        await db.Characters
            .Where(character => character.Id == elsewhere.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(character => character.CurrentZoneId, "zone-1")
                .SetProperty(character => character.CurrentZoneLocationSlug, "market"));

        // Online is a live PlayerSession row; all three are online, but only two are at the same location.
        db.Worlds.Add(new World { Id = "world-1", Name = "Test world" });
        db.Regions.Add(new Region { Id = "region-1", Name = "Test region", WorldId = "world-1" });
        db.PlayerSessions.AddRange(
            new PlayerSession
            {
                CharacterId = leader.Id,
                CharacterName = leader.Name,
                ConnectionId = "conn-leader",
                RegionId = "region-1",
            },
            new PlayerSession
            {
                CharacterId = ally.Id,
                CharacterName = ally.Name,
                ConnectionId = "conn-ally",
                RegionId = "region-1",
            },
            new PlayerSession
            {
                CharacterId = elsewhere.Id,
                CharacterName = elsewhere.Name,
                ConnectionId = "conn-elsewhere",
                RegionId = "region-1",
            });
        await db.SaveChangesAsync();

        var peers = await store.GetOnlinePartyMembersAtLocationAsync(leader.Id);

        peers.Should().Equal([ally.Id], "only the member online at the same location joins the fight");
    }
}
