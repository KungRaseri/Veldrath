using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Tests.Content;

/// <summary>
/// Proves how <see cref="ContentValueCopy"/> copies an authored entity onto the tracked row it replaces — in particular
/// that an owned collection carried as <c>null</c> keeps the stored elements instead of clearing them.
/// </summary>
/// <remarks>
/// <c>ZoneLocation.ActorPool</c> is the only owned collection a write-path entity carries, and it is assigned wholesale
/// (<see cref="ContentValueCopy.ApplyAuthoredValues"/>), not reconciled. The shape rule refuses a payload that names it as
/// null before any write path runs; this is the level that protects a caller which skipped validation.
/// </remarks>
public class ContentValueCopyTests
{
    /// <summary>
    /// A null owned collection is not an authored set. Assigning it would delete every stored element, which is the one
    /// outcome the null-collection contract forbids — so the assignment is skipped and the pool survives a save.
    /// </summary>
    [Fact]
    public async Task ApplyAuthoredValues_leaves_an_owned_collection_carried_as_null_alone()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.ZoneLocations.SingleAsync(location => location.Slug == "drove-road");
        existing.ActorPool.Should().HaveCount(2);

        var authored = new ZoneLocation
        {
            Slug = existing.Slug,
            TypeKey = existing.TypeKey,
            DisplayName = "Drove Road (renamed)",
            ZoneId = existing.ZoneId,
            ActorPool = null!,
        };

        ContentValueCopy.ApplyAuthoredValues(authored, existing, db.Model);

        existing.ActorPool.Should().NotBeNull("a null is not an authored set");
        existing.ActorPool.Select(entry => entry.ArchetypeSlug).Should().BeEquivalentTo("giant-rat", "goblin-scout");
        existing.DisplayName.Should().Be("Drove Road (renamed)", "the scalar half of the copy still applies");

        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.ZoneLocations.SingleAsync(location => location.Slug == "drove-road");
        stored.ActorPool.Should().HaveCount(2);
        stored.DisplayName.Should().Be("Drove Road (renamed)");
    }

    /// <summary>
    /// The other direction, so the guard is not a blanket "never copy an owned collection": an authored pool replaces
    /// the stored one, dropped elements included.
    /// </summary>
    [Fact]
    public async Task ApplyAuthoredValues_replaces_an_owned_collection_the_payload_authored()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.ZoneLocations.SingleAsync(location => location.Slug == "drove-road");

        var authored = new ZoneLocation
        {
            Slug = existing.Slug,
            TypeKey = existing.TypeKey,
            DisplayName = existing.DisplayName,
            ZoneId = existing.ZoneId,
            ActorPool = [new ActorPoolEntry { ArchetypeSlug = "forest-wolf", Weight = 100 }],
        };

        ContentValueCopy.ApplyAuthoredValues(authored, existing, db.Model);
        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.ZoneLocations.SingleAsync(location => location.Slug == "drove-road");
        stored.ActorPool.Should().ContainSingle();
        stored.ActorPool[0].ArchetypeSlug.Should().Be("forest-wolf");
        stored.ActorPool[0].Weight.Should().Be(100);
    }

    /// <summary>Creates an in-memory SQLite catalog holding one zone location with a two-entry owned spawn pool.</summary>
    /// <returns>The open connection (keep it alive for the database to survive) and the context options.</returns>
    private static async Task<(SqliteConnection Connection, DbContextOptions<ContentDbContext> Options)> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

        await using var db = new ContentDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.ZoneLocations.Add(new ZoneLocation
        {
            Slug = "drove-road",
            TypeKey = "locations",
            DisplayName = "Drove Road",
            ZoneId = "varenmark",
            IsActive = true,
            ActorPool =
            [
                new ActorPoolEntry { ArchetypeSlug = "giant-rat", Weight = 60 },
                new ActorPoolEntry { ArchetypeSlug = "goblin-scout", Weight = 40 },
            ],
        });

        await db.SaveChangesAsync();

        return (connection, options);
    }
}
