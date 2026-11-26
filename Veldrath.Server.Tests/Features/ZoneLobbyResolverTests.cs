using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Tests.Infrastructure;
using ZoneLocationEntry = RealmEngine.Shared.Models.ZoneLocationEntry;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for <see cref="ZoneLobbyResolver"/>: the zone's safe location is only a lobby when this
/// character can see it, and a hidden one is reported as closed rather than as missing.
/// </summary>
public class ZoneLobbyResolverTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ZoneLocationEntry Location(
        string slug,
        string displayName,
        bool safeRespawn = false,
        bool hidden = false,
        string zoneId = "ashlen-wood") =>
        new(slug, displayName, "locations", zoneId, 10, null, null,
            IsHidden: hidden, IsSafeRespawn: safeRespawn);

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db)
    {
        var account = await HubHarness.SeedAccountAsync(db);
        return await HubHarness.SeedCharacterAsync(db, account.Id);
    }

    private static async Task UnlockAsync(ApplicationDbContext db, Guid characterId, string locationSlug)
    {
        db.CharacterUnlockedLocations.Add(new CharacterUnlockedLocation
        {
            CharacterId = characterId,
            LocationSlug = locationSlug,
            UnlockSource = "test",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Resolve_Returns_The_Zones_Safe_Location()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("trailhead", "The Trailhead"),
                Location("strangled-oak", "The Strangled Oak", safeRespawn: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.Resolved);
        resolution.Lobby!.Slug.Should().Be("strangled-oak");
        resolution.LockedLobby.Should().BeNull("the lobby was visible, so nothing was closed to the character");
    }

    [Fact]
    public async Task Resolve_Returns_A_Hidden_Safe_Location_The_Character_Unlocked()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        await UnlockAsync(db, character.Id, "strangled-oak");
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("trailhead", "The Trailhead"),
                Location("strangled-oak", "The Strangled Oak", safeRespawn: true, hidden: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.Resolved);
        resolution.Lobby!.Slug.Should().Be(
            "strangled-oak", "the plain zone lookup hides it, so only the character's unlocks make it reachable");
    }

    [Fact]
    public async Task Resolve_Reports_A_Hidden_Safe_Location_As_Not_Unlocked()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("trailhead", "The Trailhead"),
                Location("strangled-oak", "The Strangled Oak", safeRespawn: true, hidden: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(
            ZoneLobbyStatus.NotUnlocked, "the zone has a lobby; it is the character who has not opened it");
        resolution.Lobby.Should().BeNull();
        resolution.LockedLobby!.Slug.Should().Be("strangled-oak", "the caller can name what is closed and why");
        resolution.Fallback!.Slug.Should().Be("trailhead", "the entrance fallback is the first visible location");
    }

    [Fact]
    public async Task Resolve_Does_Not_Lend_One_Characters_Unlock_To_Another()
    {
        await using var db = _factory.CreateContext();
        var owner = await SeedCharacterAsync(db);
        var stranger = await SeedCharacterAsync(db);
        await UnlockAsync(db, owner.Id, "strangled-oak");
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("trailhead", "The Trailhead"),
                Location("strangled-oak", "The Strangled Oak", safeRespawn: true, hidden: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", stranger.Id);

        resolution.Status.Should().Be(
            ZoneLobbyStatus.NotUnlocked, "unlocks belong to the character who earned them");
        resolution.LockedLobby!.Slug.Should().Be("strangled-oak");
    }

    [Fact]
    public async Task Resolve_Reports_NoSafeLocation_When_None_Is_Flagged()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([Location("trailhead", "The Trailhead")]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.NoSafeLocation);
        resolution.Fallback!.Slug.Should().Be("trailhead");
        resolution.LockedLobby.Should().BeNull("a zone that flags no safe location has none hidden either");
    }

    [Fact]
    public async Task Resolve_Reports_NoLocations_For_A_Zone_With_None()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([Location("trailhead", "The Trailhead", zoneId: "crestfall")]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.NoLocations);
        resolution.Fallback.Should().BeNull();
    }
}
