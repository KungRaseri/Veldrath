using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Chests;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the location-chest listing behind <c>GetLocationChests</c>: only the unopened chests at the
/// character's own zone, location and instance run come back, ordered the way the panel draws them.
/// </summary>
public class GetLocationChestsHubCommandHandlerTests : IDisposable
{
    private const string ZoneId = "ashlen-wood";
    private const string LocationSlug = "old-clearing";

    private readonly TestDbContextFactory _factory = new();

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Listing_Without_A_Character_Is_Empty()
    {
        await using var db = _factory.CreateContext();

        var listed = await Handler(db).Handle(
            new GetLocationChestsHubCommand(Guid.NewGuid()), CancellationToken.None);

        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task Listing_Without_A_Location_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db, locationSlug: null);

        var listed = await Handler(db).Handle(
            new GetLocationChestsHubCommand(character.Id), CancellationToken.None);

        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task Listing_Returns_The_Unopened_Chests_Here_Ordered_By_Kind_Then_Name()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);

        var roaming = await SeedChestAsync(db, "Half-Buried Cache", LootChestKind.Roaming);
        var strongbox = await SeedChestAsync(db, "Mossy Strongbox", LootChestKind.Authored);
        var satchel = await SeedChestAsync(db, "Abandoned Satchel", LootChestKind.Authored);

        var listed = await Handler(db).Handle(
            new GetLocationChestsHubCommand(character.Id), CancellationToken.None);

        listed.Should().BeEquivalentTo(
            new[]
            {
                new LocationChestDto(satchel.Id, "Abandoned Satchel", LocationSlug, "Authored"),
                new LocationChestDto(strongbox.Id, "Mossy Strongbox", LocationSlug, "Authored"),
                new LocationChestDto(roaming.Id, "Half-Buried Cache", LocationSlug, "Roaming"),
            },
            options => options.WithStrictOrdering(),
            "authored chests sort before roaming ones, then by display name");
    }

    [Fact]
    public async Task Listing_Leaves_Out_What_Is_Opened_Elsewhere_Or_In_Another_Run()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);

        // A delve in progress elsewhere: its chests share the zone and location but belong to that run.
        var otherRun = new ZoneInstance
        {
            ZoneId = ZoneId,
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:{ZoneId}#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(otherRun);
        await db.SaveChangesAsync();

        var here = await SeedChestAsync(db, "Mossy Strongbox", LootChestKind.Authored);
        await SeedChestAsync(db, "Opened Strongbox", LootChestKind.Authored, isOpened: true);
        await SeedChestAsync(db, "Road Strongbox", LootChestKind.Authored, locationSlug: "wood-road");
        await SeedChestAsync(db, "Droveway Strongbox", LootChestKind.Authored, zoneId: "the-droveway");
        await SeedChestAsync(db, "Delve Strongbox", LootChestKind.Authored, zoneInstanceId: otherRun.Id);

        var listed = await Handler(db).Handle(
            new GetLocationChestsHubCommand(character.Id), CancellationToken.None);

        listed.Should().ContainSingle().Which.Id.Should().Be(here.Id);
    }

    [Fact]
    public async Task Listing_Inside_A_Run_Returns_That_Run_Chests()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);

        var run = new ZoneInstance
        {
            ZoneId = ZoneId,
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:{ZoneId}#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(run);
        await db.SaveChangesAsync();

        var session = await db.PlayerSessions.SingleAsync(row => row.CharacterId == character.Id);
        session.ZoneInstanceId = run.Id;
        await db.SaveChangesAsync();

        var inRun = await SeedChestAsync(db, "Delve Strongbox", LootChestKind.Authored, zoneInstanceId: run.Id);
        await SeedChestAsync(db, "World Strongbox", LootChestKind.Authored);

        var listed = await Handler(db).Handle(
            new GetLocationChestsHubCommand(character.Id), CancellationToken.None);

        listed.Should().ContainSingle().Which.Id.Should().Be(inRun.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GetLocationChestsHubCommandHandler Handler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            new PlayerSessionRepository(db),
            db,
            NullLogger<GetLocationChestsHubCommandHandler>.Instance);

    private static async Task<Character> SeedStandingAsync(
        ApplicationDbContext db,
        string? locationSlug = LocationSlug)
    {
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = ZoneId;
        character.CurrentZoneLocationSlug = locationSlug;
        await db.SaveChangesAsync();

        await HubHarness.SeedSessionAsync(db, character, "conn-chests", ZoneId);

        return character;
    }

    private static async Task<LootChest> SeedChestAsync(
        ApplicationDbContext db,
        string displayName,
        LootChestKind kind,
        bool isOpened = false,
        string zoneId = ZoneId,
        string locationSlug = LocationSlug,
        Guid? zoneInstanceId = null)
    {
        var chest = new LootChest
        {
            ChestId = $"test-{Guid.NewGuid():N}",
            Kind = kind,
            ZoneId = zoneId,
            LocationSlug = locationSlug,
            ZoneInstanceId = zoneInstanceId,
            DisplayName = displayName,
            LootTableRefs = "[\"@loot-tables/chests/forest:forest-chest-loot\"]",
            SpawnedAt = DateTimeOffset.UtcNow,
            IsOpened = isOpened,
        };

        db.LootChests.Add(chest);
        await db.SaveChangesAsync();
        return chest;
    }
}
