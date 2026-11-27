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
        bool instanced = false,
        string zoneId = "ashlen-wood") =>
        new(slug, displayName, instanced ? "dungeons" : "locations", zoneId, 10, null, null,
            IsHidden: hidden, IsSafeRespawn: safeRespawn, IsInstanced: instanced);

    /// <summary>The slugs a zone map places on location nodes, as the handler passes them to the scoped overload.</summary>
    /// <param name="slugs">Slugs the map places.</param>
    /// <returns>The set, compared case-insensitively like the node refs it came from.</returns>
    private static IReadOnlySet<string> Placed(params string[] slugs) =>
        slugs.ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    // ── Scope: a shared anchor is not a run's lobby ────────────────────────────

    [Fact]
    public async Task Resolve_Skips_An_Instanced_Safe_Location_For_The_Shared_Scope()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true),
                Location("trailhead", "The Trailhead", safeRespawn: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.Resolved);
        resolution.Lobby!.Slug.Should().Be(
            "trailhead",
            "a run's lobby is instanced; an ordinary respawn or a reconnect must never place a character in one");
    }

    [Fact]
    public async Task Resolve_Reports_NoLocations_When_Only_Instanced_Rows_Are_Authored()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true),
                Location("crypt", "The Crypt", instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync("ashlen-wood", character.Id);

        resolution.Status.Should().Be(ZoneLobbyStatus.NoLocations);
        resolution.Fallback.Should().BeNull(
            "the entrance fallback is shared too, so an instanced row is not one either");
    }

    [Fact]
    public async Task Resolve_Scoped_Run_Returns_The_Instanced_Safe_Lobby()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("town-square", "Town Square", safeRespawn: true),
                Location("crypt", "The Crypt", instanced: true),
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true,
            Placed("town-square", "crypt", "vault-of-echoes"), preferredFallbackSlug: "crypt");

        resolution.Status.Should().Be(ZoneLobbyStatus.Resolved);
        resolution.Lobby!.Slug.Should().Be("vault-of-echoes", "both flags make it a run lobby");
        resolution.Fallback.Should().BeNull("a resolved lobby needs no fallback");
    }

    [Fact]
    public async Task Resolve_Scoped_Run_Skips_A_Lobby_The_Map_Does_Not_Place()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("crypt", "The Crypt", instanced: true),
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true, Placed("crypt"));

        resolution.Status.Should().Be(ZoneLobbyStatus.NoSafeLocation);
        resolution.Lobby.Should().BeNull("the zone map never places that room, so a client could not resume onto it");
    }

    [Fact]
    public async Task Resolve_Scoped_Run_Reports_A_Locked_Lobby_And_Keeps_The_Preferred_Room()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        // No unlock row: the run authored a lobby this character has not opened.
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("crypt", "The Crypt", instanced: true),
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, hidden: true, instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true,
            Placed("crypt", "vault-of-echoes"), preferredFallbackSlug: "crypt");

        resolution.Status.Should().Be(ZoneLobbyStatus.NotUnlocked);
        resolution.Lobby.Should().BeNull();
        resolution.LockedLobby!.Slug.Should().Be("vault-of-echoes");
        resolution.Fallback!.Slug.Should().Be(
            "crypt", "inside a run the only degradation is the room the character fell in");
    }

    [Fact]
    public async Task Resolve_Scoped_Run_Only_Falls_Back_To_The_Preferred_Room()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("antechamber", "The Antechamber", instanced: true),
                Location("crypt", "The Crypt", instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true,
            Placed("antechamber", "crypt"), preferredFallbackSlug: "crypt");

        resolution.Status.Should().Be(ZoneLobbyStatus.NoSafeLocation);
        resolution.Fallback!.Slug.Should().Be(
            "crypt",
            "the scoped path does not take whichever room sorts first; it takes the room the character is in");
    }

    [Fact]
    public async Task Resolve_Scoped_Run_Never_Falls_Back_To_A_Shared_Anchor()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("town-square", "Town Square", safeRespawn: true),
                Location("crypt", "The Crypt", instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var withPreferred = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true,
            Placed("town-square", "crypt"), preferredFallbackSlug: "crypt");
        var withoutPreferred = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: true,
            Placed("town-square", "crypt"));

        withPreferred.Fallback!.Slug.Should().Be("crypt", "a run keeps the character inside itself");
        withoutPreferred.Fallback.Should().BeNull(
            "the shared anchor belongs to the other scope, so it is not this scope's fallback");
    }

    [Fact]
    public async Task Resolve_Scoped_Public_Takes_The_Ordinal_First_Mapped_Anchor()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db);
        // Authored zulu-first on purpose: the scoped choice must not depend on catalog order.
        var resolver = new ZoneLobbyResolver(
            new FakeZoneLocationRepository([
                Location("zulu-square", "Zulu Square", safeRespawn: true),
                Location("alpha-square", "Alpha Square", safeRespawn: true),
                Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true),
            ]),
            new CharacterUnlockedLocationRepository(db));

        var resolution = await resolver.ResolveAsync(
            "ashlen-wood", character.Id, instanced: false,
            Placed("zulu-square", "alpha-square", "vault-of-echoes"));

        resolution.Status.Should().Be(ZoneLobbyStatus.Resolved);
        resolution.Lobby!.Slug.Should().Be(
            "alpha-square", "sibling anchors are broken by ordinal slug, so two content rows cannot decide a tie");
    }
}
