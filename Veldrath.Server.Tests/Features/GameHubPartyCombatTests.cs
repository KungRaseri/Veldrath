using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Party;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Hubs;
using Veldrath.Server.Services;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the party and combat hub methods of <see cref="GameHub"/> — group
/// membership, the combat turn payloads, kill-credit fan-out and utility methods.
/// </summary>
public class GameHubPartyCombatTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    private static PartySnapshot Snapshot(Guid partyId, Guid leaderId, string leaderName = "Bran") =>
        new(partyId, leaderId, "contribution",
            [new PartyMemberView(leaderId, leaderName, 3, 30, 30, true)]);

    // ── Party: guards ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("create")]
    [InlineData("leave")]
    [InlineData("kick")]
    [InlineData("get")]
    [InlineData("invite")]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task Party_Methods_Require_A_Selected_Character(string method)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await (method switch
        {
            "create" => hub.CreateParty(),
            "leave" => hub.LeaveParty(),
            "kick" => hub.KickPartyMember(Guid.NewGuid()),
            "get" => hub.GetParty(),
            "invite" => hub.InviteToParty("Ghost"),
            "accept" => hub.AcceptPartyInvite(Guid.NewGuid()),
            _ => hub.DeclinePartyInvite(Guid.NewGuid()),
        });

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "Error");
        clients.Caller.SentMessages.Single().Args[0].Should()
            .Be($"SelectCharacter must be called before {method switch
            {
                "create" => "CreateParty",
                "leave" => "LeaveParty",
                "kick" => "KickPartyMember",
                "get" => "GetParty",
                "invite" => "InviteToParty",
                "accept" => "AcceptPartyInvite",
                _ => "DeclinePartyInvite",
            }}");
    }

    // ── CreateParty ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParty_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "Character is already in a party." });

        await hub.CreateParty();

        ShouldBeError(clients.Caller, "Character is already in a party.");
    }

    [Fact]
    public async Task CreateParty_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.CreateParty();

        ShouldBeError(clients.Caller, "Failed to create party");
    }

    [Fact]
    public async Task CreateParty_Joins_The_Party_Group_And_Sends_The_Snapshot()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        var partyId = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true, Party = Snapshot(partyId, characterId) });

        await hub.CreateParty();

        groups.AddedGroups.Should().BeEquivalentTo([$"party:{partyId}"]);
        clients.Caller.SentMessages.Single(m => m.Method == "PartyUpdated").Args[0]
            .Should().BeOfType<PartySnapshot>().Which.PartyId.Should().Be(partyId);
    }

    // ── LeaveParty ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveParty_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "You are not in a party." });

        await hub.LeaveParty();

        ShouldBeError(clients.Caller, "You are not in a party.");
    }

    [Fact]
    public async Task LeaveParty_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.LeaveParty();

        ShouldBeError(clients.Caller, "Failed to leave party");
    }

    [Fact]
    public async Task LeaveParty_Disbands_When_The_Last_Member_Leaves()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true, PartyId = partyId, PartyDisbanded = true });

        await hub.LeaveParty();

        groups.RemovedGroups.Should().BeEquivalentTo([$"party:{partyId}"]);
        clients.GroupNamed($"party:{partyId}").SentMessages.Should()
            .ContainSingle(m => m.Method == "PartyDisbanded");
        clients.Caller.SentMessages.Should().ContainSingle(m =>
            m.Method == "PartyUpdated" && m.Args[0] == null);
    }

    [Fact]
    public async Task LeaveParty_Broadcasts_The_Remaining_Party_Snapshot()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult
        {
            Success = true,
            PartyId = partyId,
            Party = Snapshot(partyId, Guid.NewGuid(), "Ally"),
        });

        await hub.LeaveParty();

        groups.RemovedGroups.Should().BeEquivalentTo([$"party:{partyId}"]);
        clients.GroupNamed($"party:{partyId}").SentMessages.Should()
            .ContainSingle(m => m.Method == "PartyUpdated");
        clients.Caller.SentMessages.Should().BeEmpty("the caller has already left the group");
    }

    // ── KickPartyMember ──────────────────────────────────────────────────────

    [Fact]
    public async Task KickPartyMember_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "Only the leader may remove members." });

        await hub.KickPartyMember(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Only the leader may remove members.");
    }

    [Fact]
    public async Task KickPartyMember_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.KickPartyMember(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Failed to remove party member");
    }

    [Fact]
    public async Task KickPartyMember_Broadcasts_The_New_Snapshot_To_The_Party()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true, Party = Snapshot(partyId, Guid.NewGuid()) });

        await hub.KickPartyMember(Guid.NewGuid());

        clients.GroupNamed($"party:{partyId}").SentMessages.Should()
            .ContainSingle(m => m.Method == "PartyUpdated");
    }

    // ── GetParty ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetParty_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "Character not found" });

        await hub.GetParty();

        ShouldBeError(clients.Caller, "Character not found");
    }

    [Fact]
    public async Task GetParty_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.GetParty();

        ShouldBeError(clients.Caller, "Failed to read party");
    }

    [Fact]
    public async Task GetParty_Is_Silent_About_Being_Solo_But_Joins_The_Group_When_Grouped()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var partyId = Guid.NewGuid();

        var solo = HubHarness.Create(db, account.Id);
        solo.Ctx.Items["CharacterId"] = Guid.NewGuid();
        solo.Mediator.Returns(new PartyCommandResult { Success = true, Party = null });
        await solo.Hub.GetParty();
        solo.Clients.Caller.SentMessages.Single(m => m.Method == "PartyUpdated").Args[0].Should().BeNull();
        solo.Groups.AddedGroups.Should().BeEmpty();

        var grouped = HubHarness.Create(db, account.Id);
        grouped.Ctx.Items["CharacterId"] = Guid.NewGuid();
        grouped.Mediator.Returns(new PartyCommandResult { Success = true, Party = Snapshot(partyId, Guid.NewGuid()) });
        await grouped.Hub.GetParty();
        grouped.Groups.AddedGroups.Should().BeEquivalentTo([$"party:{partyId}"]);
        grouped.Clients.Caller.SentMessages.Single(m => m.Method == "PartyUpdated").Args[0]
            .Should().BeOfType<PartySnapshot>();
    }

    // ── InviteToParty ────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteToParty_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "That character is not online." });

        await hub.InviteToParty("Ghost");

        ShouldBeError(clients.Caller, "That character is not online.");
    }

    [Fact]
    public async Task InviteToParty_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.InviteToParty("Ghost");

        ShouldBeError(clients.Caller, "Failed to invite that character");
    }

    [Fact]
    public async Task InviteToParty_Sends_The_Snapshot_And_Nudges_An_Online_Invitee()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-invitee");

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult
        {
            Success = true,
            Party = Snapshot(partyId, Guid.NewGuid()),
            InvitedCharacterId = target.Id,
        });

        await hub.InviteToParty(target.Name);

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "PartyUpdated");
        clients.Connection("conn-invitee").SentMessages.Should()
            .ContainSingle(m => m.Method == "PartyInvitesChanged");
    }

    [Fact]
    public async Task InviteToParty_Does_Not_Nudge_An_Offline_Invitee()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult
        {
            Success = true,
            Party = Snapshot(Guid.NewGuid(), Guid.NewGuid()),
            InvitedCharacterId = target.Id,
        });

        await hub.InviteToParty(target.Name);

        clients.Connection("conn-invitee").SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task InviteToParty_Without_An_Invited_Character_Sends_Nothing_Extra()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true, Party = Snapshot(Guid.NewGuid(), Guid.NewGuid()) });

        await hub.InviteToParty("Ghost");

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "PartyUpdated");
    }

    // ── Accept / Decline invite ──────────────────────────────────────────────

    [Fact]
    public async Task AcceptPartyInvite_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "Invite has expired." });

        await hub.AcceptPartyInvite(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Invite has expired.");
    }

    [Fact]
    public async Task AcceptPartyInvite_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.AcceptPartyInvite(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Failed to join the party");
    }

    [Fact]
    public async Task AcceptPartyInvite_Joins_The_Group_And_Broadcasts()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true, Party = Snapshot(partyId, Guid.NewGuid()) });

        await hub.AcceptPartyInvite(partyId);

        groups.AddedGroups.Should().BeEquivalentTo([$"party:{partyId}"]);
        clients.GroupNamed($"party:{partyId}").SentMessages.Should()
            .ContainSingle(m => m.Method == "PartyUpdated");
    }

    [Fact]
    public async Task DeclinePartyInvite_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = false, ErrorMessage = "Invite not found." });

        await hub.DeclinePartyInvite(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Invite not found.");
    }

    [Fact]
    public async Task DeclinePartyInvite_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<PartyCommandResult>(new InvalidOperationException("boom"));

        await hub.DeclinePartyInvite(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Failed to decline the invite");
    }

    [Fact]
    public async Task DeclinePartyInvite_Asks_The_Caller_To_Refresh_Its_Invites()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var partyId = Guid.NewGuid();
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PartyCommandResult { Success = true });

        await hub.DeclinePartyInvite(partyId);

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "PartyInvitesChanged");
    }

    // ── Combat: EngageEnemy ──────────────────────────────────────────────────

    [Fact]
    public async Task EngageEnemy_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.EngageEnemy(new EngageEnemyHubRequest("market", Guid.NewGuid()));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before EngageEnemy");
    }

    [Fact]
    public async Task EngageEnemy_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new EngageEnemyHubResult { Success = false, ErrorMessage = "That enemy is already dead." });

        await hub.EngageEnemy(new EngageEnemyHubRequest("market", Guid.NewGuid()));

        ShouldBeError(clients.Caller, "That enemy is already dead.");
    }

    [Fact]
    public async Task EngageEnemy_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<EngageEnemyHubResult>(new InvalidOperationException("boom"));

        await hub.EngageEnemy(new EngageEnemyHubRequest("market", Guid.NewGuid()));

        ShouldBeError(clients.Caller, "Failed to engage enemy");
    }

    [Fact]
    public async Task EngageEnemy_Sends_CombatStarted_To_The_Caller_And_EnemyEngaged_To_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        ctx.Items["CurrentLocationSlug"] = "market";
        var enemyId = Guid.NewGuid();
        mediator.Returns(new EngageEnemyHubResult
        {
            Success = true,
            EnemyId = enemyId,
            EnemyName = "Goblin",
            EnemyLevel = 2,
            EnemyCurrentHealth = 20,
            EnemyMaxHealth = 20,
            EnemyAbilityNames = ["Stab"],
        });

        await hub.EngageEnemy(new EngageEnemyHubRequest("ignored", enemyId));

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<EngageEnemyHubCommand>().Single();
        command.ZoneGroup.Should().Be("zone:crestfall");
        command.LocationSlug.Should().Be("market", "the stored location wins over the request fallback");

        clients.Caller.SentMessages.Single(m => m.Method == "CombatStarted").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, EnemyId = enemyId, EnemyName = "Goblin" });
        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, EnemyId = enemyId, EnemyName = "Goblin" });
    }

    [Fact]
    public async Task EngageEnemy_Outside_A_Zone_Does_Not_Broadcast_To_Peers()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new EngageEnemyHubResult { Success = true, EnemyName = "Goblin" });

        await hub.EngageEnemy(new EngageEnemyHubRequest("market", Guid.NewGuid()));

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "CombatStarted");
        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    // ── Combat: AttackEnemy ──────────────────────────────────────────────────

    [Fact]
    public async Task AttackEnemy_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.AttackEnemy();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before AttackEnemy");
    }

    [Fact]
    public async Task AttackEnemy_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new AttackEnemyHubResult { Success = false, ErrorMessage = "You are not in combat." });

        await hub.AttackEnemy();

        ShouldBeError(clients.Caller, "You are not in combat.");
    }

    [Fact]
    public async Task AttackEnemy_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<AttackEnemyHubResult>(new InvalidOperationException("boom"));

        await hub.AttackEnemy();

        ShouldBeError(clients.Caller, "Attack failed");
    }

    [Fact]
    public async Task AttackEnemy_Sends_The_Combat_Turn_Without_Kill_Fan_Out()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new AttackEnemyHubResult
        {
            Success = true,
            PlayerDamage = 7,
            EnemyRemainingHealth = 13,
            EnemyDamage = 2,
            PlayerRemainingHealth = 28,
        });

        await hub.AttackEnemy();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "CombatTurn").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            Action = "attack",
            PlayerDamage = 7,
            EnemyRemainingHealth = 13,
            EnemyDefeated = false,
            EnemyDamage = 2,
            PlayerRemainingHealth = 28,
        });
        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
        mediator.Invocations.Select(i => i.Arguments[0])
            .Should().NotContain(a => a is AdvanceQuestObjectivesHubCommand);
    }

    [Fact]
    public async Task AttackEnemy_On_A_Kill_Credits_Every_Participant_And_Tells_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var killer = await HubHarness.SeedCharacterAsync(db, account.Id);
        var allyAccount = await HubHarness.SeedAccountAsync(db);
        var ally = await HubHarness.SeedCharacterAsync(db, allyAccount.Id);
        await HubHarness.SeedSessionAsync(db, ally, "conn-ally");

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(killer.Id);
        mediator.Returns(new AttackEnemyHubResult
        {
            Success = true,
            EnemyDefeated = true,
            EnemyName = "Goblin Scout",
            EnemyArchetypeSlug = "goblin-scout",
            ParticipantIds = [killer.Id, ally.Id],
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 1 });

        await hub.AttackEnemy();

        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "EnemyDefeated");

        var advances = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>()
            .Select(c => (c.CharacterId, c.Key))
            .ToList();
        advances.Should().BeEquivalentTo([
            (killer.Id, "goblin-scout"),
            (killer.Id, "Goblin Scout"),
            (ally.Id, "goblin-scout"),
            (ally.Id, "Goblin Scout"),
        ]);

        clients.Connection("conn-ally").SentMessages.Should()
            .Contain(m => m.Method == "QuestLogReceived", "the ally's journal is pushed to their own connection");
    }

    [Fact]
    public async Task AttackEnemy_On_A_Shared_Kill_Ends_The_Other_Fighters_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var killer = await HubHarness.SeedCharacterAsync(db, account.Id);
        var defenderAccount = await HubHarness.SeedAccountAsync(db);
        var defender = await HubHarness.SeedCharacterAsync(db, defenderAccount.Id);
        await HubHarness.SeedSessionAsync(db, defender, "conn-defender");

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(killer.Id);
        mediator.Returns(new AttackEnemyHubResult
        {
            Success = true,
            EnemyDefeated = true,
            EnemyName = "Goblin",
            EnemyArchetypeSlug = "goblin",
            ParticipantIds = [killer.Id, defender.Id],
            ReleasedCombatCharacterIds = [killer.Id, defender.Id],
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 1 });

        await hub.AttackEnemy();

        // The kill removed the defender's session too, so the other connection is told its fight is over. Without
        // this it keeps a combat panel for a fight the server dropped and its next Defend is answered "Not in combat".
        clients.Connection("conn-defender").SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = defender.Id, Reason = "shared kill" });

        // The killer's own turn already closed their panel; a second, contradicting end event would overwrite it.
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "CombatEnded");
    }

    [Fact]
    public async Task AttackEnemy_On_Player_Defeat_Ends_The_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new AttackEnemyHubResult
        {
            Success = true,
            EnemyRemainingHealth = 12,
            PlayerRemainingHealth = 0,
            PlayerDefeated = true,
        });

        await hub.AttackEnemy();

        // The turn carries the death; the end event is what closes the shell's combat panel. Order matters to the
        // client, which applies the death penalty from the turn and only then drops the panel.
        var methods = clients.Caller.SentMessages.Select(m => m.Method).ToList();
        methods.Should().ContainInOrder("CombatTurn", "CombatEnded");
        clients.Caller.SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, Reason = "defeated" });
    }

    [Fact]
    public async Task AttackEnemy_On_A_Survived_Turn_Does_Not_End_The_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new AttackEnemyHubResult
        {
            Success = true,
            EnemyRemainingHealth = 12,
            PlayerRemainingHealth = 18,
        });

        await hub.AttackEnemy();

        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "CombatEnded");
    }

    // ── Combat: DefendAction ─────────────────────────────────────────────────

    [Fact]
    public async Task DefendAction_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.DefendAction();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before DefendAction");
    }

    [Fact]
    public async Task DefendAction_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new DefendActionHubResult { Success = false, ErrorMessage = "You are not in combat." });

        await hub.DefendAction();

        ShouldBeError(clients.Caller, "You are not in combat.");
    }

    [Fact]
    public async Task DefendAction_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<DefendActionHubResult>(new InvalidOperationException("boom"));

        await hub.DefendAction();

        ShouldBeError(clients.Caller, "Defend failed");
    }

    [Fact]
    public async Task DefendAction_Sends_A_Defend_Combat_Turn()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new DefendActionHubResult
        {
            Success = true,
            EnemyDamage = 1,
            EnemyAbilityUsed = "Claw",
            PlayerRemainingHealth = 29,
        });

        await hub.DefendAction();

        clients.Caller.SentMessages.Single(m => m.Method == "CombatTurn").Args[0]
            .Should().BeEquivalentTo(new
            {
                Action = "defend",
                EnemyDamage = 1,
                EnemyAbilityUsed = "Claw",
                PlayerRemainingHealth = 29,
                PlayerDefeated = false,
            });
    }

    [Fact]
    public async Task DefendAction_On_Player_Defeat_Ends_The_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new DefendActionHubResult
        {
            Success = true,
            EnemyDamage = 9,
            PlayerRemainingHealth = 0,
            PlayerDefeated = true,
        });

        await hub.DefendAction();

        var methods = clients.Caller.SentMessages.Select(m => m.Method).ToList();
        methods.Should().ContainInOrder("CombatTurn", "CombatEnded");
        clients.Caller.SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, Reason = "defeated" });
    }

    // ── Combat: FleeFromCombat ───────────────────────────────────────────────

    [Fact]
    public async Task FleeFromCombat_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.FleeFromCombat();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before FleeFromCombat");
    }

    [Fact]
    public async Task FleeFromCombat_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new FleeFromCombatHubResult { Success = false, ErrorMessage = "You are not in combat." });

        await hub.FleeFromCombat();

        ShouldBeError(clients.Caller, "You are not in combat.");
    }

    [Fact]
    public async Task FleeFromCombat_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<FleeFromCombatHubResult>(new InvalidOperationException("boom"));

        await hub.FleeFromCombat();

        ShouldBeError(clients.Caller, "Flee failed");
    }

    [Fact]
    public async Task FleeFromCombat_Reports_The_Escape()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new FleeFromCombatHubResult { Success = true, Fled = true });

        await hub.FleeFromCombat();

        clients.Caller.SentMessages.Single().Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, Reason = "fled" });
        clients.Caller.SentMessages.Single().Method.Should().Be("CombatEnded");
    }

    [Fact]
    public async Task FleeFromCombat_On_Failure_Reports_The_Counter_Attack()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new FleeFromCombatHubResult
        {
            Success = true,
            Fled = false,
            EnemyDamage = 4,
            EnemyAbilityUsed = "Bite",
            PlayerRemainingHealth = 26,
        });

        await hub.FleeFromCombat();

        clients.Caller.SentMessages.Single().Method.Should().Be("CombatTurn");
        clients.Caller.SentMessages.Single().Args[0].Should().BeEquivalentTo(new
        {
            Action = "flee_failed",
            EnemyDamage = 4,
            EnemyAbilityUsed = "Bite",
            PlayerRemainingHealth = 26,
        });
    }

    [Fact]
    public async Task FleeFromCombat_On_Player_Defeat_Ends_The_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new FleeFromCombatHubResult
        {
            Success = true,
            Fled = false,
            EnemyDamage = 9,
            PlayerRemainingHealth = 0,
            PlayerDefeated = true,
        });

        await hub.FleeFromCombat();

        // The failed escape did not open a door, but the counter-attack ended the fight: the panel must close.
        var methods = clients.Caller.SentMessages.Select(m => m.Method).ToList();
        methods.Should().ContainInOrder("CombatTurn", "CombatEnded");
        clients.Caller.SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, Reason = "defeated" });
    }

    // ── Combat: UseAbilityInCombat ───────────────────────────────────────────

    [Fact]
    public async Task UseAbilityInCombat_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before UseAbilityInCombat");
    }

    [Fact]
    public async Task UseAbilityInCombat_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Not enough mana." });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        ShouldBeError(clients.Caller, "Not enough mana.");
    }

    [Fact]
    public async Task UseAbilityInCombat_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<UseAbilityInCombatHubResult>(new InvalidOperationException("boom"));

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        ShouldBeError(clients.Caller, "Ability use failed");
    }

    [Fact]
    public async Task UseAbilityInCombat_Sends_The_Ability_Combat_Turn()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new UseAbilityInCombatHubResult
        {
            Success = true,
            AbilityId = "fireball",
            AbilityDamage = 12,
            ManaCost = 8,
            PlayerRemainingMana = 12,
            EnemyRemainingHealth = 3,
        });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "CombatTurn").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            Action = "ability",
            AbilityId = "fireball",
            AbilityDamage = 12,
            ManaCost = 8,
            PlayerRemainingMana = 12,
            EnemyRemainingHealth = 3,
            EnemyDefeated = false,
        });
        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task UseAbilityInCombat_On_A_Kill_Advances_The_Kill_Objective_By_Slug_And_Display_Name()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new UseAbilityInCombatHubResult
        {
            Success = true,
            AbilityId = "fireball",
            EnemyDefeated = true,
            EnemyName = "Goblin Scout",
            EnemyArchetypeSlug = "goblin-scout",
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "EnemyDefeated");

        // Same contract as a melee kill: the slug is what content authors, the display name keeps a legacy
        // journal progressing, and neither key is advanced twice.
        var advances = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>()
            .Select(c => (c.CharacterId, c.Kind, c.Key))
            .ToList();
        advances.Should().BeEquivalentTo([
            (characterId, "kill", "goblin-scout"),
            (characterId, "kill", "Goblin Scout"),
        ]);
    }

    [Fact]
    public async Task UseAbilityInCombat_On_A_Kill_Advances_A_Case_Colliding_Archetype_Once()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);

        // Cinderling's display name and slug differ only by case, and the journal matches objective keys
        // OrdinalIgnoreCase, so advancing both would credit the same objective twice for one kill.
        mediator.Returns(new UseAbilityInCombatHubResult
        {
            Success = true,
            AbilityId = "fireball",
            EnemyDefeated = true,
            EnemyName = "Cinderling",
            EnemyArchetypeSlug = "cinderling",
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "EnemyDefeated");

        var advances = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>()
            .Select(c => (c.CharacterId, c.Kind, c.Key))
            .ToList();
        advances.Should().BeEquivalentTo([(characterId, "kill", "cinderling")]);
    }

    [Fact]
    public async Task UseAbilityInCombat_On_A_Shared_Kill_Ends_The_Other_Fighters_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var caster = await HubHarness.SeedCharacterAsync(db, account.Id);
        var defenderAccount = await HubHarness.SeedAccountAsync(db);
        var defender = await HubHarness.SeedCharacterAsync(db, defenderAccount.Id);
        await HubHarness.SeedSessionAsync(db, defender, "conn-defender");

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(caster.Id);
        mediator.Returns(new UseAbilityInCombatHubResult
        {
            Success = true,
            AbilityId = "fireball",
            EnemyDefeated = true,
            EnemyName = "Goblin",
            ReleasedCombatCharacterIds = [caster.Id, defender.Id],
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        // An ability kill is a kill: it releases every session pointed at the enemy, and the other fighter has to be
        // told rather than left with a panel the server no longer backs.
        clients.Connection("conn-defender").SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = defender.Id, Reason = "shared kill" });
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "CombatEnded");
    }

    [Fact]
    public async Task UseAbilityInCombat_On_Player_Defeat_Ends_The_Fight()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new UseAbilityInCombatHubResult
        {
            Success = true,
            AbilityId = "fireball",
            EnemyRemainingHealth = 4,
            PlayerRemainingHealth = 0,
            PlayerDefeated = true,
        });

        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        var methods = clients.Caller.SentMessages.Select(m => m.Method).ToList();
        methods.Should().ContainInOrder("CombatTurn", "CombatEnded");
        clients.Caller.SentMessages.Single(m => m.Method == "CombatEnded").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, Reason = "defeated" });
    }

    // ── Combat: Respawn ──────────────────────────────────────────────────────

    [Fact]
    public async Task Respawn_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.Respawn();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before Respawn");
    }

    [Fact]
    public async Task Respawn_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new RespawnHubResult { Success = false, ErrorMessage = "You are not dead." });

        await hub.Respawn();

        ShouldBeError(clients.Caller, "You are not dead.");
    }

    [Fact]
    public async Task Respawn_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<RespawnHubResult>(new InvalidOperationException("boom"));

        await hub.Respawn();

        ShouldBeError(clients.Caller, "Respawn failed");
    }

    [Fact]
    public async Task Respawn_Sends_The_Restored_State_And_The_Degraded_Notice()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new RespawnHubResult
        {
            Success = true,
            CurrentHealth = 10,
            CurrentMana = 5,
            LocationSlug = "crestfall-gate",
            LocationDisplayName = "Crestfall Gate",
            UsedFallbackRespawn = true,
            Warning = "Your last rest point no longer exists; you woke at the gate.",
        });

        await hub.Respawn();

        clients.Caller.SentMessages.Single(m => m.Method == "CharacterRespawned").Args[0]
            .Should().BeEquivalentTo(new
            {
                CharacterId = characterId,
                CurrentHealth = 10,
                CurrentMana = 5,
                LocationSlug = "crestfall-gate",
                LocationDisplayName = "Crestfall Gate",
            });
        clients.Caller.SentMessages.Should().Contain(m =>
            m.Method == "SystemMessage"
            && (string)m.Args[0]! == "Your last rest point no longer exists; you woke at the gate.");
    }

    [Fact]
    public async Task Respawn_Without_A_Warning_Sends_Only_The_State()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new RespawnHubResult { Success = true, CurrentHealth = 20, CurrentMana = 10 });

        await hub.Respawn();

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "CharacterRespawned");
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ping_Returns_The_Current_Unix_Millisecond_Timestamp()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, _, _, _, _, _) = HubHarness.Create(db, account.Id);
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var timestamp = await hub.Ping();

        timestamp.Should().BeInRange(before, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task GetActiveCharacters_Returns_Only_Claimed_Characters()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var tracker = new ActiveCharacterTracker();
        var claimed = Guid.NewGuid();
        tracker.TryClaim(claimed, "conn-a").Should().BeTrue();
        var (hub, _, _, _, _, _) = HubHarness.Create(db, account.Id, tracker: tracker);

        var active = await hub.GetActiveCharacters();

        active.Should().BeEquivalentTo([claimed]);
    }
}
