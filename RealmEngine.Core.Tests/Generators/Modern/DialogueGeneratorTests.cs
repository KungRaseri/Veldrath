using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Generators.Modern;

/// <summary>
/// Behavioural tests for <see cref="DialogueGenerator"/>: type filtering, greeting/farewell/response
/// trait lookups, line projection and conversation assembly.
/// </summary>
[Trait("Category", "Generators")]
public class DialogueGeneratorTests
{
    private static ContentDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(name).Options);

    private static string Seed(Action<ContentDbContext> seed)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        seed(db);
        db.SaveChanges();
        return databaseName;
    }

    private static DialogueGenerator CreateGenerator(ContentDbContext db) =>
        new(db, NullLogger<DialogueGenerator>.Instance);

    private static bool IsGuid(string? value) => Guid.TryParse(value, out _);

    private static Dialogue MakeDialogue(
        string typeKey,
        string[] lines,
        bool isActive = true,
        bool? greeting = null,
        bool? farewell = null) => new()
        {
            Slug = $"{typeKey}-{Guid.NewGuid():N}",
            TypeKey = typeKey,
            IsActive = isActive,
            Stats = new DialogueStats { Lines = [.. lines] },
            Traits = new DialogueTraits { Greeting = greeting, Farewell = farewell }
        };

    // ── GenerateDialogueAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDialogueAsync_Returns_Requested_Count_Of_Lines_From_The_Type()
    {
        // Arrange — two greeting entries (3 lines total) plus decoys of other types and an inactive entry.
        var databaseName = Seed(db => db.Dialogues.AddRange(
            MakeDialogue("greetings", ["Well met.", "Hail, traveller."]),
            MakeDialogue("greetings", ["You again?"]),
            MakeDialogue("farewells", ["Safe roads."]),
            MakeDialogue("greetings", ["Never used."], isActive: false)));

        using var db = CreateDb(databaseName);

        // Act
        var lines = await CreateGenerator(db).GenerateDialogueAsync("greetings", "formal", count: 5);

        // Assert
        lines.Should().HaveCount(5);
        lines.Should().OnlyContain(l => l.Type == "greetings");
        lines.Should().OnlyContain(l => l.Style == "formal");
        lines.Should().OnlyContain(l => l.Text == "Well met." || l.Text == "Hail, traveller." || l.Text == "You again?");
        lines.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Id));
        lines.Should().OnlyContain(l => IsGuid(l.Id), "every projected line receives a fresh identifier");
    }

    [Fact]
    public async Task GenerateDialogueAsync_Returns_Empty_When_The_Type_Has_No_Active_Entries()
    {
        var databaseName = Seed(db => db.Dialogues.Add(
            MakeDialogue("greetings", ["Hi."], isActive: false)));

        using var db = CreateDb(databaseName);

        var lines = await CreateGenerator(db).GenerateDialogueAsync("greetings", "casual", count: 3);

        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateDialogueAsync_Returns_Empty_When_The_Entries_Have_No_Lines()
    {
        var databaseName = Seed(db => db.Dialogues.Add(MakeDialogue("greetings", [])));

        using var db = CreateDb(databaseName);

        var lines = await CreateGenerator(db).GenerateDialogueAsync("greetings", "casual", count: 3);

        lines.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateDialogueAsync_Returns_Empty_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Dialogues.Add(MakeDialogue("greetings", ["Hi."])));
        var db = CreateDb(databaseName);
        db.Dispose();

        var lines = await CreateGenerator(db).GenerateDialogueAsync("greetings", "casual", count: 3);

        lines.Should().BeEmpty();
    }

    // ── GenerateGreetingAsync / GenerateFarewellAsync ─────────────────────────

    [Fact]
    public async Task GenerateGreetingAsync_Selects_Only_Entries_Flagged_As_Greetings()
    {
        var databaseName = Seed(db => db.Dialogues.AddRange(
            MakeDialogue("greetings", ["Well met."], greeting: true),
            MakeDialogue("responses", ["Aye."]),
            MakeDialogue("greetings", ["Inactive greeting."], isActive: false, greeting: true)));

        using var db = CreateDb(databaseName);

        var line = await CreateGenerator(db).GenerateGreetingAsync("casual");

        line.Should().NotBeNull();
        line!.Text.Should().Be("Well met.");
        line.Style.Should().Be("casual");
        line.Type.Should().Be("greetings");
    }

    [Fact]
    public async Task GenerateGreetingAsync_Returns_Null_When_No_Entry_Is_Flagged()
    {
        var databaseName = Seed(db => db.Dialogues.Add(
            MakeDialogue("greetings", ["Well met."], greeting: false)));

        using var db = CreateDb(databaseName);

        (await CreateGenerator(db).GenerateGreetingAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GenerateGreetingAsync_Returns_Null_When_The_Query_Throws()
    {
        var databaseName = Seed(db => db.Dialogues.Add(MakeDialogue("greetings", ["Well met."], greeting: true)));
        var db = CreateDb(databaseName);
        db.Dispose();

        (await CreateGenerator(db).GenerateGreetingAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GenerateFarewellAsync_Selects_Only_Entries_Flagged_As_Farewells()
    {
        var databaseName = Seed(db => db.Dialogues.AddRange(
            MakeDialogue("farewells", ["Safe roads."], farewell: true),
            MakeDialogue("greetings", ["Well met."], greeting: true)));

        using var db = CreateDb(databaseName);

        var line = await CreateGenerator(db).GenerateFarewellAsync("formal");

        line.Should().NotBeNull();
        line!.Text.Should().Be("Safe roads.");
        line.Style.Should().Be("formal");
        line.Type.Should().Be("farewells");
    }

    [Fact]
    public async Task GenerateFarewellAsync_Returns_Null_When_The_Query_Throws()
    {
        var databaseName = Seed(db => db.Dialogues.Add(MakeDialogue("farewells", ["Bye."], farewell: true)));
        var db = CreateDb(databaseName);
        db.Dispose();

        (await CreateGenerator(db).GenerateFarewellAsync()).Should().BeNull();
    }

    // ── GenerateResponseAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateResponseAsync_Reads_From_The_Responses_Type_Regardless_Of_Context()
    {
        var databaseName = Seed(db => db.Dialogues.AddRange(
            MakeDialogue("responses", ["Aye.", "Nay."]),
            MakeDialogue("greetings", ["Well met."])));

        using var db = CreateDb(databaseName);

        var line = await CreateGenerator(db).GenerateResponseAsync("shop", "neutral");

        line.Should().NotBeNull();
        line!.Type.Should().Be("responses");
        line.Text.Should().BeOneOf("Aye.", "Nay.");
        line.Style.Should().Be("neutral");
    }

    [Fact]
    public async Task GenerateResponseAsync_Returns_Null_When_No_Responses_Exist()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);

        (await CreateGenerator(db).GenerateResponseAsync("shop")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateResponseAsync_Returns_Null_When_The_Query_Throws()
    {
        var databaseName = Seed(db => db.Dialogues.Add(MakeDialogue("responses", ["Aye."])));
        var db = CreateDb(databaseName);
        db.Dispose();

        (await CreateGenerator(db).GenerateResponseAsync("shop")).Should().BeNull();
    }

    // ── GenerateConversationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateConversationAsync_Assembles_Greeting_Responses_And_Farewell()
    {
        var databaseName = Seed(db => db.Dialogues.AddRange(
            MakeDialogue("greetings", ["Well met."], greeting: true),
            MakeDialogue("responses", ["Aye.", "Indeed."]),
            MakeDialogue("farewells", ["Safe roads."], farewell: true)));

        using var db = CreateDb(databaseName);

        var conversation = await CreateGenerator(db).GenerateConversationAsync("casual");

        conversation.Should().HaveCount(4, "one greeting + two responses + one farewell");
        conversation.First().Type.Should().Be("greetings");
        conversation.Last().Type.Should().Be("farewells");
        conversation.Count(l => l.Type == "responses").Should().Be(2);
        conversation.Should().OnlyContain(l => l.Style == "casual");
    }

    [Fact]
    public async Task GenerateConversationAsync_Omits_The_Sections_That_Have_No_Content()
    {
        // Arrange — responses only: no greeting and no farewell to prepend/append.
        var databaseName = Seed(db => db.Dialogues.Add(
            MakeDialogue("responses", ["Aye."])));

        using var db = CreateDb(databaseName);

        var conversation = await CreateGenerator(db).GenerateConversationAsync("neutral");

        conversation.Should().HaveCount(2);
        conversation.Should().OnlyContain(l => l.Type == "responses");
    }

    [Fact]
    public async Task GenerateConversationAsync_Returns_Empty_When_The_Catalog_Is_Empty()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);

        var conversation = await CreateGenerator(db).GenerateConversationAsync();

        conversation.Should().BeEmpty();
    }
}
