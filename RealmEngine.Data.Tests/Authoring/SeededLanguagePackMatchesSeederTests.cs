using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Authoring.ContentPacks;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>
/// Guards the committed Calethic pack against the seeder it was generated from.
/// </summary>
/// <remarks>
/// The pack is how an existing database receives a corrected row — the seeder is insert-only, so it only ever reaches a
/// fresh database. That makes the two copies of the same language a drift risk: an edit to the seeder that nobody
/// re-serialises leaves production importable, unchanged, and silently wrong. This test closes that gap by comparing the
/// pack's payload with the seeded entity field for field, after dropping the three fields the server owns
/// (<c>id</c>, <c>version</c>, <c>updatedAt</c>). No database is needed: the pack is read from the repository and the
/// seeder runs against EF Core's in-memory provider.
/// </remarks>
[Trait("Category", "Authoring")]
public sealed class SeededLanguagePackMatchesSeederTests
{
    /// <summary>Fields the import pipeline owns, so a pack must not carry them and a comparison must ignore them.</summary>
    private static readonly string[] _serverOwnedFields = ["id", "version", "updatedAt"];

    [Fact]
    public async Task The_committed_language_pack_is_the_seeded_language_payload()
    {
        var root = FindRepositoryRoot();

        var read = ContentPackReader.Read(Path.Combine(root, "content"));

        read.Issues
            .Where(issue => issue.Severity == ContentIssueSeverity.Error)
            .Should()
            .BeEmpty("content/ must read cleanly; ContentPackValidator covers the entity rules on top of this");

        var entries = read.Files
            .Where(file => string.Equals(file.CanonicalFamily, "language", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => file.Entries)
            .ToList();

        entries.Should().ContainSingle("the pack carries exactly one language row");
        entries[0].Slug.Should().Be("calethic");

        ContentEntityTypes.TryResolve("language", out var resolver).Should().BeTrue();
        var fromPack = ContentEntityTypes.Deserialize(
            resolver,
            JsonSerializer.SerializeToElement(entries[0].Payload));

        fromPack.Should().BeOfType<Language>();

        var fromSeeder = await SeedCalethicAsync();

        Normalise(fromPack!).Should().Be(
            Normalise(fromSeeder),
            "content/languages/calethic.yaml must stay a serialisation of LanguagesSeeder.Calethic(), not a second copy of the data");

        ((Language)fromPack!).Phonology.JunctionRules[0].FormalExample
            .Should()
            .Be("vaeld + raeth → Vaelraeth", "the D-drop example names the compound Vaelraeth, as docs/lore/calethic-language.md does");
    }

    /// <summary>
    /// Walks up from the test binary until it finds the directory holding the repository's solution file.
    /// </summary>
    /// <returns>The absolute repository root.</returns>
    private static string FindRepositoryRoot()
    {
        var searched = new List<string>();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var level = 0; level <= 8 && directory is not null; level++, directory = directory.Parent)
        {
            searched.Add(directory.FullName);
            if (File.Exists(Path.Combine(directory.FullName, "RealmEngine.slnx")))
            {
                return directory.FullName;
            }
        }

        Assert.Fail(
            $"Could not find the repository root: no 'RealmEngine.slnx' in any of {string.Join(", ", searched)}.");
        return string.Empty;
    }

    /// <summary>
    /// Seeds the language catalogue into a private in-memory content context and returns the Calethic row.
    /// </summary>
    /// <returns>The seeded entity, exactly as <see cref="LanguagesSeeder"/> built it.</returns>
    private static async Task<Language> SeedCalethicAsync()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase($"calethic-pack-guard-{Guid.NewGuid()}")
            .Options;

        await using var db = new ContentDbContext(options);
        await LanguagesSeeder.SeedAsync(db);

        return await db.Languages.SingleAsync(language => language.Slug == "calethic");
    }

    /// <summary>
    /// Serialises an entity in the payload shape the content schema advertises, without the fields the server owns.
    /// </summary>
    /// <param name="entity">Either copy of the Calethic row.</param>
    /// <returns>Indented camelCase JSON, ready to be compared and diffed on failure.</returns>
    private static string Normalise(ContentBase entity)
    {
        var payload = JsonSerializer.SerializeToNode(entity, entity.GetType(), ContentEntityTypes.JsonOptions)
            as JsonObject
            ?? throw new InvalidOperationException($"{entity.GetType().Name} did not serialise to a JSON object.");

        foreach (var property in payload.Select(pair => pair.Key).ToList())
        {
            if (_serverOwnedFields.Contains(property, StringComparer.OrdinalIgnoreCase))
            {
                payload.Remove(property);
            }
        }

        return payload.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }
}
