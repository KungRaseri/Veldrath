using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Npcs;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the dialogue options a conversation offers: the read path that decides which choices are available and why
/// the rest are not, and the choose path that applies them once.
/// </summary>
/// <remarks>
/// The content is written per test rather than seeded, because the seeded townspeople deliberately carry lines only —
/// their conversations are authored in Foundry. What is under test here is the mechanism, so a fixture entry with one
/// of each kind of requirement and grant is more useful than the real dialogue will be for a while.
/// </remarks>
public class DialogueOptionTests : IAsyncDisposable
{
    private const string Speaker = "village-blacksmith";
    private const string Here = "aurelian-market";
    private const string Elsewhere = "low-quarter";

    private readonly TestDbContextFactory _factory = new();
    private readonly SeededContentProbe _content = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await _content.DisposeAsync();
    }

    // ── Read path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_entry_with_lines_and_no_options_reads_as_prose_with_no_choices()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, options: []);
        var character = await SeedCharacterAsync(db);

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Dialogue.Should().NotBeNull();
        result.Dialogue!.HasConversation.Should().BeFalse("the seeded townspeople are spoken to, not conversed with");
        result.Dialogue.Options.Should().BeEmpty();
        result.Dialogue.Prompt.Should().NotBeEmpty("they still greet you");
        result.Dialogue.DisplayName.Should().Be("Village Blacksmith");
    }

    [Fact]
    public async Task An_available_option_is_enabled_and_previews_what_it_grants()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("buy-draught", grants: "item:healing-draught:1\ngold:25")]);
        var character = await SeedCharacterAsync(db);

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        var option = result.Dialogue!.Options.Should().ContainSingle().Subject;
        option.Key.Should().Be("buy-draught");
        option.Enabled.Should().BeTrue(option.BlockedReason);
        option.BlockedReason.Should().BeNull();
        option.Consequences.Should().Equal("1 × healing-draught", "25 gold");
    }

    [Fact]
    public async Task A_level_requirement_blocks_the_option_and_says_what_it_needs()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("ask-about-moor", requires: "level:3")]);
        var character = await SeedCharacterAsync(db);

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        var option = result.Dialogue!.Options.Single();
        option.Enabled.Should().BeFalse();
        option.BlockedReason.Should().Be("You would need level 3.");
    }

    [Fact]
    public async Task An_item_requirement_reports_the_shortfall_with_numbers()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("forge-blade", requires: "item:iron-ingot:2")]);
        var character = await SeedCharacterAsync(db);
        character.InventoryBlob = """[{"ItemRef":"iron-ingot","Quantity":1}]""";
        await db.SaveChangesAsync();

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        var option = result.Dialogue!.Options.Single();
        option.Enabled.Should().BeFalse();
        option.BlockedReason.Should().Be("You would need 2 × iron-ingot; you carry 1.");
    }

    [Fact]
    public async Task A_quest_requirement_blocks_when_the_quest_is_not_in_the_journal()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("hand-over-cargo", requires: "quest:active:the-lost-shipment")]);
        var character = await SeedCharacterAsync(db);

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        result.Dialogue!.Options.Single().BlockedReason
            .Should().Be("That needs the-lost-shipment in progress.");
    }

    [Fact]
    public async Task A_one_shot_option_already_taken_is_blocked_on_the_second_read()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("ask-about-woods", grants: "gold:5")]);
        var character = await SeedCharacterAsync(db);
        db.CharacterDialogueChoices.Add(new Veldrath.Server.Data.Entities.CharacterDialogueChoice
        {
            CharacterId = character.Id,
            SpeakerSlug = Speaker,
            OptionKey = "ask-about-woods",
            ChosenAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);

        var option = result.Dialogue!.Options.Single();
        option.Enabled.Should().BeFalse();
        option.BlockedReason.Should().Be("You have already spoken about that.");
    }

    [Fact]
    public async Task Someone_who_is_not_standing_here_is_refused()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("ask-about-woods")]);
        var character = await SeedCharacterAsync(db, Elsewhere);

        var read = await Reader(db, content).Handle(
            new GetNpcDialogueHubCommand(character.Id, Speaker),
            CancellationToken.None);
        var choose = await Chooser(db, content, new RecordingSender()).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "ask-about-woods"),
            CancellationToken.None);

        read.Success.Should().BeFalse();
        read.ErrorMessage.Should().Contain("nobody called village-blacksmith here");
        choose.Success.Should().BeFalse();
        choose.ErrorMessage.Should().Contain("nobody called village-blacksmith here");
    }

    // ── Choose path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Choosing_an_option_puts_the_item_in_the_bag_and_says_so()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("buy-draught", grants: "item:healing-draught:1")]);
        var character = await SeedCharacterAsync(db);

        var result = await Chooser(db, content, new RecordingSender()).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "buy-draught"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Response.Should().Be("Take it, and mind the edge.");
        result.Applied.Should().Equal("1 × healing-draught");
        result.Ended.Should().BeTrue("the option names no next entry");

        var stored = await db.Characters.SingleAsync(row => row.Id == character.Id);
        stored.InventoryBlob.Should().Contain("healing-draught");
    }

    [Fact]
    public async Task Gold_goes_through_the_one_writer_rather_than_being_written_here()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("sell-rumour", grants: "gold:25")]);
        var character = await SeedCharacterAsync(db);
        var sender = new RecordingSender();

        var result = await Chooser(db, content, sender).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "sell-rumour"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Applied.Should().Equal("25 gold");
        result.GoldAwarded.Should().Be(25, "the hub announces the award so the client's gold stat moves");
        result.NewGoldTotal.Should().Be(75);

        var gold = sender.Sent.OfType<AddGoldHubCommand>().Should().ContainSingle().Subject;
        gold.Amount.Should().Be(25);
        gold.CharacterId.Should().Be(character.Id);
        gold.Source.Should().StartWith("Dialogue:village-blacksmith:");
    }

    [Fact]
    public async Task A_gold_grant_the_writer_refuses_is_not_reported_as_paid()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("sell-rumour", grants: "gold:25")]);
        var character = await SeedCharacterAsync(db);
        var sender = new RecordingSender { GoldSucceeds = false };

        var result = await Chooser(db, content, sender).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "sell-rumour"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Applied.Should().BeEmpty("nothing was paid");
        result.GoldAwarded.Should().Be(0);
        result.NewGoldTotal.Should().BeNull("there is no authoritative total to announce");
    }

    [Fact]
    public async Task A_quest_grant_enrols_through_the_accept_command()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("take-the-job", grants: "quest-start:clear-the-forest")]);
        var character = await SeedCharacterAsync(db);
        var sender = new RecordingSender();

        var result = await Chooser(db, content, sender).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "take-the-job"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        sender.Sent.OfType<AcceptQuestHubCommand>().Should().ContainSingle()
            .Which.Slug.Should().Be("clear-the-forest");
    }

    [Fact]
    public async Task An_objective_grant_advances_the_journal()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("report-back", grants: "objective:talk:village-blacksmith:1")]);
        var character = await SeedCharacterAsync(db);
        character.QuestBlob = QuestJournal.Serialize(
        [
            new CharacterQuestEntry(
                "the-lost-shipment",
                "The Lost Shipment",
                "Find the cargo.",
                CharacterQuestEntry.StatusActive,
                10,
                20,
                new Dictionary<string, int> { ["talk:village-blacksmith"] = 1 },
                new Dictionary<string, int>(),
                DateTimeOffset.UtcNow),
        ]);
        await db.SaveChangesAsync();

        var result = await Chooser(db, content, new RecordingSender()).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "report-back"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Applied.Should().Equal("talk village-blacksmith");

        var stored = await db.Characters.SingleAsync(row => row.Id == character.Id);
        QuestJournal.Parse(stored.QuestBlob).Single()
            .Progress["talk:village-blacksmith"].Should().Be(1);
    }

    [Fact]
    public async Task Taking_a_one_shot_option_twice_grants_once()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("buy-draught", grants: "item:healing-draught:1")]);
        var character = await SeedCharacterAsync(db);
        var handler = Chooser(db, content, new RecordingSender());

        var first = await handler.Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "buy-draught"),
            CancellationToken.None);
        var second = await handler.Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "buy-draught"),
            CancellationToken.None);

        first.Success.Should().BeTrue("the first one is a normal purchase");
        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Be("You have already spoken about that.");

        var stored = await db.Characters.SingleAsync(row => row.Id == character.Id);
        Veldrath.Server.Features.Characters.CharacterBag.Read(stored.InventoryBlob)
            .Should().ContainSingle("the second attempt must not pay twice")
            .Which["Quantity"].GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_repeatable_option_may_be_taken_again()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("ask-again", grants: null, oneShot: false)]);
        var character = await SeedCharacterAsync(db);
        var handler = Chooser(db, content, new RecordingSender());

        var first = await handler.Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "ask-again"),
            CancellationToken.None);
        var second = await handler.Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "ask-again"),
            CancellationToken.None);

        first.Success.Should().BeTrue(first.ErrorMessage);
        second.Success.Should().BeTrue(second.ErrorMessage);
        db.CharacterDialogueChoices.Count().Should().Be(0, "only one-shot options need a record");
    }

    [Fact]
    public async Task An_unmet_requirement_refuses_and_changes_nothing()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("ask-about-moor", requires: "level:9", grants: "gold:100")]);
        var character = await SeedCharacterAsync(db);
        var sender = new RecordingSender();

        var result = await Chooser(db, content, sender).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "ask-about-moor"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You would need level 9.");
        sender.Sent.Should().BeEmpty("a refused choice must not pay out");
        db.CharacterDialogueChoices.Count().Should().Be(0, "a refused choice is not taken");
    }

    [Fact]
    public async Task An_option_key_the_content_no_longer_has_is_refused()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        await AddDialogueAsync(content, [Option("buy-draught")]);
        var character = await SeedCharacterAsync(db);

        var result = await Chooser(db, content, new RecordingSender()).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "a-removed-option"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("That is not something they say any more.");
    }

    [Fact]
    public async Task An_option_that_chains_returns_the_next_entry_with_its_own_choices()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await PlaceSpeakerAsync(db);
        // Slugs deliberately outside the seeded catalog: the seed already owns the speaker's greeting entries, and a
        // collision would make this test assert against whichever row won.
        await AddDialogueAsync(content, [Option("ask-more", next: "qa-follow-up")], slug: "qa-greeting");
        content.Dialogues.Add(new Dialogue
        {
            Slug = "qa-follow-up",
            TypeKey = "responses",
            DisplayName = "Blacksmith Follow Up",
            IsActive = true,
            Speaker = Speaker,
            Stats = new DialogueStats
            {
                Lines = ["Ask away."],
                Options = [Option("ask-about-steel")],
            },
        });
        await content.SaveChangesAsync();
        var character = await SeedCharacterAsync(db);

        var result = await Chooser(db, content, new RecordingSender()).Handle(
            new ChooseNpcOptionHubCommand(character.Id, Speaker, "ask-more", "qa-greeting"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Ended.Should().BeFalse();
        result.Next.Should().NotBeNull();
        result.Next!.EntrySlug.Should().Be("qa-follow-up");
        result.Next.Options.Should().ContainSingle().Which.Key.Should().Be("ask-about-steel");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DialogueOption Option(
        string key,
        string? requires = null,
        string? grants = null,
        string? next = null,
        bool? oneShot = null) => new()
        {
            Key = key,
            Label = $"Say {key}",
            Response = "Take it, and mind the edge.",
            Requires = requires,
            Grants = grants,
            NextSlug = next,
            OneShot = oneShot,
        };

    private static async Task PlaceSpeakerAsync(ApplicationDbContext db)
    {
        db.LocationNpcs.Add(new Veldrath.Server.Data.Entities.LocationNpc
        {
            NpcId = "aurelian-market-blacksmith",
            ZoneId = "crestfall",
            LocationSlug = Here,
            ArchetypeSlug = Speaker,
            SortOrder = 1,
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddDialogueAsync(
        ContentDbContext content,
        DialogueOption[] options,
        string slug = "blacksmith-greeting")
    {
        content.Dialogues.Add(new Dialogue
        {
            Slug = slug,
            TypeKey = "greetings",
            DisplayName = "Blacksmith Greeting",
            IsActive = true,
            Speaker = Speaker,
            Stats = new DialogueStats
            {
                Lines = ["The forge is hot and my hammer is ready."],
                Options = [.. options],
            },
        });
        await content.SaveChangesAsync();
    }

    private static GetNpcDialogueHubCommandHandler Reader(ApplicationDbContext db, ContentDbContext content) =>
        new(
            new CharacterRepository(db),
            db,
            content,
            new EfCoreDialogueRepository(content, NullLogger<EfCoreDialogueRepository>.Instance),
            NullLogger<GetNpcDialogueHubCommandHandler>.Instance);

    private static ChooseNpcOptionHubCommandHandler Chooser(
        ApplicationDbContext db,
        ContentDbContext content,
        ISender sender) =>
        new(
            new CharacterRepository(db),
            db,
            content,
            sender,
            NullLogger<ChooseNpcOptionHubCommandHandler>.Instance);

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string locationSlug = Here)
    {
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = "crestfall";
        character.CurrentZoneLocationSlug = locationSlug;
        await db.SaveChangesAsync();
        return character;
    }

    /// <summary>
    /// Stands in for MediatR: records what the handler asked for and answers as the real command would.
    /// </summary>
    /// <remarks>
    /// The real handlers are not used on purpose. Gold and quest enrolment each own a store this test is not about —
    /// what this test has to prove is that the dialogue path goes <em>through</em> them instead of re-implementing
    /// the attributes key or the journal format.
    /// </remarks>
    private sealed class RecordingSender : ISender
    {
        /// <summary>Gets the commands the handler sent, in order.</summary>
        public List<object> Sent { get; } = [];

        /// <summary>Gets a value indicating whether the simulated gold writer accepts the award.</summary>
        public bool GoldSucceeds { get; init; } = true;

        /// <inheritdoc />
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);

            object? response = request switch
            {
                AcceptQuestHubCommand => new AcceptQuestHubResult { Success = true },
                AddGoldHubCommand command => GoldSucceeds
                    ? new AddGoldHubResult { Success = true, GoldAdded = command.Amount, NewGoldTotal = 75 }
                    : new AddGoldHubResult { Success = false, ErrorMessage = "Character not found" },
                _ => null,
            };

            return Task.FromResult(response is TResponse typed ? typed : default!);
        }

        /// <inheritdoc />
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult<object?>(null);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
