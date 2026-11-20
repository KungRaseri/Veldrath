namespace Veldrath.Server.Data.Seeders;

/// <summary>A canonical name and description pair for one seeded world row.</summary>
/// <param name="Name">The display name the row ships with (the <c>DisplayName</c> of a map node).</param>
/// <param name="Description">The description text the row ships with.</param>
public sealed record CanonicalText(string Name, string Description);

/// <summary>The canonical text of the single seeded world row.</summary>
/// <param name="Id">Stable world identifier.</param>
/// <param name="Name">World name.</param>
/// <param name="Description">World description.</param>
/// <param name="Era">Era label shown beside the description.</param>
public sealed record CanonicalWorld(string Id, string Name, string Description, string Era);

/// <summary>
/// The canon text the shipped world geography is written in — one authority shared by
/// <see cref="ApplicationDataSeeder"/> (what a fresh database gets) and <see cref="WorldCanonSweep"/> (what an
/// already-seeded database is brought back to).
/// </summary>
/// <remarks>
/// <para>
/// The seeders bail out as soon as a table has a row, which is deliberate: a restart must never overwrite content
/// an author edited. The cost is that a prose change only reaches databases that are still empty, so the sweep is
/// the path that carries a rewrite into a database migrated before it. Both paths read this table so the strings
/// exist once on the server side and cannot drift apart.
/// </para>
/// <para>
/// <see cref="Maps"/> and <see cref="MapNodes"/> mirror <c>RealmEngine.Data/Seeders/MapGraphSeeder.cs</c>, which
/// lives in <c>RealmEngine.Data</c> and cannot reference this assembly; that second copy is unavoidable, and
/// <c>WorldCanonSweepTests</c> asserts both copies stay equal. The map-graph copies of region and zone prose are
/// taken from <see cref="Regions"/> and <see cref="Zones"/> rather than retyped.
/// </para>
/// </remarks>
public static class WorldCanonText
{
    /// <summary>
    /// The retired lore tokens this file replaces. Matching is case-sensitive on purpose: the slug
    /// <c>kaldrek-maw</c> is an opaque key that appears in map node ids, zone-location rows and character
    /// position data, so a case-insensitive match would corrupt the map graph instead of the prose.
    /// </summary>
    public static IReadOnlyList<string> RetiredTokens { get; } = ["Dravan", "Thysmara", "Kaldrek", "Age of Embers"];

    /// <summary>Identifier of the single seeded world row.</summary>
    public const string WorldId = "veldrath";

    /// <summary>The seeded world row: name, description and the era the game is set in.</summary>
    public static CanonicalWorld World { get; } = new(
        WorldId,
        "Veldrath",
        "A world in the aftermath of the Collapse: the empire that broke it is gone, its ruins remain, and the magic it left behind no longer obeys.",
        "The Ashen Age (the Age of Reclaiming)");

    /// <summary>The seeded region rows, keyed by region slug.</summary>
    public static IReadOnlyDictionary<string, CanonicalText> Regions { get; } =
        new Dictionary<string, CanonicalText>(StringComparer.Ordinal)
        {
            ["varenmark"] = new(
                "Varenmark",
                "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here."),
            ["greymoor"] = new(
                "Greymoor",
                "Fog-wreathed highlands where ancient burial mounds dot the heather and wandering spirits trouble the living."),
            ["saltcliff"] = new(
                "Saltcliff",
                "Sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remains of whatever stood here before them."),
            ["cinderplain"] = new(
                "Cinderplain",
                "A scorched expanse of ash and cooled lava, where fire-touched creatures and those who work the heat stake their claim."),
        };

    /// <summary>The seeded zone rows, keyed by zone id.</summary>
    public static IReadOnlyDictionary<string, CanonicalText> Zones { get; } =
        new Dictionary<string, CanonicalText>(StringComparer.Ordinal)
        {
            ["crestfall"] = new(
                "Crestfall",
                "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles."),
            ["the-droveway"] = new(
                "The Droveway",
                "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something."),
            ["ashlen-wood"] = new(
                "The Ashlen Wood",
                "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back."),
            ["grevenmire"] = new(
                "Grevenmire",
                "Deep boggy country beyond the Ashlen Wood. Half-sunken ruins, fog that does not lift, and a hostility that feels personal."),
            ["the-halrow"] = new(
                "The Halrow",
                "A buried estate at the end of the Droveway, walls still standing but roof long collapsed. Something has lived in the cellars for a very long time."),
            ["drowning-pits"] = new(
                "The Drowning Pits",
                "Subterranean chambers beneath Grevenmire, half-flooded and ancient. Whatever built this was not building for humans."),
            ["aldenmere"] = new(
                "Aldenmere",
                "A grey-stoned waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets."),
            ["pale-moor"] = new(
                "The Pale Moor",
                "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides."),
            ["soddenfen"] = new(
                "Soddenfen",
                "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense."),
            ["barrow-deeps"] = new(
                "The Barrow Deeps",
                "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard relics older than the moor above them."),
            ["tolvaren"] = new(
                "Tolvaren",
                "A salt-caked port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders."),
            ["tidewrack-flats"] = new(
                "The Tidewrack Flats",
                "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators."),
            ["saltcliff-heights"] = new(
                "The Saltcliff Heights",
                "Wind-blasted clifftops above a sea that never settles, dotted with lighthouse ruins and contested by rival gull-rider clans."),
            ["sunken-name"] = new(
                "The Sunken Name",
                "The drowned heart of a city no surviving record names, now a flooded ruin accessible only to the brave and the waterproofed."),
            ["skarhold"] = new(
                "Skarhold",
                "A forge-city built inside a dormant caldera, where the best smiths in Veldrath ply their trade under sulphur skies."),
            ["ashfields"] = new(
                "The Ashfields",
                "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies."),
            ["smoldering-reach"] = new(
                "The Smoldering Reach",
                "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk."),
            ["kaldrek-maw"] = new(
                "The Maw",
                "A vast volcanic vent complex, lair of a fire-bound ancient and the final test of the worthy."),
            ["playground"] = new(
                "Dev Playground",
                "An unreleased sandbox zone seeded with one representative location for every game mechanic. Not accessible to players."),
        };

    /// <summary>
    /// The seeded map rows, keyed by map id. Each map carries the name and description of the region or zone that
    /// owns it, so the values are taken from <see cref="Regions"/> and <see cref="Zones"/> instead of being copied.
    /// </summary>
    public static IReadOnlyDictionary<string, CanonicalText> Maps { get; } = BuildMaps();

    /// <summary>
    /// The seeded map-node text that is not a copy of a region or zone — the node ids whose prose carried the
    /// retired lore, plus the Maw's entry and location nodes. Node ids are opaque keys: <c>kaldrek-maw</c> and
    /// <c>kaldreks-heart</c> stay exactly as they are.
    /// </summary>
    public static IReadOnlyDictionary<string, CanonicalText> MapNodes { get; } =
        new Dictionary<string, CanonicalText>(StringComparer.Ordinal)
        {
            // Zone nodes on a region map carry the zone's own name and description.
            ["region:cinderplain:zone:skarhold"] = Copy(Zones["skarhold"]),
            ["region:cinderplain:zone:kaldrek-maw"] = Copy(Zones["kaldrek-maw"]),
            ["region:greymoor:zone:aldenmere"] = Copy(Zones["aldenmere"]),
            ["region:greymoor:zone:barrow-deeps"] = Copy(Zones["barrow-deeps"]),
            ["region:saltcliff:zone:saltcliff-heights"] = Copy(Zones["saltcliff-heights"]),
            ["region:saltcliff:zone:tolvaren"] = Copy(Zones["tolvaren"]),
            ["region:saltcliff:zone:sunken-name"] = Copy(Zones["sunken-name"]),

            // Region exits carry the destination region's name and description.
            ["region:cinderplain:exit:greymoor"] = Copy(Regions["greymoor"]),
            ["region:greymoor:exit:cinderplain"] = Copy(Regions["cinderplain"]),
            ["region:greymoor:exit:saltcliff"] = Copy(Regions["saltcliff"]),
            ["region:saltcliff:exit:greymoor"] = Copy(Regions["greymoor"]),
            ["region:saltcliff:exit:cinderplain"] = Copy(Regions["cinderplain"]),
            ["region:varenmark:exit:greymoor"] = Copy(Regions["greymoor"]),

            // The Maw's own map nodes name the zone in their description, and there is no region or zone copy to
            // borrow, so these are written out.
            ["zone:kaldrek-maw:entry"] = new("Arrival", "Arrival point for the Maw."),
            ["zone:kaldrek-maw:location:fire-ancients-chamber"] = new(
                "The Fire-Ancient's Chamber",
                "The Fire-Ancient's Chamber in the Maw."),
            ["zone:kaldrek-maw:location:kaldreks-heart"] = new(
                "The Maw's Heart",
                "The Maw's Heart in the Maw. Hidden until discovered. Unlock: quest."),
            ["zone:kaldrek-maw:location:maw-descent"] = new(
                "The Maw Descent",
                "The Maw Descent in the Maw."),
        };

    /// <summary>
    /// Zone-location display names that changed, keyed by location slug. The slug is the key: only the label
    /// carries canon.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ZoneLocationDisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kaldreks-heart"] = "The Maw's Heart",
        };

    /// <summary>Reports whether <paramref name="value"/> still carries a retired lore token.</summary>
    /// <param name="value">Text to inspect; <see langword="null"/> and empty text never match.</param>
    /// <param name="token">The matched token, or <see langword="null"/> when nothing matched.</param>
    /// <returns><see langword="true"/> when a retired token is present.</returns>
    public static bool ContainsRetiredToken(string? value, out string? token)
    {
        token = null;

        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var candidate in RetiredTokens)
        {
            if (!value.Contains(candidate, StringComparison.Ordinal))
                continue;

            token = candidate;
            return true;
        }

        return false;
    }

    private static CanonicalText Copy(CanonicalText source) => new(source.Name, source.Description);

    private static IReadOnlyDictionary<string, CanonicalText> BuildMaps()
    {
        var maps = new Dictionary<string, CanonicalText>(StringComparer.Ordinal);

        foreach (var (id, text) in Regions)
            maps[$"region:{id}"] = Copy(text);

        foreach (var (id, text) in Zones)
            maps[$"zone:{id}"] = Copy(text);

        return maps;
    }
}
