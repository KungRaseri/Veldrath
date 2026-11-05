using System.Text.Json;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the moderation and world-admin slash commands routed through
/// <see cref="GameHub.SendChatMessage"/>. Every command is driven with a caller that holds
/// (or lacks) the required permission, so the gate and the effect are both asserted.
/// </summary>
public class GameHubAdminCommandTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static PlayerAccount Account(string? reason = null, int warnCount = 0) => new()
    {
        UserName = "TargetAccount",
        NormalizedUserName = "TARGETACCOUNT",
        WarnCount = warnCount,
        BanReason = reason,
    };

    private static string SystemText(RecordingHubClients clients) =>
        clients.Caller.SentMessages.Last(m => m.Method == "SystemMessage").Args[0]
            .Should().BeOfType<string>().Subject;

    /// <summary>
    /// User manager whose caller-account lookup resolves to a clean moderator while every other
    /// lookup resolves to the managed (possibly muted/banned) target account.
    /// </summary>
    private static Mock<Microsoft.AspNetCore.Identity.UserManager<PlayerAccount>> CallerIsNotManaged(
        Guid callerAccountId,
        PlayerAccount managed)
    {
        var manager = HubHarness.NewUserManager(managed);
        manager.Setup(m => m.FindByIdAsync(callerAccountId.ToString()))
               .ReturnsAsync(new PlayerAccount { UserName = "Moderator", NormalizedUserName = "MODERATOR" });
        return manager;
    }

    // ── /announce ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Announce_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/announce server down soon"));

        clients.All.SentMessages.Should().BeEmpty();
        SystemText(clients).Should().Be("You do not have permission to send announcements.");
    }

    [Fact]
    public async Task Announce_Without_Text_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.SendAnnouncements);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/announce"));

        SystemText(clients).Should().Be("Usage: /announce <message>");
    }

    [Fact]
    public async Task Announce_Broadcasts_To_Everyone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.SendAnnouncements);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/announce server restart in 5"));

        var payload = clients.All.SentMessages.Single(m => m.Method == "OnAnnouncement").Args[0]
            .Should().BeOfType<AnnouncementPayload>().Subject;
        payload.Message.Should().Be("server restart in 5");
        payload.Severity.Should().Be("info");
    }

    // ── /kick ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kick_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/kick {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("You do not have permission to kick players.");
    }

    [Theory]
    [InlineData("/kick")]
    [InlineData("/kick not-a-guid")]
    public async Task Kick_With_An_Unusable_Character_Id_Prints_Usage(string command)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.KickPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest(command));

        SystemText(clients).Should().Be("Usage: /kick <characterId>");
    }

    [Fact]
    public async Task Kick_Of_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.KickPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/kick {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Character not found or not online.");
    }

    [Fact]
    public async Task Kick_Tells_The_Target_They_Were_Kicked_And_Confirms_To_The_Moderator()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.KickPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/kick {target.Id}"));

        clients.Connection("conn-target").SentMessages.Single().Args[0]
            .Should().BeOfType<KickedPayload>().Which.Reason.Should().Be("You have been kicked by a moderator.");
        SystemText(clients).Should().Be($"Kick sent to character {target.Id}.");
    }

    // ── /warn ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Warn_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/warn {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("You do not have permission to warn players.");
    }

    [Fact]
    public async Task Warn_With_An_Unusable_Character_Id_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.WarnPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/warn nope"));

        SystemText(clients).Should().Be("Usage: /warn <characterId> [reason]");
    }

    [Fact]
    public async Task Warn_Of_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.WarnPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/warn {Guid.NewGuid()} spam"));

        SystemText(clients).Should().Be("Character not found or not online.");
    }

    [Fact]
    public async Task Warn_Increments_The_Count_And_Delivers_The_Reason()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            moderation: new ModerationOptions { AutoBanWarnThreshold = 3 },
            permissions: Permissions.WarnPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/warn {target.Id} spoiling the plot"));

        managed.WarnCount.Should().Be(1);
        managed.IsBanned.Should().BeFalse();
        var warned = clients.Connection("conn-target").SentMessages
            .Single(m => m.Method == "OnWarned").Args[0]
            .Should().BeOfType<WarnedPayload>().Subject;
        warned.Reason.Should().Be("spoiling the plot");
        warned.NewWarnCount.Should().Be(1);
        SystemText(clients).Should().Be($"Warning 1/3 issued to {target.Name}.");
    }

    [Fact]
    public async Task Warn_At_The_Threshold_Also_Auto_Bans_And_Kicks()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account(warnCount: 2);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            moderation: new ModerationOptions { AutoBanWarnThreshold = 3 },
            permissions: Permissions.WarnPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/warn {target.Id}"));

        managed.WarnCount.Should().Be(3);
        managed.IsBanned.Should().BeTrue();
        managed.BannedUntil.Should().BeNull("the auto-ban is permanent");
        managed.BanReason.Should().Be("Auto-banned after 3 warnings.");
        clients.Connection("conn-target").SentMessages.Should().Contain(m => m.Method == "OnKicked");
        SystemText(clients).Should().Be($"Warning 3/3 issued to {target.Name}. Player auto-banned.");
    }

    // ── /mute ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Mute_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/mute {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("You do not have permission to mute players.");
    }

    [Fact]
    public async Task Mute_With_An_Unusable_Character_Id_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.MutePlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/mute nope"));

        SystemText(clients).Should().Be("Usage: /mute <characterId> [minutes] [reason]");
    }

    [Fact]
    public async Task Mute_Of_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.MutePlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/mute {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Character not found or not online.");
    }

    [Fact]
    public async Task Mute_Without_A_Duration_Is_Permanent()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            permissions: Permissions.MutePlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/mute {target.Id} stop spamming"));

        managed.IsMuted.Should().BeTrue();
        managed.MuteReason.Should().Be("stop spamming");
        managed.MutedUntil.Should().BeNull();
        var muted = clients.Connection("conn-target").SentMessages
            .Single(m => m.Method == "OnMuted").Args[0]
            .Should().BeOfType<MutedPayload>().Subject;
        muted.Reason.Should().Be("stop spamming");
        muted.Until.Should().BeNull();
        SystemText(clients).Should().Be($"{target.Name} has been muted. (permanent)");
    }

    [Fact]
    public async Task Mute_With_A_Duration_Sets_An_Expiry()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            permissions: Permissions.MutePlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/mute {target.Id} 30 cooling off"));

        managed.MutedUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        managed.MuteReason.Should().Be("cooling off");
        SystemText(clients).Should().Be($"{target.Name} has been muted. (30m)");
    }

    // ── /tp ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Teleport_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/tp {Guid.NewGuid()} crestfall"));

        SystemText(clients).Should().Be("You do not have permission to teleport players.");
    }

    [Fact]
    public async Task Teleport_Without_A_Zone_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/tp {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Usage: /tp <characterId> <zoneId>");
    }

    [Fact]
    public async Task Teleport_To_An_Unknown_Zone_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/tp {target.Id} nowhere"));

        SystemText(clients).Should().Be("Zone 'nowhere' not found.");
        clients.Connection("conn-target").SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Teleport_Sends_The_Target_To_The_Named_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/tp {target.Id} the-droveway"));

        var teleported = clients.Connection("conn-target").SentMessages
            .Single().Args[0].Should().BeOfType<TeleportedPayload>().Subject;
        teleported.ZoneId.Should().Be("the-droveway");
        teleported.ZoneName.Should().Be("The Droveway");
        SystemText(clients).Should().Be($"Teleported {target.Name} to The Droveway.");
    }

    // ── /summon ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Summon_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/summon {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("You do not have permission to summon players.");
    }

    [Fact]
    public async Task Summon_Of_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/summon {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Character not found or not online.");
    }

    [Fact]
    public async Task Summon_Without_A_Zone_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Moderator";
        ctx.Items["CurrentZoneGroupName"] = "zone:crestfall";

        await hub.SendChatMessage(new ChatMessageHubRequest($"/summon {target.Id}"));

        SystemText(clients).Should().Be("You must be in a zone to summon.");
    }

    [Fact]
    public async Task Summon_With_An_Unusable_Character_Id_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/summon nope"));

        SystemText(clients).Should().Be("Usage: /summon <characterId>");
    }

    [Fact]
    public async Task Summon_Pulls_The_Target_Into_The_Callers_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.TeleportPlayers);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/summon {target.Id}"));

        var summoned = clients.Connection("conn-target").SentMessages
            .Single().Args[0].Should().BeOfType<SummonedPayload>().Subject;
        summoned.ByCharacterName.Should().Be("Moderator");
        summoned.ZoneId.Should().Be("crestfall");
        SystemText(clients).Should().Be($"Summoned {target.Name} to your location.");
    }

    // ── /give ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Give_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {Guid.NewGuid()} iron-sword"));

        SystemText(clients).Should().Be("You do not have permission to give items.");
    }

    [Fact]
    public async Task Give_Without_An_Item_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Usage: /give <characterId> <itemSlug> [quantity]");
    }

    [Fact]
    public async Task Give_Adds_A_New_Stack_And_Notifies_The_Target()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {target.Id} health_potion 3"));

        var stored = await db.Characters.FindAsync(target.Id);
        var inventory = JsonSerializer.Deserialize<List<Veldrath.Server.Features.Characters.InventoryItemDto>>(
            stored!.InventoryBlob, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        inventory.Should().ContainSingle(i => i.ItemRef == "health_potion" && i.Quantity == 3);

        var received = clients.Connection("conn-target").SentMessages
            .Single().Args[0].Should().BeOfType<ItemReceivedPayload>().Subject;
        received.ItemSlug.Should().Be("health_potion");
        received.Quantity.Should().Be(3);
        received.GivenByUsername.Should().Be("Moderator");
        SystemText(clients).Should().Be($"Gave 3x 'health_potion' to {target.Name}.");
    }

    [Fact]
    public async Task Give_Stacks_Onto_An_Existing_Item_With_The_Same_Ref()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        target.InventoryBlob = """[{"ItemRef":"health_potion","Quantity":2}]""";
        await db.SaveChangesAsync();
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, _, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {target.Id} health_potion 4"));

        var stored = await db.Characters.FindAsync(target.Id);
        var inventory = JsonSerializer.Deserialize<List<Veldrath.Server.Features.Characters.InventoryItemDto>>(
            stored!.InventoryBlob, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        inventory.Should().ContainSingle();
        inventory[0].Quantity.Should().Be(6);
    }

    [Fact]
    public async Task Give_Treats_An_Unparseable_Inventory_Blob_As_Empty()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        target.InventoryBlob = "{not json";
        await db.SaveChangesAsync();
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, _, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {target.Id} health_potion 2"));

        var stored = await db.Characters.FindAsync(target.Id);
        var inventory = JsonSerializer.Deserialize<List<Veldrath.Server.Features.Characters.InventoryItemDto>>(
            stored!.InventoryBlob, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        inventory.Should().ContainSingle(i => i.ItemRef == "health_potion" && i.Quantity == 2);
    }

    // ── /setgold ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task SetGold_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/setgold {Guid.NewGuid()} 100"));

        SystemText(clients).Should().Be("You do not have permission to set gold.");
    }

    [Theory]
    [InlineData("/setgold <id> -5")]
    [InlineData("/setgold <id> nope")]
    public async Task SetGold_With_An_Invalid_Amount_Prints_Usage(string template)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest(
            template.Replace("<id>", Guid.NewGuid().ToString())));

        SystemText(clients).Should().Be("Usage: /setgold <characterId> <amount>");
    }

    [Fact]
    public async Task SetGold_Writes_The_Attribute_And_Notifies_The_Target()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id, attributes: """{"Gold":5,"Strength":10}""");
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/setgold {target.Id} 900"));

        var stored = await db.Characters.FindAsync(target.Id);
        using var document = JsonDocument.Parse(stored!.Attributes);
        document.RootElement.GetProperty("Gold").GetInt32().Should().Be(900);
        document.RootElement.GetProperty("Strength").GetInt32().Should().Be(10, "other attributes are preserved");

        clients.Connection("conn-target").SentMessages.Should()
            .ContainSingle(m => m.Method == "GoldChanged");
        SystemText(clients).Should().Be($"Set gold to 900 for {target.Name}.");
    }

    [Fact]
    public async Task SetGold_Treats_An_Unparseable_Attributes_Blob_As_Empty()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id, attributes: "{broken");
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, _, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/setgold {target.Id} 42"));

        var stored = await db.Characters.FindAsync(target.Id);
        using var document = JsonDocument.Parse(stored!.Attributes);
        document.RootElement.GetProperty("Gold").GetInt32().Should().Be(42);
    }

    // ── /sethealth ───────────────────────────────────────────────────────────
    [Fact]
    public async Task SetHealth_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/sethealth {Guid.NewGuid()} 5"));

        SystemText(clients).Should().Be("You do not have permission to set health.");
    }

    [Fact]
    public async Task SetHealth_With_An_Invalid_Amount_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/sethealth {Guid.NewGuid()} -1"));

        SystemText(clients).Should().Be("Usage: /sethealth <characterId> <amount>");
    }

    [Fact]
    public async Task SetHealth_Clamps_To_The_Characters_Maximum()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(
            db, targetAccount.Id, attributes: """{"MaxHealth":40,"CurrentHealth":10}""");
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.GiveItems);
        ctx.EnterZoneState(Guid.NewGuid(), "Moderator");

        await hub.SendChatMessage(new ChatMessageHubRequest($"/sethealth {target.Id} 999"));

        var stored = await db.Characters.FindAsync(target.Id);
        using var document = JsonDocument.Parse(stored!.Attributes);
        document.RootElement.GetProperty("CurrentHealth").GetInt32().Should().Be(40);
        SystemText(clients).Should().Be($"Set health to 40/40 for {target.Name}.");
    }

    // ── /ban ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ban_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/ban {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("You do not have permission to ban players.");
    }

    [Fact]
    public async Task Ban_With_An_Unusable_Character_Id_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.BanPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/ban nope"));

        SystemText(clients).Should().Be("Usage: /ban <characterId> [reason]");
    }

    [Fact]
    public async Task Ban_Of_An_Unknown_Character_Is_Reported()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.BanPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/ban {Guid.NewGuid()}"));

        SystemText(clients).Should().Be("Character not found.");
    }

    [Fact]
    public async Task Ban_Of_An_Account_The_Server_Cannot_Resolve_Is_Reported()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(null),
            permissions: Permissions.BanPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/ban {target.Id}"));

        SystemText(clients).Should().Be("Account not found.");
    }

    [Fact]
    public async Task Ban_Permanently_Bans_The_Account_And_Kicks_The_Live_Session()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            permissions: Permissions.BanPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/ban {target.Id} exploiting"));

        managed.IsBanned.Should().BeTrue();
        managed.BannedUntil.Should().BeNull();
        managed.BanReason.Should().Be("exploiting");
        clients.Connection("conn-target").SentMessages.Single().Args[0]
            .Should().BeOfType<KickedPayload>().Which.Reason.Should().Be("exploiting");
        SystemText(clients).Should().Be($"{target.Name} has been permanently banned.");
    }

    // ── /suspend ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suspend_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/suspend {Guid.NewGuid()} 4"));

        SystemText(clients).Should().Be("You do not have permission to suspend players.");
    }

    [Theory]
    [InlineData("/suspend <id> 0")]
    [InlineData("/suspend <id> nope")]
    public async Task Suspend_With_An_Invalid_Duration_Prints_Usage(string template)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.SuspendPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest(
            template.Replace("<id>", Guid.NewGuid().ToString())));

        SystemText(clients).Should().Be("Usage: /suspend <characterId> <hours> [reason]");
    }

    [Fact]
    public async Task Suspend_Sets_A_Temporary_Ban_Expiry()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var managed = Account();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: HubHarness.NewUserManager(managed),
            permissions: Permissions.SuspendPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/suspend {target.Id} 6 griefing"));

        managed.IsBanned.Should().BeTrue();
        managed.BannedUntil.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(6), TimeSpan.FromMinutes(1));
        managed.BanReason.Should().Be("griefing");
        clients.Connection("conn-target").SentMessages.Single().Method.Should().Be("OnKicked");
        SystemText(clients).Should().StartWith($"{target.Name} suspended for 6h. Expires: ");
    }

    // ── /lookup ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/lookup Someone"));

        SystemText(clients).Should().Be("You do not have permission to look up players.");
    }

    [Fact]
    public async Task Lookup_Without_A_Name_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.ViewPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/lookup"));

        SystemText(clients).Should().Be("Usage: /lookup <characterName>");
    }

    [Fact]
    public async Task Lookup_Of_An_Unknown_Character_Is_Reported()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.ViewPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/lookup Nobody"));

        SystemText(clients).Should().Be("Character 'Nobody' not found.");
    }

    [Fact]
    public async Task Lookup_Reports_Online_Status_Ban_And_Mute_State()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target", "the-droveway");

        var managed = Account();
        managed.IsBanned = true;
        managed.BannedUntil = DateTimeOffset.UtcNow.AddHours(2);
        managed.IsMuted = true;
        managed.MutedUntil = null;
        managed.WarnCount = 2;

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id,
            userManager: CallerIsNotManaged(account.Id, managed),
            permissions: Permissions.ViewPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/lookup {target.Name}"));

        var text = SystemText(clients);
        text.Should().Contain($"Character: {target.Name}").And.Contain("Status:    Online (zone: the-droveway)");
        text.Should().Contain("Warnings:  2").And.Contain("Ban:       Suspended until ").And.Contain("Mute:      Permanently muted");
    }

    // ── /event ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Event_Is_Denied_Without_The_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/event invasion"));

        SystemText(clients).Should().Be("You do not have permission to run events.");
    }

    [Fact]
    public async Task Event_Reports_That_Events_Are_Not_Implemented_Yet()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, permissions: Permissions.RunEvents);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/event invasion"));

        SystemText(clients).Should().Be("Game events are not yet implemented. This command is a stub.");
    }
}
