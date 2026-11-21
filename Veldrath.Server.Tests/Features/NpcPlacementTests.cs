using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Characters.Npcs;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers NPC placement: the authored seed, the read and talk paths (which need the person to actually stand
/// where the character is), and the guard that keeps a non-hostile actor named in a spawn pool out of combat.
/// </summary>
public class NpcPlacementTests : IAsyncDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly SeededContentProbe _content = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await _content.DisposeAsync();
    }

    // ── Seed ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_Authored_Placements_Are_Unique_And_Named()
    {
        var authored = LocationNpcsSeeder.Authors();

        authored.Select(npc => npc.NpcId).Should().OnlyHaveUniqueItems();
        // D3's roster is one person per settlement location, so every reachable town is populated and the dev sandbox
        // — which is not a settlement — stays empty on purpose.
        authored.Select(npc => npc.ZoneId).Distinct().Should().BeEquivalentTo(
            ["crestfall", "aldenmere", "tolvaren", "skarhold"],
            "every reachable settlement has townspeople; the dev playground does not");
        authored.Should().OnlyContain(npc => !string.IsNullOrWhiteSpace(npc.ArchetypeSlug));
        authored.Should().OnlyContain(npc => !string.IsNullOrWhiteSpace(npc.LocationSlug));
        // The main-story quest asks the player to return cargo to the blacksmith, so he has to be findable.
        authored.Should().Contain(npc => npc.ArchetypeSlug == "village-blacksmith");
    }

    [Fact]
    public async Task Seeding_Is_Idempotent()
    {
        await using var db = _factory.CreateContext();

        await LocationNpcsSeeder.SeedAsync(db);
        await LocationNpcsSeeder.SeedAsync(db);

        db.LocationNpcs.Count().Should().Be(LocationNpcsSeeder.Authors().Length);
    }

    // ── Read path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_People_Standing_Here_Are_Listed_With_Their_Roles()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await LocationNpcsSeeder.SeedAsync(db);
        var character = await SeedCharacterAsync(db, "crestfall", "aurelian-market");

        var listed = await Reader(db, content).Handle(
            new GetLocationNpcsHubCommand(character.Id),
            CancellationToken.None);

        var smith = listed.Should().ContainSingle().Subject;
        smith.ArchetypeSlug.Should().Be("village-blacksmith");
        smith.DisplayName.Should().Be("Village Blacksmith");
        smith.IsShopkeeper.Should().BeTrue("the archetype says so");
        smith.HasDialogue.Should().BeTrue();
    }

    [Fact]
    public async Task Nobody_Is_Listed_Where_Nobody_Stands()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await LocationNpcsSeeder.SeedAsync(db);
        // A wilderness location: the roster places people in the four settlements only, never on the roads.
        var character = await SeedCharacterAsync(db, "the-droveway", "droveway-crossing");

        var listed = await Reader(db, content).Handle(
            new GetLocationNpcsHubCommand(character.Id),
            CancellationToken.None);

        listed.Should().BeEmpty();
    }

    // ── Talk path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Talking_To_The_Blacksmith_Answers_With_One_Of_His_Own_Lines()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await LocationNpcsSeeder.SeedAsync(db);
        var character = await SeedCharacterAsync(db, "crestfall", "aurelian-market");

        var result = await Speaker(db, content).Handle(
            new TalkToNpcHubCommand(character.Id, "village-blacksmith"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.DisplayName.Should().Be("Village Blacksmith");
        result.Line.Should().NotBeEmpty();
        result.IsShopkeeper.Should().BeTrue();
        result.Line.Should().ContainAny(
            "Welcome, traveler!",
            "The forge is hot and my hammer is ready.",
            "Finest ironwork in Thornveil, guaranteed.");
    }

    [Fact]
    public async Task Talking_To_Someone_Who_Is_Not_Here_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        await LocationNpcsSeeder.SeedAsync(db);
        var character = await SeedCharacterAsync(db, "crestfall", "low-quarter");

        var result = await Speaker(db, content).Handle(
            new TalkToNpcHubCommand(character.Id, "village-blacksmith"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("nobody called village-blacksmith here");
    }

    [Fact]
    public async Task Talking_To_Nobody_Is_Refused()
    {
        await using var db = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        var character = await SeedCharacterAsync(db, "crestfall", "aurelian-market");

        var result = await Speaker(db, content).Handle(
            new TalkToNpcHubCommand(character.Id, "   "),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("An NPC slug is required.");
    }

    // ── Combat guard ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Non_Hostile_Actor_In_A_Spawn_Pool_Is_Left_Out_Of_The_Roster()
    {
        var resolver = Resolver(
            new Enemy { Slug = "village-blacksmith", Name = "Village Blacksmith", Traits = Hostile(false) });

        var roster = await resolver.SpawnRosterAsync([new ActorPoolEntry("village-blacksmith", 100)]);

        roster.Should().BeEmpty("a shopkeeper named in a pool is content debt, not something to hit");
    }

    [Fact]
    public async Task A_Hostile_Actor_In_A_Spawn_Pool_Still_Spawns()
    {
        var resolver = Resolver(
            new Enemy { Slug = "goblin-scout", Name = "Goblin Scout", Traits = Hostile(true) });

        var roster = await resolver.SpawnRosterAsync([new ActorPoolEntry("goblin-scout", 100)]);

        roster.Should().NotBeEmpty();
        roster.Should().OnlyContain(enemy => enemy.ArchetypeSlug == "goblin-scout");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, TraitValue> Hostile(bool hostile) => new()
    {
        ["hostile"] = new TraitValue(hostile, TraitType.Boolean),
    };

    private static ActorPoolResolver Resolver(params Enemy[] archetypes)
    {
        var enemy = new EnemyRepository(archetypes);
        return new ActorPoolResolver(new EnemyGenerator(enemy, NullLogger<EnemyGenerator>.Instance));
    }

    private static GetLocationNpcsHubCommandHandler Reader(ApplicationDbContext db, RealmEngine.Data.Persistence.ContentDbContext content) =>
        new(
            new CharacterRepository(db),
            db,
            content,
            NullLogger<GetLocationNpcsHubCommandHandler>.Instance);

    private static TalkToNpcHubCommandHandler Speaker(ApplicationDbContext db, RealmEngine.Data.Persistence.ContentDbContext content) =>
        new(
            new CharacterRepository(db),
            db,
            content,
            new EfCoreDialogueRepository(content, NullLogger<EfCoreDialogueRepository>.Instance),
            NullLogger<TalkToNpcHubCommandHandler>.Instance);

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string zoneId,
        string locationSlug)
    {
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = zoneId;
        character.CurrentZoneLocationSlug = locationSlug;
        await db.SaveChangesAsync();
        return character;
    }

    private sealed class EnemyRepository(params Enemy[] archetypes) : IEnemyRepository
    {
        private readonly List<Enemy> _archetypes = [.. archetypes];

        public Task<List<Enemy>> GetAllAsync() => Task.FromResult(_archetypes.ToList());

        public Task<Enemy?> GetBySlugAsync(string slug) =>
            Task.FromResult(_archetypes.FirstOrDefault(enemy => enemy.Slug == slug));

        /// <summary>The model has no family key, so the roster tests only ever need the whole list.</summary>
        /// <param name="family">Ignored.</param>
        /// <returns>Every archetype in the fake.</returns>
        public Task<List<Enemy>> GetByFamilyAsync(string family) => Task.FromResult(_archetypes.ToList());
    }
}
