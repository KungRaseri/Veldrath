using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Party;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the C2 invite flow: who may be invited, what is refused and why, and what accepting does.
/// </summary>
public class PartyInviteTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakePlayerSessionRepository _sessions = new();

    public void Dispose() => _dbFactory.Dispose();

    private static PartyOptions PartyOptionsFor(int maxSize = 5) => new() { MaxSize = maxSize };

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string name)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = name,
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = """{"CurrentHealth":50,"MaxHealth":100}""",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private InviteToPartyHubCommandHandler InviteHandler(
        ApplicationDbContext db,
        FakeCombatSessionStore? combat = null,
        PartyOptions? options = null) =>
        new(
            new PartyStore(db),
            new CharacterRepository(db),
            _sessions,
            combat ?? new FakeCombatSessionStore(),
            Options.Create(options ?? PartyOptionsFor()),
            NullLogger<InviteToPartyHubCommandHandler>.Instance);

    private static AcceptPartyInviteHubCommandHandler AcceptHandler(
        ApplicationDbContext db,
        PartyOptions? options = null) =>
        new(
            new PartyStore(db),
            Options.Create(options ?? PartyOptionsFor()),
            NullLogger<AcceptPartyInviteHubCommandHandler>.Instance);

    private static async Task<Guid> CreatePartyAsync(ApplicationDbContext db, Guid leaderId)
    {
        var created = await new CreatePartyHubCommandHandler(
                new PartyStore(db),
                NullLogger<CreatePartyHubCommandHandler>.Instance)
            .Handle(new CreatePartyHubCommand(leaderId), CancellationToken.None);

        return created.Party!.PartyId;
    }

    // ── Refusals ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invite_Refuses_An_Unknown_Name()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        _sessions.MarkOnline(leader.Id);
        await CreatePartyAsync(db, leader.Id);

        var result = await InviteHandler(db).Handle(
            new InviteToPartyHubCommand(leader.Id, "Nobody"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No character named");
    }

    [Fact]
    public async Task Invite_Refuses_An_Offline_Character()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var offline = await SeedCharacterAsync(db, "Offline");
        _sessions.MarkOnline(leader.Id);
        await CreatePartyAsync(db, leader.Id);

        var result = await InviteHandler(db).Handle(
            new InviteToPartyHubCommand(leader.Id, offline.Name),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is not online");
    }

    [Fact]
    public async Task Invite_Refuses_Yourself()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        _sessions.MarkOnline(leader.Id);
        await CreatePartyAsync(db, leader.Id);

        var result = await InviteHandler(db).Handle(
            new InviteToPartyHubCommand(leader.Id, leader.Name),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot invite yourself");
    }

    [Fact]
    public async Task Invite_Refuses_Somebody_Already_Grouped()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var other = await SeedCharacterAsync(db, "Grouped");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(other.Id);
        await CreatePartyAsync(db, leader.Id);
        await CreatePartyAsync(db, other.Id);

        var result = await InviteHandler(db).Handle(
            new InviteToPartyHubCommand(leader.Id, other.Name),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already in a party");
    }

    [Fact]
    public async Task Invite_Refuses_Somebody_In_Combat()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var fighter = await SeedCharacterAsync(db, "Fighter");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(fighter.Id);
        await CreatePartyAsync(db, leader.Id);

        var combat = new FakeCombatSessionStore();
        combat.Set(fighter.Id, new ActiveCombatSession("zone:test", "square", Guid.NewGuid(), false, 1, DateTimeOffset.UtcNow));

        var result = await InviteHandler(db, combat).Handle(
            new InviteToPartyHubCommand(leader.Id, fighter.Name),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("in combat");
    }

    [Fact]
    public async Task Invite_Refuses_When_The_Party_Is_Full()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(friend.Id);
        await CreatePartyAsync(db, leader.Id);

        var result = await InviteHandler(db, options: PartyOptionsFor(maxSize: 1)).Handle(
            new InviteToPartyHubCommand(leader.Id, friend.Name),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("party is full");
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invite_Creates_A_Pending_Invite_The_Invitee_Can_See()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(friend.Id);
        var partyId = await CreatePartyAsync(db, leader.Id);

        var invite = await InviteHandler(db).Handle(
            new InviteToPartyHubCommand(leader.Id, friend.Name),
            CancellationToken.None);

        invite.Success.Should().BeTrue();
        invite.InvitedCharacterId.Should().Be(friend.Id, "the hub needs it to nudge the invitee's connection");

        var pending = await new GetPartyHubQueryHandler(new PartyStore(db))
            .Handle(new GetPartyHubQuery(friend.Id), CancellationToken.None);

        pending.PendingInvites.Should().ContainSingle();
        pending.PendingInvites[0].PartyId.Should().Be(partyId);
        pending.PendingInvites[0].PartyLeaderName.Should().Be("Lead");
        pending.PendingInvites[0].InvitedByName.Should().Be("Lead");
    }

    [Fact]
    public async Task Accept_Adds_The_Member_And_Clears_The_Invite()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(friend.Id);
        var partyId = await CreatePartyAsync(db, leader.Id);
        await InviteHandler(db).Handle(new InviteToPartyHubCommand(leader.Id, friend.Name), CancellationToken.None);

        var result = await AcceptHandler(db).Handle(
            new AcceptPartyInviteHubCommand(friend.Id, partyId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PartyId.Should().Be(partyId);
        result.Party!.Members.Should().HaveCount(2);
        result.Party.Members.Should().Contain(member => member.CharacterId == friend.Id);

        var pending = await new GetPartyHubQueryHandler(new PartyStore(db))
            .Handle(new GetPartyHubQuery(friend.Id), CancellationToken.None);
        pending.PendingInvites.Should().BeEmpty("accepting consumes the invite");
    }

    [Fact]
    public async Task Accept_Refuses_An_Expired_Invite()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(friend.Id);
        var partyId = await CreatePartyAsync(db, leader.Id);

        db.PartyInvites.Add(new PartyInvite
        {
            PartyId = partyId,
            InvitedCharacterId = friend.Id,
            InvitedByCharacterId = leader.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var result = await AcceptHandler(db).Handle(
            new AcceptPartyInviteHubCommand(friend.Id, partyId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("expired");
        db.PartyMembers.Count().Should().Be(1, "an expired invite must not add the member");
    }

    [Fact]
    public async Task Accept_Refuses_When_The_Party_Has_Filled_Up()
    {
        await using var db = _dbFactory.CreateContext();
        var leader = await SeedCharacterAsync(db, "Lead");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(leader.Id);
        _sessions.MarkOnline(friend.Id);
        var partyId = await CreatePartyAsync(db, leader.Id);
        await InviteHandler(db).Handle(new InviteToPartyHubCommand(leader.Id, friend.Name), CancellationToken.None);

        var result = await AcceptHandler(db, PartyOptionsFor(maxSize: 1)).Handle(
            new AcceptPartyInviteHubCommand(friend.Id, partyId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("party is full");
    }

    [Fact]
    public async Task Decline_Removes_Only_That_Partys_Invite()
    {
        await using var db = _dbFactory.CreateContext();
        var firstLeader = await SeedCharacterAsync(db, "First");
        var secondLeader = await SeedCharacterAsync(db, "Second");
        var friend = await SeedCharacterAsync(db, "Friend");
        _sessions.MarkOnline(firstLeader.Id);
        _sessions.MarkOnline(secondLeader.Id);
        _sessions.MarkOnline(friend.Id);

        var firstParty = await CreatePartyAsync(db, firstLeader.Id);
        var secondParty = await CreatePartyAsync(db, secondLeader.Id);
        await InviteHandler(db).Handle(new InviteToPartyHubCommand(firstLeader.Id, friend.Name), CancellationToken.None);
        await InviteHandler(db).Handle(new InviteToPartyHubCommand(secondLeader.Id, friend.Name), CancellationToken.None);

        var result = await new DeclinePartyInviteHubCommandHandler(new PartyStore(db))
            .Handle(new DeclinePartyInviteHubCommand(friend.Id, firstParty), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PendingInvites.Should().ContainSingle();
        result.PendingInvites[0].PartyId.Should().Be(secondParty, "declining one invite must not clear the other");
    }
}
