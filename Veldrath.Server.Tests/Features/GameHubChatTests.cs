using Veldrath.Contracts.Chat;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the chat surface of <see cref="GameHub"/>: direct chat methods
/// (<c>SendZoneMessage</c>, <c>SendGlobalMessage</c>, <c>SendWhisper</c>) and the
/// slash-command router behind <see cref="GameHub.SendChatMessage"/>.
/// </summary>
public class GameHubChatTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    // ── SendChatMessage ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Without_A_Character_Tells_The_Caller_To_Select_One()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SendChatMessage(new ChatMessageHubRequest("hello"));

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "Error");
        clients.Caller.SentMessages.Single().Args[0].Should().Be("Select a character before chatting.");
    }

    [Fact]
    public async Task SendChatMessage_Outside_A_Zone_Tells_The_Caller_To_Enter_One()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        await hub.SendChatMessage(new ChatMessageHubRequest("hello"));

        clients.Caller.SentMessages.Single(m => m.Method == "Error").Args[0]
            .Should().Be("Enter a zone before chatting.");
    }

    [Fact]
    public async Task SendChatMessage_From_A_Muted_Account_Explains_The_Mute_And_Sends_Nothing_Else()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var muted = new Veldrath.Server.Data.Entities.PlayerAccount
        {
            UserName = "muted",
            NormalizedUserName = "MUTED",
            IsMuted = true,
            MuteReason = "spam",
        };
        var userManager = HubHarness.NewUserManager(muted);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, userManager: userManager);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("hello"));

        clients.Caller.SentMessages.Should().ContainSingle();
        clients.Caller.SentMessages.Single().Method.Should().Be("SystemMessage");
        clients.Caller.SentMessages.Single().Args[0].Should().Be("You are muted. Reason: spam");
        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendChatMessage_With_Only_Whitespace_Broadcasts_Nothing()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("   "));

        clients.EveryMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task SendChatMessage_Trims_And_Broadcasts_To_The_Zone_Group()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("  hold the line  "));

        var message = clients.GroupNamed("zone:crestfall").SentMessages
            .Should().ContainSingle(m => m.Method == "ReceiveChatMessage").Subject;
        var dto = message.Args[0].Should().BeOfType<ChatMessageHubDto>().Subject;
        dto.CharacterId.Should().Be(characterId);
        dto.Channel.Should().Be("Zone");
        dto.Sender.Should().Be("Bran");
        dto.Message.Should().Be("hold the line");
        clients.Caller.SentMessages.Should().BeEmpty("the zone group includes the sender");
    }

    [Fact]
    public async Task SendChatMessage_Truncates_At_The_Configured_Max_Length()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, moderation: new ModerationOptions { MaxChatMessageLength = 5 });
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("abcdefghij"));

        var dto = clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Subject;
        dto.Message.Should().Be("abcde");
    }

    [Fact]
    public async Task SendChatMessage_While_Afk_Announces_The_Return_Before_The_Message()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var afk = new Veldrath.Server.Services.AfkTracker();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, afk: afk);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");
        afk.Set(characterId, "lunch");

        await hub.SendChatMessage(new ChatMessageHubRequest("back"));

        var sent = clients.GroupNamed("zone:crestfall").SentMessages;
        sent.Should().HaveCount(2);
        sent[0].Args[0].Should().BeOfType<ChatMessageHubDto>()
            .Which.Message.Should().Be("* is no longer AFK *");
        sent[1].Args[0].Should().BeOfType<ChatMessageHubDto>()
            .Which.Message.Should().Be("back");
        afk.TryGet(characterId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendChatMessage_When_Not_Afk_Sends_Only_The_Message()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("hello"));

        clients.GroupNamed("zone:crestfall").SentMessages.Should().ContainSingle();
    }

    // ── Slash commands: universal ────────────────────────────────────────────

    [Fact]
    public async Task Help_Command_Lists_Only_The_Players_Own_Commands()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/help"));

        var text = clients.Caller.SentMessages.Single(m => m.Method == "SystemMessage").Args[0]
            .Should().BeOfType<string>().Subject;
        text.Should().StartWith("Available commands:").And.Contain("/roll").And.Contain("/afk");
        text.Should().NotContain(Permissions.BanPlayers, "an unprivileged caller cannot ban");
    }

    [Fact]
    public async Task Help_Command_Marks_Admin_Commands_With_Their_Permission()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, connectionId: "conn-admin", permissions: Permissions.BanPlayers);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/help"));

        var text = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<string>().Subject;
        text.Should().Contain("/ban").And.Contain($"[{Permissions.BanPlayers}]");
    }

    [Fact]
    public async Task Unknown_Command_Is_Reported_With_A_Pointer_To_Help()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/dance"));

        clients.Caller.SentMessages.Single().Args[0].Should()
            .Be("Unknown command '/dance'. Type /help for a list of commands.");
    }

    [Fact]
    public async Task A_Bare_Slash_Is_Ignored()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/"));

        clients.EveryMessage.Should().BeEmpty();
    }

    // ── Slash commands: /who ─────────────────────────────────────────────────

    [Fact]
    public async Task Who_Without_A_Zone_Identifier_Reports_Not_In_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CurrentZoneGroupName"] = "zone:crestfall";

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("You are not in a zone.");
    }

    [Fact]
    public async Task Who_With_An_Empty_Zone_Says_Nobody_Is_There()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran", "the-droveway");

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Players here: No players in this zone.");
    }

    [Fact]
    public async Task Who_Lists_The_Shared_Scope_Of_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var mine = await HubHarness.SeedCharacterAsync(db, account.Id);
        var otherAccount = await HubHarness.SeedAccountAsync(db);
        var other = await HubHarness.SeedCharacterAsync(db, otherAccount.Id);
        await HubHarness.SeedSessionAsync(db, mine, "conn-mine", "the-droveway");
        await HubHarness.SeedSessionAsync(db, other, "conn-other", "the-droveway");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(mine.Id, mine.Name, "the-droveway");

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        var text = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<string>().Subject;
        text.Should().Be($"Players here: {mine.Name}, {other.Name}");
    }

    [Fact]
    public async Task Who_Does_Not_Name_A_Character_Inside_An_Instance_Run()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var mine = await HubHarness.SeedCharacterAsync(db, account.Id);
        var residentAccount = await HubHarness.SeedAccountAsync(db);
        var resident = await HubHarness.SeedCharacterAsync(db, residentAccount.Id);
        var delverAccount = await HubHarness.SeedAccountAsync(db);
        var delver = await HubHarness.SeedCharacterAsync(db, delverAccount.Id);

        await HubHarness.SeedSessionAsync(db, mine, "conn-mine", "the-droveway");
        await HubHarness.SeedSessionAsync(db, resident, "conn-resident", "the-droveway");
        var delverSession = await HubHarness.SeedSessionAsync(db, delver, "conn-delver", "the-droveway");
        delverSession.ZoneInstanceId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(mine.Id, mine.Name, "the-droveway");

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        var text = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<string>().Subject;
        text.Should().StartWith("Players here: ");
        text["Players here: ".Length..].Split(", ").Should().BeEquivalentTo(
            [mine.Name, resident.Name],
            "the delver stands in its run's SignalR group, which the shared scope cannot see or chat with");
    }

    [Fact]
    public async Task Who_Inside_An_Instance_Run_Lists_The_Runs_Members_And_Not_The_Shared_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var mine = await HubHarness.SeedCharacterAsync(db, account.Id);
        var partyAccount = await HubHarness.SeedAccountAsync(db);
        var party = await HubHarness.SeedCharacterAsync(db, partyAccount.Id);
        var residentAccount = await HubHarness.SeedAccountAsync(db);
        var resident = await HubHarness.SeedCharacterAsync(db, residentAccount.Id);

        var runId = Guid.NewGuid();
        var mineSession = await HubHarness.SeedSessionAsync(db, mine, "conn-mine", "the-droveway");
        var partySession = await HubHarness.SeedSessionAsync(db, party, "conn-party", "the-droveway");
        mineSession.ZoneInstanceId = runId;
        partySession.ZoneInstanceId = runId;
        await HubHarness.SeedSessionAsync(db, resident, "conn-resident", "the-droveway");
        await db.SaveChangesAsync();

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(mine.Id, mine.Name, "the-droveway", "zone:the-droveway_normal#i1:" + runId.ToString("D"));
        ctx.Items["CurrentZoneInstanceId"] = runId;

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        var text = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<string>().Subject;
        text.Should().StartWith("Players here: ");
        text["Players here: ".Length..].Split(", ").Should().BeEquivalentTo(
            [mine.Name, party.Name],
            "a run is its own SignalR group, so the shared resident beside the dungeon door is not in it");
    }

    [Fact]
    public async Task Who_In_A_Wilderness_Zone_Lists_The_Callers_Difficulty_Shard_Only()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var mine = await HubHarness.SeedCharacterAsync(db, account.Id);
        var shardmateAccount = await HubHarness.SeedAccountAsync(db);
        var shardmate = await HubHarness.SeedCharacterAsync(db, shardmateAccount.Id);
        var hardcoreAccount = await HubHarness.SeedAccountAsync(db);
        var hardcore = await HubHarness.SeedCharacterAsync(db, hardcoreAccount.Id);
        hardcore.DifficultyMode = "hardcore";
        await db.SaveChangesAsync();

        await HubHarness.SeedSessionAsync(db, mine, "conn-mine", "the-droveway");
        await HubHarness.SeedSessionAsync(db, shardmate, "conn-shardmate", "the-droveway");
        await HubHarness.SeedSessionAsync(db, hardcore, "conn-hardcore", "the-droveway");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(mine.Id, mine.Name, "the-droveway", "zone:the-droveway_normal");
        ctx.Items["DifficultyMode"] = "normal";

        await hub.SendChatMessage(new ChatMessageHubRequest("/who"));

        var text = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<string>().Subject;
        text.Should().StartWith("Players here: ");
        text["Players here: ".Length..].Split(", ").Should().BeEquivalentTo(
            [mine.Name, shardmate.Name],
            "the hardcore character is in zone:the-droveway_hardcore, not in the caller's group");
    }

    // ── Slash commands: /whisper ─────────────────────────────────────────────

    [Fact]
    public async Task Whisper_Without_Message_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/w Bran"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Usage: /whisper <characterName> <message>");
    }

    [Fact]
    public async Task Whisper_To_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/whisper Ghost hello there"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("'Ghost' is not online.");
    }

    [Fact]
    public async Task Whisper_Delivers_To_The_Target_And_Echoes_To_The_Sender()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        var senderId = Guid.NewGuid();
        ctx.EnterZoneState(senderId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/whisper " + target.Name + " meet me at the gate"));

        var delivered = clients.Connection("conn-target").SentMessages
            .Should().ContainSingle(m => m.Method == "OnWhisper").Subject;
        var payload = delivered.Args[0].Should().BeOfType<WhisperPayload>().Subject;
        payload.FromCharacterId.Should().Be(senderId);
        payload.FromCharacterName.Should().Be("Bran");
        payload.Message.Should().Be("meet me at the gate");

        var echo = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<WhisperPayload>().Subject;
        echo.FromCharacterName.Should().Be($"[To {target.Name}]");
    }

    [Fact]
    public async Task Whisper_To_An_Afk_Target_Adds_An_Auto_Reply()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var afk = new Veldrath.Server.Services.AfkTracker();
        afk.Set(target.Id, "eating");
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, afk: afk);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/w " + target.Name + " ping"));

        clients.Caller.SentMessages.Should().HaveCount(2);
        clients.Caller.SentMessages[1].Args[0].Should().BeOfType<WhisperPayload>()
            .Which.Message.Should().Be("[AFK] eating");
    }

    // ── Slash commands: /emote ───────────────────────────────────────────────

    [Fact]
    public async Task Emote_Without_An_Action_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/emote"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Usage: /emote <action>");
    }

    [Fact]
    public async Task Emote_Is_Broadcast_To_The_Zone_Group()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/emote waves slowly"));

        var payload = clients.GroupNamed("zone:crestfall").SentMessages
            .Single(m => m.Method == "OnEmote").Args[0]
            .Should().BeOfType<EmotePayload>().Subject;
        payload.CharacterId.Should().Be(characterId);
        payload.CharacterName.Should().Be("Bran");
        payload.Action.Should().Be("waves slowly");
    }

    // ── Slash commands: /roll ────────────────────────────────────────────────

    [Fact]
    public async Task Roll_Defaults_To_A_Hundred_Sided_Die()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, random: FakeRandomSource.Returning(42));
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/roll"));

        clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Which.Message.Should().Be("* rolled 42 (1-100) *");
    }

    [Fact]
    public async Task Roll_Honours_An_Explicit_Maximum()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, random: FakeRandomSource.Returning(7));
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/roll 20"));

        clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Which.Message.Should().Be("* rolled 7 (1-20) *");
    }

    [Theory]
    [InlineData("/roll abc")]
    [InlineData("/roll 1")]
    public async Task Roll_Falls_Back_To_A_Hundred_When_The_Maximum_Is_Unusable(string command)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, random: FakeRandomSource.Returning(3));
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest(command));

        clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Which.Message.Should().Be("* rolled 3 (1-100) *");
    }

    // ── Slash commands: /afk ─────────────────────────────────────────────────

    [Fact]
    public async Task Afk_With_A_Reason_Toggles_The_Flag_And_Tells_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var afk = new Veldrath.Server.Services.AfkTracker();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, afk: afk);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/afk making tea"));

        afk.TryGet(characterId, out var reason).Should().BeTrue();
        reason.Should().Be("making tea");
        clients.Caller.SentMessages.Single().Args[0].Should().Be("You are now AFK: making tea.");
        clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Which.Message.Should().Be("* is now AFK: making tea *");
    }

    [Fact]
    public async Task Afk_Without_A_Reason_Reports_Plain_Afk()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var afk = new Veldrath.Server.Services.AfkTracker();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, afk: afk);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/afk"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("You are now AFK.");
        afk.TryGet(characterId, out var reason).Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public async Task Afk_When_Already_Away_Clears_The_Flag()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var afk = new Veldrath.Server.Services.AfkTracker();
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, afk: afk);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran");
        afk.Set(characterId, "brb");

        await hub.SendChatMessage(new ChatMessageHubRequest("/afk"));

        afk.TryGet(characterId, out _).Should().BeFalse();
        clients.Caller.SentMessages.Single().Args[0].Should().Be("You are no longer AFK.");
        clients.GroupNamed("zone:crestfall").SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Which.Message.Should().Be("* is no longer AFK *");
    }

    // ── Slash commands: /ignore ──────────────────────────────────────────────

    [Fact]
    public async Task Ignore_Without_A_Name_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/ignore"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Usage: /ignore <characterName>");
    }

    [Fact]
    public async Task Ignore_Resolves_An_Online_Character_From_Its_Session()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/ignore " + target.Name));

        var payload = clients.Caller.SentMessages
            .Single(m => m.Method == "CharacterIgnored").Args[0]
            .Should().BeOfType<CharacterIgnoredPayload>().Subject;
        payload.CharacterId.Should().Be(target.Id);
        payload.CharacterName.Should().Be(target.Name);
    }

    [Fact]
    public async Task Ignore_Falls_Back_To_The_Database_For_An_Offline_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/ignore " + target.Name));

        clients.Caller.SentMessages.Single(m => m.Method == "CharacterIgnored").Args[0]
            .Should().BeOfType<CharacterIgnoredPayload>().Which.CharacterId.Should().Be(target.Id);
    }

    [Fact]
    public async Task Ignore_Of_An_Unknown_Character_Is_Reported()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/ignore Nobody"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Character 'Nobody' not found.");
    }

    // ── Slash commands: /report ──────────────────────────────────────────────

    [Fact]
    public async Task Report_Without_A_Reason_Prints_Usage()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/report Someone"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("Usage: /report <characterName> <reason>");
    }

    [Fact]
    public async Task Report_Against_An_Offline_Character_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/report Ghost being rude"));

        clients.Caller.SentMessages.Single().Args[0].Should().Be("'Ghost' is not online.");
        db.PlayerReports.Should().BeEmpty();
    }

    [Fact]
    public async Task Report_Persists_A_PlayerReport_And_Thanks_The_Reporter()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        var reporterId = Guid.NewGuid();
        ctx.EnterZoneState(reporterId, "Bran");

        await hub.SendChatMessage(new ChatMessageHubRequest("/report " + target.Name + " shouting at people"));

        var report = db.PlayerReports.Should().ContainSingle().Subject;
        report.ReporterCharacterId.Should().Be(reporterId);
        report.ReporterName.Should().Be("Bran");
        report.TargetCharacterId.Should().Be(target.Id);
        report.TargetName.Should().Be(target.Name);
        report.Reason.Should().Be("shouting at people");

        clients.Caller.SentMessages.Single().Args[0].Should()
            .Be($"Report submitted against '{target.Name}'. Thank you.");
    }

    // ── SendPartyMessage guard ───────────────────────────────────────────────

    [Fact]
    public async Task SendPartyMessage_Without_A_Character_Name_Asks_For_Selection()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        await hub.SendPartyMessage(new SendZoneChatMessageHubRequest("hello"));

        clients.Caller.SentMessages.Single().Args[0]
            .Should().Be("SelectCharacter must be called before SendPartyMessage");
    }

    // ── SendZoneMessage / SendGlobalMessage / SendWhisper ────────────────────

    [Fact]
    public async Task SendZoneMessage_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SendZoneMessage(new SendZoneChatMessageHubRequest("hi"));

        clients.Caller.SentMessages.Single().Args[0]
            .Should().Be("SelectCharacter must be called before SendZoneMessage");
    }

    [Fact]
    public async Task SendZoneMessage_Without_A_Zone_Group_Falls_Back_To_The_Caller()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendZoneMessage(new SendZoneChatMessageHubRequest("  hi  "));

        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
        var dto = clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<ChatMessageHubDto>().Subject;
        dto.Channel.Should().Be("Zone");
        dto.Message.Should().Be("hi");
    }

    [Fact]
    public async Task SendZoneMessage_In_A_Zone_Broadcasts_To_The_Group()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran");

        await hub.SendZoneMessage(new SendZoneChatMessageHubRequest("hi"));

        clients.GroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "ReceiveChatMessage");
        clients.Caller.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendZoneMessage_With_Blank_Text_Sends_Nothing()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.SendZoneMessage(new SendZoneChatMessageHubRequest("   "));

        clients.EveryMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task SendGlobalMessage_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SendGlobalMessage(new SendGlobalChatMessageHubRequest("hi"));

        clients.Caller.SentMessages.Single().Args[0]
            .Should().Be("SelectCharacter must be called before SendGlobalMessage");
    }

    [Fact]
    public async Task SendGlobalMessage_From_A_Muted_Account_Is_Blocked_With_A_Reason()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var muted = new Veldrath.Server.Data.Entities.PlayerAccount
        {
            UserName = "muted",
            NormalizedUserName = "MUTED",
            IsMuted = true,
            MuteReason = "slurs",
            MutedUntil = DateTimeOffset.UtcNow.AddHours(1),
        };
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, userManager: HubHarness.NewUserManager(muted));
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendGlobalMessage(new SendGlobalChatMessageHubRequest("hi"));

        clients.All.SentMessages.Should().BeEmpty();
        clients.Caller.SentMessages.Single().Args[0].Should()
            .BeOfType<string>().Which.Should().StartWith("You are muted until ").And.EndWith("Reason: slurs");
    }

    [Fact]
    public async Task SendGlobalMessage_Broadcasts_To_Everyone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendGlobalMessage(new SendGlobalChatMessageHubRequest("  server restart soon  "));

        var dto = clients.All.SentMessages.Single().Args[0].Should().BeOfType<ChatMessageHubDto>().Subject;
        dto.Channel.Should().Be("Global");
        dto.Sender.Should().Be("Bran");
        dto.Message.Should().Be("server restart soon");
    }

    [Fact]
    public async Task SendGlobalMessage_With_Blank_Text_Broadcasts_Nothing()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendGlobalMessage(new SendGlobalChatMessageHubRequest(" "));

        clients.EveryMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task SendWhisper_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SendWhisper(new SendWhisperHubRequest("Ghost", "hi"));

        clients.Caller.SentMessages.Single().Args[0]
            .Should().Be("SelectCharacter must be called before SendWhisper");
    }

    [Fact]
    public async Task SendWhisper_From_A_Muted_Account_Is_Blocked()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var muted = new Veldrath.Server.Data.Entities.PlayerAccount
        {
            UserName = "muted",
            NormalizedUserName = "MUTED",
            IsMuted = true,
        };
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, userManager: HubHarness.NewUserManager(muted));
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendWhisper(new SendWhisperHubRequest("Ghost", "hi"));

        clients.Caller.SentMessages.Single().Method.Should().Be("SystemMessage");
        clients.Caller.SentMessages.Single().Args[0].Should()
            .BeOfType<string>().Which.Should().Be("You are muted. Reason: No reason given.");
    }

    [Theory]
    [InlineData("   ", "Ghost")]
    [InlineData("hi", "   ")]
    public async Task SendWhisper_With_Blank_Message_Or_Target_Sends_Nothing(string message, string target)
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendWhisper(new SendWhisperHubRequest(target, message));

        clients.EveryMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task SendWhisper_To_An_Offline_Target_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendWhisper(new SendWhisperHubRequest("Ghost", "hi"));

        clients.Caller.SentMessages.Single().Args[0].Should()
            .Be("Player 'Ghost' is not online.");
    }

    [Fact]
    public async Task SendWhisper_Delivers_And_Echoes_With_The_Target_In_The_Sender_Field()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var targetAccount = await HubHarness.SeedAccountAsync(db);
        var target = await HubHarness.SeedCharacterAsync(db, targetAccount.Id);
        await HubHarness.SeedSessionAsync(db, target, "conn-target");

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-sender");
        var senderId = Guid.NewGuid();
        ctx.Items["CharacterId"] = senderId;
        ctx.Items["CharacterName"] = "Bran";

        await hub.SendWhisper(new SendWhisperHubRequest(target.Name, "  psst  "));

        var delivered = clients.Connection("conn-target").SentMessages
            .Single().Args[0].Should().BeOfType<ChatMessageHubDto>().Subject;
        delivered.Channel.Should().Be("Whisper");
        delivered.CharacterId.Should().Be(senderId);
        delivered.Message.Should().Be("psst");

        var echo = clients.Caller.SentMessages.Single().Args[0]
            .Should().BeOfType<ChatMessageHubDto>().Subject;
        echo.CharacterId.Should().Be(Guid.Empty);
        echo.Sender.Should().Be($"To {target.Name}");
        echo.Message.Should().Be("psst");
    }
}
