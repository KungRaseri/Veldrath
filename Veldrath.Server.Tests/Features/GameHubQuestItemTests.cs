using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.LevelUp;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Hubs;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the ground-item, quest, NPC, crafting and dungeon hub methods of
/// <see cref="GameHub"/>, including the quest-objective pokes those actions trigger.
/// </summary>
public class GameHubQuestItemTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    private static GroundItemView SwordOnTheGround() =>
        new(Guid.NewGuid(), "iron_sword", "Iron Sword", "Common", 1);

    // ── GetGroundItems ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetGroundItems_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetGroundItems();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetGroundItems");
    }

    [Fact]
    public async Task GetGroundItems_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new GetGroundItemsHubResult { Success = false, ErrorMessage = "Character not found" });

        await hub.GetGroundItems();

        ShouldBeError(clients.Caller, "Character not found");
    }

    [Fact]
    public async Task GetGroundItems_Sends_The_Items_At_The_Location()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new GetGroundItemsHubResult { Success = true, Items = [SwordOnTheGround()] });

        await hub.GetGroundItems();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "GroundItemsChanged").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            Items = new[] { new { ItemRef = "iron_sword", Name = "Iron Sword", Quantity = 1 } },
        });
    }

    // ── DropItemToGround ─────────────────────────────────────────────────────

    [Fact]
    public async Task DropItemToGround_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.DropItemToGround("iron_sword");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before DropItemToGround");
    }

    [Fact]
    public async Task DropItemToGround_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new DropItemToGroundHubResult { Success = false, ErrorMessage = "Item not in bag" });

        await hub.DropItemToGround("iron_sword");

        ShouldBeError(clients.Caller, "Item not in bag");
    }

    [Fact]
    public async Task DropItemToGround_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<DropItemToGroundHubResult>(new InvalidOperationException("boom"));

        await hub.DropItemToGround("iron_sword");

        ShouldBeError(clients.Caller, "Failed to drop item");
    }

    [Fact]
    public async Task DropItemToGround_Broadcasts_The_New_Ground_State_To_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new DropItemToGroundHubResult { Success = true, RemainingQuantity = 0, Items = [SwordOnTheGround()] });
        mediator.Returns(new GetInventoryHubResult { Success = true });

        await hub.DropItemToGround("iron_sword");

        var payload = clients.GroupNamed("zone:crestfall").SentMessages
            .Single(m => m.Method == "GroundItemsChanged").Args[0];
        payload.Should().BeEquivalentTo(new { CharacterId = characterId, ItemRef = "iron_sword" });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task DropItemToGround_Falls_Back_To_The_Caller_Outside_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new DropItemToGroundHubResult { Success = true, Items = [] });

        await hub.DropItemToGround("iron_sword");

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "GroundItemsChanged");
        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    // ── PickUpGroundItem ─────────────────────────────────────────────────────

    [Fact]
    public async Task PickUpGroundItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.PickUpGroundItem(Guid.NewGuid());

        ShouldBeError(clients.Caller, "SelectCharacter must be called before PickUpGroundItem");
    }

    [Fact]
    public async Task PickUpGroundItem_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new PickUpGroundItemHubResult { Success = false, ErrorMessage = "Item already taken" });

        await hub.PickUpGroundItem(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Item already taken");
    }

    [Fact]
    public async Task PickUpGroundItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<PickUpGroundItemHubResult>(new InvalidOperationException("boom"));

        await hub.PickUpGroundItem(Guid.NewGuid());

        ShouldBeError(clients.Caller, "Failed to pick up item");
    }

    [Fact]
    public async Task PickUpGroundItem_Broadcasts_The_New_Ground_State_And_Collects_The_Objective()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new PickUpGroundItemHubResult { Success = true, ItemRef = "iron_sword", Items = [] });
        mediator.Returns(new GetInventoryHubResult { Success = true });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.PickUpGroundItem(Guid.NewGuid());

        var payload = clients.GroupNamed("zone:crestfall").SentMessages
            .Single(m => m.Method == "GroundItemsChanged").Args[0];
        payload.Should().BeEquivalentTo(new { CharacterId = characterId, ItemRef = "iron_sword" });

        var advance = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>().Single();
        advance.Kind.Should().Be("collect");
        advance.Key.Should().Be("iron_sword");
    }

    [Fact]
    public async Task PickUpGroundItem_Falls_Back_To_The_Caller_Outside_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new PickUpGroundItemHubResult { Success = true, ItemRef = "iron_sword" });
        mediator.Returns(new GetInventoryHubResult { Success = true });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.PickUpGroundItem(Guid.NewGuid());

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "GroundItemsChanged");
        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    // ── GetQuestLog ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestLog_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetQuestLog();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetQuestLog");
    }

    [Fact]
    public async Task GetQuestLog_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new GetQuestLogHubResult { Success = false, ErrorMessage = "Character not found." });

        await hub.GetQuestLog();

        ShouldBeError(clients.Caller, "Character not found.");
    }

    [Fact]
    public async Task GetQuestLog_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<GetQuestLogHubResult>(new InvalidOperationException("boom"));

        await hub.GetQuestLog();

        ShouldBeError(clients.Caller, "Failed to load quest log");
    }

    [Fact]
    public async Task GetQuestLog_Sends_The_Journal_And_Available_Quests()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        var entry = new QuestLogEntryDto(
            "the-halrow", "The Halrow", "Clear the estate", "Active", 50, 100, false,
            [new QuestObjectiveDto("kill:barrow-wolf", 1, 3, false)]);
        mediator.Returns(new GetQuestLogHubResult
        {
            Success = true,
            Quests = [entry],
            Available = [new AvailableQuestDto("rats", "Rats", "Kill rats", 5, 10, ["kill:rat"])],
        });

        await hub.GetQuestLog();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "QuestLogReceived").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            Quests = new[] { new { Slug = "the-halrow", Title = "The Halrow", Status = "Active" } },
            Available = new[] { new { Slug = "rats" } },
        });
    }

    // ── AcceptQuest / AbandonQuest ───────────────────────────────────────────

    [Fact]
    public async Task AcceptQuest_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.AcceptQuest("rats");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before AcceptQuest");
    }

    [Fact]
    public async Task AcceptQuest_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new AcceptQuestHubResult { Success = false, ErrorMessage = "Quest not found" });

        await hub.AcceptQuest("rats");

        ShouldBeError(clients.Caller, "Quest not found");
    }

    [Fact]
    public async Task AcceptQuest_Returns_The_Updated_Journal()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new AcceptQuestHubResult
        {
            Success = true,
            Quests = [new QuestLogEntryDto("rats", "Rats", "Kill rats", "Active", 5, 10, false, [])],
        });

        await hub.AcceptQuest("rats");

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "QuestLogReceived").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            Quests = new[] { new { Slug = "rats" } },
        });
    }

    [Fact]
    public async Task AbandonQuest_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.AbandonQuest("rats");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before AbandonQuest");
    }

    [Fact]
    public async Task AbandonQuest_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new AbandonQuestHubResult { Success = false, ErrorMessage = "Quest is not active" });

        await hub.AbandonQuest("rats");

        ShouldBeError(clients.Caller, "Quest is not active");
    }

    [Fact]
    public async Task AbandonQuest_Returns_The_Updated_Journal()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new AbandonQuestHubResult { Success = true, Quests = [] });

        await hub.AbandonQuest("rats");

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "QuestLogReceived").Args[0];
        payload.Should().BeEquivalentTo(new { CharacterId = characterId, Quests = Array.Empty<object>() });
    }

    // ── TurnInQuest ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TurnInQuest_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.TurnInQuest("rats");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before TurnInQuest");
    }

    [Fact]
    public async Task TurnInQuest_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new TurnInQuestHubResult { Success = false, ErrorMessage = "Objectives are incomplete" });

        await hub.TurnInQuest("rats");

        ShouldBeError(clients.Caller, "Objectives are incomplete");
    }

    [Fact]
    public async Task TurnInQuest_Grants_Gold_And_Experience_Then_Announces_The_Rewards()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new TurnInQuestHubResult
        {
            Success = true,
            Slug = "rats",
            GoldReward = 25,
            XpReward = 60,
            Quests = [],
        });
        mediator.Returns(new AddGoldHubResult { Success = true, GoldAdded = 25, NewGoldTotal = 125 });
        mediator.Returns(new GainExperienceHubResult { Success = true, NewLevel = 2, NewExperience = 60 });

        await hub.TurnInQuest("rats");

        var gold = mediator.Invocations.Select(i => i.Arguments[0]).OfType<AddGoldHubCommand>().Single();
        gold.Amount.Should().Be(25);
        var xp = mediator.Invocations.Select(i => i.Arguments[0]).OfType<GainExperienceHubCommand>().Single();
        xp.Amount.Should().Be(60);
        xp.Source.Should().Be("Quest");

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "QuestTurnedIn").Args[0];
        payload.Should().BeEquivalentTo(new { CharacterId = characterId, Slug = "rats", GoldReward = 25, XpReward = 60 });
    }

    [Fact]
    public async Task TurnInQuest_With_No_Rewards_Sends_No_Reward_Commands()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new TurnInQuestHubResult { Success = true, Slug = "rats", Quests = [] });

        await hub.TurnInQuest("rats");

        mediator.Invocations.Select(i => i.Arguments[0])
            .Should().NotContain(a => a is AddGoldHubCommand || a is GainExperienceHubCommand);
        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "QuestTurnedIn");
    }

    // ── TalkToNpc ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TalkToNpc_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.TalkToNpc("innkeeper");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before TalkToNpc");
    }

    [Fact]
    public async Task TalkToNpc_Without_A_Slug_Is_Rejected()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        await hub.TalkToNpc("   ");

        ShouldBeError(clients.Caller, "An NPC slug is required.");
    }

    [Fact]
    public async Task TalkToNpc_Advances_The_Talk_Objective_And_Pushes_A_Changed_Journal()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-talk");
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new AdvanceQuestObjectivesHubResult
        {
            Success = true,
            QuestsChanged = 1,
            Quests = [new QuestLogEntryDto("rats", "Rats", "Talk to the innkeeper", "Active", 5, 10, false, [])],
        });

        await hub.TalkToNpc("  innkeeper  ");

        var advance = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>().Single();
        advance.Kind.Should().Be("talk");
        advance.Key.Should().Be("innkeeper", "the slug is trimmed before dispatch");
        clients.Connection("conn-talk").SentMessages.Should()
            .ContainSingle(m => m.Method == "QuestLogReceived");
    }

    [Fact]
    public async Task TalkToNpc_Stays_Silent_When_No_Quest_Changed()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.TalkToNpc("innkeeper");

        clients.EveryMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task TalkToNpc_Swallows_A_Failing_Objective_Advance()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<AdvanceQuestObjectivesHubResult>(new InvalidOperationException("journal broken"));

        var act = () => hub.TalkToNpc("innkeeper");

        await act.Should().NotThrowAsync("a best-effort objective advance must never break the player action");
        clients.EveryMessage.Should().BeEmpty();
    }

    // ── CraftItem ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CraftItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.CraftItem("iron-sword");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before CraftItem");
    }

    [Fact]
    public async Task CraftItem_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new CraftItemHubResult { Success = false, ErrorMessage = "Not enough gold to craft" });

        await hub.CraftItem("iron-sword");

        ShouldBeError(clients.Caller, "Not enough gold to craft");
    }

    [Fact]
    public async Task CraftItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<CraftItemHubResult>(new InvalidOperationException("boom"));

        await hub.CraftItem("iron-sword");

        ShouldBeError(clients.Caller, "Failed to craft item");
    }

    [Fact]
    public async Task CraftItem_Announces_The_Craft_And_Advances_The_Craft_Objective()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new CraftItemHubResult
        {
            Success = true,
            RecipeSlug = "iron-sword",
            RecipeName = "Iron sword",
            ItemRef = "@items/weapons/swords:iron-sword",
            OutputQuantity = 1,
        });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.CraftItem("iron-sword");

        var payload = clients.GroupNamed("zone:crestfall").SentMessages
            .Single(m => m.Method == "ItemCrafted").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            RecipeSlug = "iron-sword",
            RecipeName = "Iron sword",
            ItemRef = "@items/weapons/swords:iron-sword",
            OutputQuantity = 1,
            ConsumedMaterials = Array.Empty<ConsumedMaterialDto>(),
            SkillId = (string?)null,
            SkillXp = 0,
        });

        var advance = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>().Single();
        advance.Kind.Should().Be("craft");
        advance.Key.Should().Be("iron-sword");
    }

    [Fact]
    public async Task CraftItem_Without_A_Crafted_Slug_Skips_The_Objective()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, _, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new CraftItemHubResult { Success = true, ItemRef = null });

        await hub.CraftItem("iron-sword");

        mediator.Invocations.Select(i => i.Arguments[0])
            .Should().NotContain(a => a is AdvanceQuestObjectivesHubCommand);
    }

    // ── EnterDungeon ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnterDungeon_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.EnterDungeon("the-halrow");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before EnterDungeon");
    }

    [Fact]
    public async Task EnterDungeon_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new EnterDungeonHubResult { Success = false, ErrorMessage = "'Crestfall' is not a dungeon" });

        await hub.EnterDungeon("crestfall");

        ShouldBeError(clients.Caller, "'Crestfall' is not a dungeon");
    }

    [Fact]
    public async Task EnterDungeon_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<EnterDungeonHubResult>(new InvalidOperationException("boom"));

        await hub.EnterDungeon("the-halrow");

        ShouldBeError(clients.Caller, "Failed to enter dungeon");
    }

    [Fact]
    public async Task EnterDungeon_Tells_The_Caller_And_Announces_To_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new EnterDungeonHubResult { Success = true, DungeonId = "the-halrow" });

        await hub.EnterDungeon("the-halrow");

        var payload = clients.Caller.SentMessages
            .Single(m => m.Method == "DungeonEntered").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            DungeonId = "the-halrow",
            DungeonSlug = "the-halrow",
            LocationSlug = (string?)null,
            RecommendedLevel = (int?)null,
            YourLevel = 0,
            LevelWarning = (string?)null,
        });

        clients.OthersInGroupNamed("zone:crestfall").SentMessages
            .Should().ContainSingle(m => m.Method == "PlayerEntered");
    }
}
